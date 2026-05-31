package com.lisb.reader.tts

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.os.Binder
import android.os.Build
import android.os.IBinder
import android.support.v4.media.MediaMetadataCompat
import android.support.v4.media.session.MediaSessionCompat
import android.support.v4.media.session.PlaybackStateCompat
import androidx.core.app.NotificationCompat
import androidx.media.app.NotificationCompat.MediaStyle
import com.lisb.reader.R
import com.lisb.reader.data.SettingsManager

/**
 * Foreground service that owns the [TtsManager] and exposes a MediaSession +
 * media-style notification with Previous-chapter / Play-Pause / Next-chapter
 * controls (plus Stop in the expanded view). The MediaSession lets system
 * affordances (lock-screen widget, Bluetooth headset buttons, Android Auto…)
 * drive playback in addition to the notification's own buttons.
 */
class TtsService : Service() {

    /** Lightweight chapter description used by the service to navigate without
     *  pulling in the full EpubBook (which we don't want to hold across the
     *  service's lifetime). */
    data class ChapterInfo(val title: String, val plainText: String)

    inner class LocalBinder : Binder() { val service: TtsService get() = this@TtsService }
    private val binder = LocalBinder()

    private lateinit var settings: SettingsManager
    var tts: TtsManager? = null
        private set

    private lateinit var mediaSession: MediaSessionCompat

    private var bookId: String = ""
    private var bookTitle: String = ""
    private var chapters: List<ChapterInfo> = emptyList()
    private var currentChapter: Int = 0
    private var isPlaying: Boolean = false

    // ReaderActivity hooks
    var onChunkStartExternal: ((Int) -> Unit)? = null
    var onChapterChangedExternal: ((Int) -> Unit)? = null
    var onAllFinishedExternal: (() -> Unit)? = null

    override fun onCreate() {
        super.onCreate()
        settings = SettingsManager.get(this)
        ensureChannel()
        mediaSession = MediaSessionCompat(this, "LisbTts").apply {
            setCallback(object : MediaSessionCompat.Callback() {
                override fun onPlay() { doResume() }
                override fun onPause() { doPause() }
                override fun onStop() { doStop(); stopSelf() }
                override fun onSkipToNext() { skipChapter(+1) }
                override fun onSkipToPrevious() { skipChapter(-1) }
            })
            isActive = true
        }
        tts = TtsManager(this).apply {
            setRate(settings.ttsRate)
            onChunkStart = { idx ->
                onChunkStartExternal?.invoke(idx)
                settings.saveTtsProgress(bookId, currentChapter, idx)
            }
            onAllFinished = {
                // Auto-advance to next chapter from the service so background
                // playback keeps flowing without the Activity being alive.
                if (currentChapter < chapters.lastIndex) {
                    skipChapter(+1)
                } else {
                    isPlaying = false
                    onAllFinishedExternal?.invoke()
                    updateMediaState()
                    refreshNotification()
                }
            }
        }
    }

    override fun onBind(intent: Intent?): IBinder = binder

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_PAUSE -> doPause()
            ACTION_RESUME -> doResume()
            ACTION_STOP -> { doStop(); stopSelf(); return START_NOT_STICKY }
            ACTION_NEXT -> skipChapter(+1)
            ACTION_PREV -> skipChapter(-1)
        }
        if (isPlaying || (tts?.chunkCount ?: 0) > 0) startForegroundCompat()
        return START_STICKY
    }

    // ---- Public API used by ReaderActivity ----

    fun setBook(bookId: String, bookTitle: String, chapters: List<ChapterInfo>) {
        this.bookId = bookId
        this.bookTitle = bookTitle
        this.chapters = chapters
    }

    fun startSpeaking(chapterIndex: Int, startChunk: Int) {
        if (chapters.isEmpty()) return
        currentChapter = chapterIndex.coerceIn(0, chapters.lastIndex)
        val chapter = chapters[currentChapter]
        isPlaying = true
        tts?.speak(chapter.plainText, "ch_$currentChapter", startChunk)
        updateMediaMetadata(); updateMediaState()
        startForegroundCompat()
    }

    fun doPause() {
        isPlaying = false
        tts?.pause()
        updateMediaState(); refreshNotification()
    }

    fun doResume() {
        if (chapters.isEmpty()) return
        isPlaying = true
        tts?.resume()
        updateMediaState(); refreshNotification()
    }

    fun doStop() {
        isPlaying = false
        tts?.stop()
        updateMediaState()
        stopForeground(STOP_FOREGROUND_REMOVE)
    }

    private fun skipChapter(delta: Int) {
        if (chapters.isEmpty()) return
        val next = (currentChapter + delta).coerceIn(0, chapters.lastIndex)
        if (next == currentChapter) return
        currentChapter = next
        onChapterChangedExternal?.invoke(currentChapter)
        startSpeaking(currentChapter, 0)
    }

    fun setRate(rate: Float) { tts?.setRate(rate) }

    val playing: Boolean get() = isPlaying
    val mediaSessionToken get() = mediaSession.sessionToken

    override fun onDestroy() {
        tts?.shutdown(); tts = null
        mediaSession.release()
        super.onDestroy()
    }

    // ---- Notification + MediaSession ----

    private fun ensureChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val nm = getSystemService(NotificationManager::class.java) ?: return
        if (nm.getNotificationChannel(CHANNEL_ID) != null) return
        val ch = NotificationChannel(CHANNEL_ID, "朗读控制", NotificationManager.IMPORTANCE_LOW).apply {
            description = "在通知栏显示朗读播放控制"
            setShowBadge(false)
        }
        nm.createNotificationChannel(ch)
    }

    private fun updateMediaMetadata() {
        val chapterTitle = chapters.getOrNull(currentChapter)?.title.orEmpty()
        mediaSession.setMetadata(
            MediaMetadataCompat.Builder()
                .putString(MediaMetadataCompat.METADATA_KEY_TITLE, chapterTitle)
                .putString(MediaMetadataCompat.METADATA_KEY_ARTIST, bookTitle)
                .putString(MediaMetadataCompat.METADATA_KEY_ALBUM, bookTitle)
                .build()
        )
    }

    private fun updateMediaState() {
        val state = if (isPlaying) PlaybackStateCompat.STATE_PLAYING else PlaybackStateCompat.STATE_PAUSED
        val actions = PlaybackStateCompat.ACTION_PLAY or
                PlaybackStateCompat.ACTION_PAUSE or
                PlaybackStateCompat.ACTION_PLAY_PAUSE or
                PlaybackStateCompat.ACTION_STOP or
                PlaybackStateCompat.ACTION_SKIP_TO_NEXT or
                PlaybackStateCompat.ACTION_SKIP_TO_PREVIOUS
        mediaSession.setPlaybackState(
            PlaybackStateCompat.Builder().setActions(actions).setState(state, 0L, 1.0f).build()
        )
    }

    private fun refreshNotification() {
        val nm = getSystemService(NotificationManager::class.java) ?: return
        nm.notify(NOTIF_ID, buildNotification())
    }

    private fun startForegroundCompat() {
        updateMediaMetadata(); updateMediaState()
        val n = buildNotification()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIF_ID, n, android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK)
        } else {
            startForeground(NOTIF_ID, n)
        }
    }

    private fun buildNotification(): Notification {
        val pi = { action: String ->
            PendingIntent.getBroadcast(
                this, action.hashCode(),
                Intent(this, TtsActionReceiver::class.java).setAction(action),
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
        }
        // Tap the notification to return to the reader.
        val launchIntent = packageManager.getLaunchIntentForPackage(packageName)?.apply {
            addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        }
        val contentPi = if (launchIntent != null) {
            PendingIntent.getActivity(
                this, 0, launchIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
        } else null
        val prev = NotificationCompat.Action(
            android.R.drawable.ic_media_previous, "上一章", pi(ACTION_PREV)
        )
        val playPause = if (isPlaying) {
            NotificationCompat.Action(android.R.drawable.ic_media_pause, "暂停", pi(ACTION_PAUSE))
        } else {
            NotificationCompat.Action(android.R.drawable.ic_media_play, "继续", pi(ACTION_RESUME))
        }
        val next = NotificationCompat.Action(
            android.R.drawable.ic_media_next, "下一章", pi(ACTION_NEXT)
        )
        val stop = NotificationCompat.Action(
            android.R.drawable.ic_menu_close_clear_cancel, "停止", pi(ACTION_STOP)
        )
        val chapterTitle = chapters.getOrNull(currentChapter)?.title.orEmpty()
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_launcher)
            .setContentTitle(bookTitle.ifEmpty { "正在朗读" })
            .setContentText(chapterTitle)
            .setContentIntent(contentPi)
            .setSubText(if (chapters.isNotEmpty()) "${currentChapter + 1} / ${chapters.size}" else null)
            .setOngoing(isPlaying)
            .setOnlyAlertOnce(true)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setShowWhen(false)
            .addAction(prev)
            .addAction(playPause)
            .addAction(next)
            .addAction(stop)
            .setStyle(
                MediaStyle()
                    .setMediaSession(mediaSession.sessionToken)
                    .setShowActionsInCompactView(0, 1, 2)
                    .setShowCancelButton(true)
                    .setCancelButtonIntent(
                        PendingIntent.getBroadcast(
                            this, ACTION_STOP.hashCode(),
                            Intent(this, TtsActionReceiver::class.java).setAction(ACTION_STOP),
                            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
                        )
                    )
            )
            .build()
    }

    companion object {
        const val CHANNEL_ID = "lisb_tts"
        const val NOTIF_ID = 1001

        const val ACTION_PAUSE = "com.lisb.reader.tts.PAUSE"
        const val ACTION_RESUME = "com.lisb.reader.tts.RESUME"
        const val ACTION_STOP = "com.lisb.reader.tts.STOP"
        const val ACTION_NEXT = "com.lisb.reader.tts.NEXT"
        const val ACTION_PREV = "com.lisb.reader.tts.PREV"
    }
}
