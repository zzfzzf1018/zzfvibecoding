package com.listeenb.reader;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.database.Cursor;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.IBinder;
import android.provider.OpenableColumns;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;

import com.listeenb.reader.data.ProgressStore;
import com.listeenb.reader.epub.EpubBook;
import com.listeenb.reader.epub.EpubChapter;
import com.listeenb.reader.epub.EpubParser;
import com.listeenb.reader.epub.TextBookParser;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class ReaderPlaybackService extends Service implements TextToSpeech.OnInitListener {
    public static final String ACTION_PLAY = "com.listeenb.reader.action.PLAY";
    public static final String ACTION_TOGGLE = "com.listeenb.reader.action.TOGGLE";
    public static final String ACTION_NEXT = "com.listeenb.reader.action.NEXT";
    public static final String ACTION_STOP = "com.listeenb.reader.action.STOP";
    public static final String ACTION_PROGRESS = "com.listeenb.reader.action.PROGRESS";
    public static final String EXTRA_BOOK_URI = "book_uri";
    public static final String EXTRA_CHAPTER_INDEX = "chapter_index";
    public static final String EXTRA_CHUNK_INDEX = "chunk_index";
    public static final String EXTRA_CHUNK_TOTAL = "chunk_total";
    public static final String EXTRA_IS_PLAYING = "is_playing";
    public static final String EXTRA_IS_ACTIVE = "is_active";
    public static final String EXTRA_IS_PAUSED = "is_paused";

    private static final String CHANNEL_ID = "reader_playback";
    private static final int NOTIFICATION_ID = 42;
    private static final int TTS_CHUNK_SIZE = 3200;
    private static final String GENDER_MALE = "male";
    private static final String GENDER_FEMALE = "female";

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final EpubParser epubParser = new EpubParser();
    private final TextBookParser textBookParser = new TextBookParser();

    private ProgressStore progressStore;
    private TextToSpeech textToSpeech;
    private boolean ttsReady;
    private boolean loading;
    private boolean paused;
    private EpubBook currentBook;
    private Uri currentBookUri;
    private int currentChapterIndex;
    private int speechChunkIndex;
    private List<String> speechChunks = new ArrayList<>();

    @Override
    public void onCreate() {
        super.onCreate();
        progressStore = new ProgressStore(this);
        createChannel();
        textToSpeech = new TextToSpeech(this, this);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        String action = intent == null ? ACTION_PLAY : intent.getAction();
        if (ACTION_STOP.equals(action)) {
            stopPlayback();
            stopSelf();
            return START_NOT_STICKY;
        }
        if (ACTION_TOGGLE.equals(action)) {
            togglePlayback();
            return START_STICKY;
        }
        if (ACTION_NEXT.equals(action)) {
            playNextChapter();
            return START_STICKY;
        }
        startForeground(NOTIFICATION_ID, buildNotification("准备听书", false));
        String uriText = intent == null ? null : intent.getStringExtra(EXTRA_BOOK_URI);
        int chapterIndex = intent == null ? progressStore.getChapterIndex() : intent.getIntExtra(EXTRA_CHAPTER_INDEX, progressStore.getChapterIndex());
        if (uriText == null) {
            uriText = progressStore.getBookUri();
        }
        if (uriText != null) {
            loadAndPlay(Uri.parse(uriText), chapterIndex);
        }
        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        stopPlayback();
        executor.shutdownNow();
        if (textToSpeech != null) {
            textToSpeech.shutdown();
        }
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onInit(int status) {
        ttsReady = status == TextToSpeech.SUCCESS;
        if (ttsReady) {
            int result = textToSpeech.setLanguage(Locale.getDefault());
            if (result == TextToSpeech.LANG_MISSING_DATA || result == TextToSpeech.LANG_NOT_SUPPORTED) {
                textToSpeech.setLanguage(Locale.CHINESE);
            }
            textToSpeech.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                @Override
                public void onStart(String utteranceId) {
                    broadcastProgress(true);
                }

                @Override
                public void onDone(String utteranceId) {
                    speechChunkIndex++;
                    broadcastProgress(true);
                    if (speechChunkIndex >= speechChunks.size()) {
                        playNextChapter();
                    }
                }

                @Override
                public void onError(String utteranceId) {
                    broadcastProgress(false);
                }
            });
            if (currentBook != null && !speechChunks.isEmpty() && !paused) {
                speakFromCurrentChunk();
            }
        }
    }

    private void loadAndPlay(Uri uri, int chapterIndex) {
        if (loading) {
            return;
        }
        loading = true;
        currentBookUri = uri;
        currentChapterIndex = Math.max(0, chapterIndex);
        executor.execute(() -> {
            try {
                currentBook = parseBook(uri);
                if (currentChapterIndex >= currentBook.getChapters().size()) {
                    currentChapterIndex = currentBook.getChapters().size() - 1;
                }
                progressStore.upsertBook(uri.toString(), currentBook.getTitle(), currentBook.getAuthor(), currentBook.getChapters().size(), currentChapterIndex, 0, calculatePercent());
                speechChunkIndex = 0;
                speechChunks = splitForTts(currentBook.getChapters().get(currentChapterIndex).getText());
                loading = false;
                speakFromCurrentChunk();
            } catch (Exception error) {
                loading = false;
                stopForeground(true);
                stopSelf();
            }
        });
    }

    private void speakFromCurrentChunk() {
        if (!ttsReady || currentBook == null || speechChunks.isEmpty()) {
            return;
        }
        paused = false;
        selectVoice(progressStore.getVoiceGender());
        textToSpeech.setSpeechRate(progressStore.getSpeechRate());
        textToSpeech.setPitch(progressStore.getSpeechPitch());
        textToSpeech.stop();
        startForeground(NOTIFICATION_ID, buildNotification(chapterTitle(), true));
        for (int index = speechChunkIndex; index < speechChunks.size(); index++) {
            int queueMode = index == speechChunkIndex ? TextToSpeech.QUEUE_FLUSH : TextToSpeech.QUEUE_ADD;
            textToSpeech.speak(speechChunks.get(index), queueMode, null, "svc-" + currentChapterIndex + "-" + index);
        }
        broadcastProgress(true);
    }

    private void togglePlayback() {
        if (currentBook == null) {
            return;
        }
        if (paused) {
            speakFromCurrentChunk();
        } else {
            textToSpeech.stop();
            paused = true;
            startForeground(NOTIFICATION_ID, buildNotification(chapterTitle(), false));
            broadcastProgress(false);
        }
    }

    private void playNextChapter() {
        if (currentBook == null || currentBook.isEmpty()) {
            return;
        }
        currentChapterIndex++;
        if (currentChapterIndex >= currentBook.getChapters().size()) {
            stopPlayback();
            stopSelf();
            return;
        }
        progressStore.upsertBook(currentBookUri.toString(), currentBook.getTitle(), currentBook.getAuthor(), currentBook.getChapters().size(), currentChapterIndex, 0, calculatePercent());
        speechChunkIndex = 0;
        speechChunks = splitForTts(currentBook.getChapters().get(currentChapterIndex).getText());
        speakFromCurrentChunk();
    }

    private void stopPlayback() {
        if (textToSpeech != null) {
            textToSpeech.stop();
        }
        paused = false;
        currentBook = null;
        speechChunks.clear();
        broadcastProgress(false);
        if (Build.VERSION.SDK_INT >= 24) {
            stopForeground(STOP_FOREGROUND_REMOVE);
        } else {
            stopForeground(true);
        }
    }

    private Notification buildNotification(String text, boolean playing) {
        Intent openIntent = new Intent(this, MainActivity.class);
        PendingIntent openPendingIntent = PendingIntent.getActivity(this, 0, openIntent, pendingFlags());
        Notification.Builder builder = Build.VERSION.SDK_INT >= 26
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        builder.setSmallIcon(R.drawable.ic_stat_reader)
                .setContentTitle(currentBook == null ? "ListeenB 听书" : currentBook.getTitle())
                .setContentText(text)
                .setContentIntent(openPendingIntent)
                .setOngoing(playing)
                .setShowWhen(false)
                .addAction(playing ? android.R.drawable.ic_media_pause : android.R.drawable.ic_media_play,
                        playing ? "暂停" : "继续", serviceAction(ACTION_TOGGLE, 1))
                .addAction(android.R.drawable.ic_media_next, "下一章", serviceAction(ACTION_NEXT, 2))
                .addAction(android.R.drawable.ic_menu_close_clear_cancel, "停止", serviceAction(ACTION_STOP, 3));
        return builder.build();
    }

    private PendingIntent serviceAction(String action, int requestCode) {
        Intent intent = new Intent(this, ReaderPlaybackService.class);
        intent.setAction(action);
        return PendingIntent.getService(this, requestCode, intent, pendingFlags());
    }

    private int pendingFlags() {
        return Build.VERSION.SDK_INT >= 23 ? PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE : PendingIntent.FLAG_UPDATE_CURRENT;
    }

    private void createChannel() {
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationChannel channel = new NotificationChannel(CHANNEL_ID, "听书播放", NotificationManager.IMPORTANCE_LOW);
            channel.setDescription("锁屏和后台听书控制");
            NotificationManager manager = getSystemService(NotificationManager.class);
            if (manager != null) {
                manager.createNotificationChannel(channel);
            }
        }
    }

    private void broadcastProgress(boolean playing) {
        Intent intent = new Intent(ACTION_PROGRESS);
        intent.setPackage(getPackageName());
        intent.putExtra(EXTRA_IS_PLAYING, playing && !paused);
        intent.putExtra(EXTRA_IS_ACTIVE, playing || paused);
        intent.putExtra(EXTRA_IS_PAUSED, paused);
        intent.putExtra(EXTRA_CHAPTER_INDEX, currentChapterIndex);
        intent.putExtra(EXTRA_CHUNK_INDEX, Math.max(0, speechChunkIndex));
        intent.putExtra(EXTRA_CHUNK_TOTAL, Math.max(1, speechChunks.size()));
        sendBroadcast(intent);
    }

    private String chapterTitle() {
        if (currentBook == null || currentBook.isEmpty()) {
            return "准备听书";
        }
        EpubChapter chapter = currentBook.getChapters().get(currentChapterIndex);
        return (currentChapterIndex + 1) + "/" + currentBook.getChapters().size() + " " + chapter.getTitle();
    }

    private float calculatePercent() {
        if (currentBook == null || currentBook.getChapters().isEmpty()) {
            return 0f;
        }
        return Math.min(100f, currentChapterIndex * 100f / currentBook.getChapters().size());
    }

    private void selectVoice(String gender) {
        Set<Voice> voices = textToSpeech.getVoices();
        if (voices == null || voices.isEmpty()) {
            return;
        }
        Voice fallback = null;
        for (Voice voice : voices) {
            if (voice == null || voice.getLocale() == null) {
                continue;
            }
            if (fallback == null) {
                fallback = voice;
            }
            String value = voice.getName().toLowerCase(Locale.US);
            if (voice.getFeatures() != null) {
                for (String feature : voice.getFeatures()) {
                    value += " " + feature.toLowerCase(Locale.US);
                }
            }
            if (GENDER_MALE.equals(gender) && ((value.contains("male") && !value.contains("female")) || value.contains("man") || value.contains("男"))) {
                textToSpeech.setVoice(voice);
                return;
            }
            if (GENDER_FEMALE.equals(gender) && (value.contains("female") || value.contains("woman") || value.contains("女"))) {
                textToSpeech.setVoice(voice);
                return;
            }
        }
        if (fallback != null) {
            textToSpeech.setVoice(fallback);
        }
    }

    private EpubBook parseBook(Uri uri) throws Exception {
        String name = getDisplayName(uri);
        String type = getContentResolver().getType(uri);
        String lowerName = name == null ? "" : name.toLowerCase(Locale.US);
        String lowerType = type == null ? "" : type.toLowerCase(Locale.US);
        if (lowerName.endsWith(".txt") || lowerType.startsWith("text/")) {
            return textBookParser.parse(getContentResolver(), uri, name);
        }
        return epubParser.parse(getContentResolver(), uri);
    }

    private String getDisplayName(Uri uri) {
        try (Cursor cursor = getContentResolver().query(uri, new String[]{OpenableColumns.DISPLAY_NAME}, null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                return cursor.getString(0);
            }
        } catch (Exception ignored) {
        }
        String path = uri.getLastPathSegment();
        return path == null ? "本地书籍" : path;
    }

    private List<String> splitForTts(String text) {
        List<String> chunks = new ArrayList<>();
        String[] paragraphs = text.split("\\n+");
        StringBuilder current = new StringBuilder();
        for (String paragraph : paragraphs) {
            String trimmed = paragraph.trim();
            if (trimmed.isEmpty()) {
                continue;
            }
            if (current.length() + trimmed.length() + 1 > TTS_CHUNK_SIZE) {
                if (current.length() > 0) {
                    chunks.add(current.toString());
                    current.setLength(0);
                }
                while (trimmed.length() > TTS_CHUNK_SIZE) {
                    chunks.add(trimmed.substring(0, TTS_CHUNK_SIZE));
                    trimmed = trimmed.substring(TTS_CHUNK_SIZE);
                }
            }
            if (current.length() > 0) {
                current.append('\n');
            }
            current.append(trimmed);
        }
        if (current.length() > 0) {
            chunks.add(current.toString());
        }
        if (chunks.isEmpty()) {
            chunks.add("当前章节没有可朗读内容");
        }
        return chunks;
    }
}