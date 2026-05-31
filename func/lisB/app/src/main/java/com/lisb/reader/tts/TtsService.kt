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
import androidx.core.app.NotificationCompat
import com.lisb.reader.R
import com.lisb.reader.data.SettingsManager

/**
 * Foreground service that owns the [TtsManager] across the app lifecycle and
 * shows a media-style notification with Play/Pause/Stop. The notification
 * stays visible whenever a chapter is loaded; closing the app does not stop
 * reading.
 */
class TtsService : Service() {

    inner class LocalBinder : Binder() { val service: TtsService get() = this@TtsService }
    private val binder = LocalBinder()

    private lateinit var settings: SettingsManager
    var tts: TtsManager? = null
        private set

    private var bookId: String = ""
    private var bookTitle: String = ""
    private var chapterTitle: String = ""
    private var currentChapter: Int = 0
    private var isPlaying: Boolean = false

    // External listeners (Activity uses these to react to chunk progress / done)
    var onChunkStartExternal: ((Int) -> Unit)? = null
    var onAllFinishedExternal: (() -> Unit)? = null

    override fun onCreate() {
        super.onCreate()
        settings = SettingsManager.get(this)
        ensureChannel()
        tts = TtsManager(this).apply {
            setRate(settings.ttsRate)
            onChunkStart = { idx ->
                onChunkStartExternal?.invoke(idx)
                settings.saveTtsProgress(bookId, currentChapter, idx)
            }
            onAllFinished = {
                onAllFinishedExternal?.invoke()
            }
        }
    }

    override fun onBind(intent: Intent?): IBinder = binder

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_PAUSE -> doPause()
            ACTION_RESUME -> doResume()
            ACTION_STOP -> { doStop(); stopSelf(); return START_NOT_STICKY }
        }
        // Ensure foreground state if we have anything to show.
        if (isPlaying || (tts?.chunkCount ?: 0) > 0) {
            startForegroundCompat()
        }
        return START_STICKY
    }

    fun startSpeaking(
        bookId: String,
        bookTitle: String,
        chapterTitle: String,
        chapterIndex: Int,
        text: String,
        startChunk: Int
    ) {
        this.bookId = bookId
        this.bookTitle = bookTitle
        this.chapterTitle = chapterTitle
        this.currentChapter = chapterIndex
        isPlaying = true
        tts?.speak(text, "ch_$chapterIndex", startChunk)
        startForegroundCompat()
    }

    fun doPause() {
        isPlaying = false
        tts?.pause()
        startForegroundCompat()
    }

    fun doResume() {
        isPlaying = true
        tts?.resume()
        startForegroundCompat()
    }

    fun doStop() {
        isPlaying = false
        tts?.stop()
        stopForeground(STOP_FOREGROUND_REMOVE)
    }

    fun setRate(rate: Float) { tts?.setRate(rate) }

    val playing: Boolean get() = isPlaying

    override fun onDestroy() {
        tts?.shutdown(); tts = null
        super.onDestroy()
    }

    // ---- Notification ----

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

    private fun startForegroundCompat() {
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
        val playPauseAction = if (isPlaying) {
            NotificationCompat.Action(android.R.drawable.ic_media_pause, "暂停", pi(ACTION_PAUSE))
        } else {
            NotificationCompat.Action(android.R.drawable.ic_media_play, "继续", pi(ACTION_RESUME))
        }
        val stopAction = NotificationCompat.Action(
            android.R.drawable.ic_menu_close_clear_cancel, "停止", pi(ACTION_STOP)
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_launcher)
            .setContentTitle(if (bookTitle.isNotEmpty()) bookTitle else "正在朗读")
            .setContentText(chapterTitle)
            .setOngoing(isPlaying)
            .setOnlyAlertOnce(true)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .addAction(playPauseAction)
            .addAction(stopAction)
            .build()
    }

    companion object {
        const val CHANNEL_ID = "lisb_tts"
        const val NOTIF_ID = 1001

        const val ACTION_PAUSE = "com.lisb.reader.tts.PAUSE"
        const val ACTION_RESUME = "com.lisb.reader.tts.RESUME"
        const val ACTION_STOP = "com.lisb.reader.tts.STOP"
    }
}
