package com.listeenb.reader;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.DocumentsContract;
import android.provider.OpenableColumns;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;
import android.text.InputType;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.TextUtils;
import android.text.style.BackgroundColorSpan;
import android.view.GestureDetector;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import com.listeenb.reader.data.ProgressStore;
import com.listeenb.reader.data.ProgressStore.BookRecord;
import com.listeenb.reader.data.ProgressStore.Bookmark;
import com.listeenb.reader.epub.EpubBook;
import com.listeenb.reader.epub.EpubChapter;
import com.listeenb.reader.epub.EpubParser;
import com.listeenb.reader.epub.TextBookParser;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends Activity implements TextToSpeech.OnInitListener {
    private static final int REQUEST_OPEN_EPUB = 1001;
    private static final int REQUEST_OPEN_FOLDER = 1002;
    private static final String GENDER_MALE = "male";
    private static final String GENDER_FEMALE = "female";
    private static final int TTS_CHUNK_SIZE = 3200;
    private static final int SORT_RECENT = 0;
    private static final int SORT_TITLE = 1;
    private static final int SORT_AUTHOR = 2;
    private static final int SORT_PROGRESS = 3;

    private final EpubParser epubParser = new EpubParser();
    private final TextBookParser textBookParser = new TextBookParser();
    private final ExecutorService executorService = Executors.newSingleThreadExecutor();
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final BroadcastReceiver playbackReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            if (ReaderPlaybackService.ACTION_PROGRESS.equals(intent.getAction())) {
                handlePlaybackProgress(intent);
            }
        }
    };

    private ProgressStore progressStore;
    private TextToSpeech textToSpeech;
    private boolean ttsReady;
    private boolean speakingPaused;
    private boolean speakingActive;
    private EpubBook currentBook;
    private Uri currentBookUri;
    private int currentChapterIndex;
    private List<String> speechChunks = new ArrayList<>();
    private int speechChunkIndex;
    private float textSize;
    private float lineSpacing;
    private boolean nightMode;
    private float speechRate;
    private float speechPitch;
    private String themeName;
    private int bookshelfSort = SORT_RECENT;
    private boolean bookshelfOnlyUnread;
    private boolean showingHome;
    private GestureDetector gestureDetector;

    private boolean immersiveMode;
    private LinearLayout topBar;
    private LinearLayout infoRow;
    private LinearLayout actionRow;
    private LinearLayout controlsRow;

    private LinearLayout root;
    private TextView titleView;
    private TextView metaView;
    private TextView chapterView;
    private TextView contentView;
    private TextView statusView;
    private ImageView coverView;
    private ScrollView scrollView;
    private Button previousButton;
    private Button nextButton;
    private Button listenButton;
    private Button pauseButton;
    private Button stopButton;
    private RadioGroup genderGroup;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        progressStore = new ProgressStore(this);
        textSize = progressStore.getTextSize();
        lineSpacing = progressStore.getLineSpacing();
        nightMode = progressStore.isNightMode();
        themeName = progressStore.getTheme();
        speechRate = progressStore.getSpeechRate();
        speechPitch = progressStore.getSpeechPitch();
        textToSpeech = new TextToSpeech(this, this);
        buildUi();
        requestNotificationPermissionIfNeeded();
        restoreVoicePreference();
        registerPlaybackReceiver();
        showHome();
        restoreLastBook();
        registerBackCallback();
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus && immersiveMode) {
            enterImmersiveMode();
        }
    }

    @Override
    protected void onDestroy() {
        saveCurrentProgress();
        executorService.shutdownNow();
        if (textToSpeech != null) {
            textToSpeech.stop();
            textToSpeech.shutdown();
        }
        try {
            unregisterReceiver(playbackReceiver);
        } catch (RuntimeException ignored) {
        }
        super.onDestroy();
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
                }

                @Override
                public void onDone(String utteranceId) {
                    speechChunkIndex++;
                    if (speechChunkIndex >= speechChunks.size()) {
                        handler.post(() -> {
                            speakingActive = false;
                            speakingPaused = false;
                            statusView.setText("本章朗读完成");
                            updateNavigationState();
                        });
                    }
                }

                @SuppressWarnings("deprecation")
                @Override
                public void onError(String utteranceId) {
                    handler.post(() -> statusView.setText("朗读失败"));
                }
            });
            statusView.setText("语音已就绪");
        } else {
            statusView.setText("当前设备没有可用的语音引擎");
        }
        updateNavigationState();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            return;
        }
        Uri uri = data.getData();
        int flags = data.getFlags() & Intent.FLAG_GRANT_READ_URI_PERMISSION;
        try {
            getContentResolver().takePersistableUriPermission(uri, flags);
        } catch (RuntimeException ignored) {
        }
        if (requestCode == REQUEST_OPEN_EPUB) {
            loadBook(uri, true, 0, 0);
        } else if (requestCode == REQUEST_OPEN_FOLDER) {
            progressStore.saveImportFolderUri(uri.toString());
            scanFolder(uri);
        }
    }

    private void buildUi() {
        root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(12), dp(10), dp(12), dp(10));

        topBar = new LinearLayout(this);
        topBar.setGravity(Gravity.CENTER_VERTICAL);
        topBar.setOrientation(LinearLayout.HORIZONTAL);

        titleView = new TextView(this);
        titleView.setText("ListeenB");
        titleView.setTextSize(20);
        titleView.setSingleLine(true);
        titleView.setEllipsize(TextUtils.TruncateAt.END);
        topBar.addView(titleView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        topBar.addView(commandButton("首页", view -> showHome()));
        topBar.addView(commandButton("书架", view -> showBookshelfOptions()));
        topBar.addView(commandButton("导入", view -> openDocumentPicker()));
        topBar.addView(commandButton("管理", view -> showImportManager()));
        root.addView(topBar);

        infoRow = new LinearLayout(this);
        infoRow.setOrientation(LinearLayout.HORIZONTAL);
        infoRow.setGravity(Gravity.CENTER_VERTICAL);
        coverView = new ImageView(this);
        coverView.setBackgroundColor(0xFFE0E0E0);
        infoRow.addView(coverView, new LinearLayout.LayoutParams(dp(54), dp(74)));

        LinearLayout metaBox = new LinearLayout(this);
        metaBox.setOrientation(LinearLayout.VERTICAL);
        metaBox.setPadding(dp(10), 0, 0, 0);
        metaView = new TextView(this);
        metaView.setText("本地 EPUB 阅读器");
        metaView.setTextSize(13);
        chapterView = new TextView(this);
        chapterView.setText("请选择一本 EPUB 书籍");
        chapterView.setTextSize(15);
        chapterView.setPadding(0, dp(4), 0, dp(4));
        metaBox.addView(metaView);
        metaBox.addView(chapterView);
        infoRow.addView(metaBox, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        root.addView(infoRow);

        actionRow = new LinearLayout(this);
        actionRow.setOrientation(LinearLayout.HORIZONTAL);
        actionRow.addView(commandButton("目录", view -> showToc()), weightParams());
        actionRow.addView(commandButton("设置", view -> showReadingSettings()), weightParams());
        actionRow.addView(commandButton("搜索", view -> showSearchDialog()), weightParams());
        actionRow.addView(commandButton("书签", view -> showBookmarkMenu()), weightParams());
        root.addView(actionRow);

        scrollView = new ScrollView(this);
        contentView = new TextView(this);
        contentView.setText("点击“导入”选择本地 EPUB，或从“书架”打开最近阅读的书。\n\n已支持书架、目录、阅读设置、听书控制、全文搜索、封面元数据、书签笔记和每本书进度保存。");
        contentView.setPadding(0, dp(8), 0, dp(24));
        scrollView.addView(contentView);
        scrollView.setOnScrollChangeListener((view, scrollX, scrollY, oldScrollX, oldScrollY) -> saveCurrentProgress());
        setupGestures();
        root.addView(scrollView, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));

        genderGroup = new RadioGroup(this);
        genderGroup.setOrientation(RadioGroup.HORIZONTAL);
        addGenderButton("女声", GENDER_FEMALE);
        addGenderButton("男声", GENDER_MALE);
        genderGroup.setOnCheckedChangeListener((group, checkedId) -> {
            View checked = group.findViewById(checkedId);
            if (checked != null && checked.getTag() != null) {
                String gender = checked.getTag().toString();
                progressStore.saveVoiceGender(gender);
                // If playback is active, restart with new voice
                if (speakingActive) {
                    startPlaybackService(ReaderPlaybackService.ACTION_PLAY);
                }
            }
        });
        root.addView(genderGroup);

        controlsRow = new LinearLayout(this);
        controlsRow.setOrientation(LinearLayout.HORIZONTAL);
        previousButton = commandButton("上一章", view -> showChapter(currentChapterIndex - 1, 0));
        listenButton = commandButton("播放", view -> startPlaybackService(ReaderPlaybackService.ACTION_PLAY));
        pauseButton = commandButton("暂停", view -> startPlaybackService(ReaderPlaybackService.ACTION_TOGGLE));
        stopButton = commandButton("停止", view -> startPlaybackService(ReaderPlaybackService.ACTION_STOP));
        nextButton = commandButton("下一章", view -> showChapter(currentChapterIndex + 1, 0));
        controlsRow.addView(previousButton, weightParams());
        controlsRow.addView(listenButton, weightParams());
        controlsRow.addView(pauseButton, weightParams());
        controlsRow.addView(stopButton, weightParams());
        controlsRow.addView(nextButton, weightParams());
        root.addView(controlsRow);

        statusView = new TextView(this);
        statusView.setText("准备就绪");
        statusView.setTextSize(13);
        statusView.setPadding(0, dp(6), 0, 0);
        root.addView(statusView);

        setContentView(root);
        applyResponsiveLayout();
        applyReadingTheme();
        updateNavigationState();
    }

    private Button commandButton(String text, View.OnClickListener listener) {
        Button button = new Button(this);
        button.setText(text);
        button.setAllCaps(false);
        button.setOnClickListener(listener);
        return button;
    }

    private LinearLayout.LayoutParams weightParams() {
        return new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
    }

    private void addGenderButton(String text, String gender) {
        RadioButton button = new RadioButton(this);
        button.setText(text);
        button.setId(View.generateViewId());
        button.setTag(gender);
        genderGroup.addView(button);
    }

    private void restoreVoicePreference() {
        String gender = progressStore.getVoiceGender();
        for (int index = 0; index < genderGroup.getChildCount(); index++) {
            View child = genderGroup.getChildAt(index);
            if (gender.equals(child.getTag())) {
                genderGroup.check(child.getId());
                return;
            }
        }
        if (genderGroup.getChildCount() > 0) {
            genderGroup.check(genderGroup.getChildAt(0).getId());
        }
    }

    private void restoreLastBook() {
        String uriText = progressStore.getBookUri();
        if (uriText != null) {
            BookRecord record = progressStore.findBook(uriText);
            int chapter = record == null ? progressStore.getChapterIndex() : record.chapterIndex;
            int scrollY = record == null ? progressStore.getScrollY() : record.scrollY;
            loadBook(Uri.parse(uriText), false, chapter, scrollY);
        }
    }

    private void showHome() {
        showingHome = true;
        currentBook = null;
        currentBookUri = null;
        titleView.setText("ListeenB");
        metaView.setText("最近阅读首页");
        chapterView.setText("导入、书架、搜索和听书从这里开始");
        updateCover(null);
        List<BookRecord> books = progressStore.getBooks();
        StringBuilder builder = new StringBuilder();
        builder.append("继续阅读\n\n");
        if (books.isEmpty()) {
            builder.append("书架为空。点击“导入”选择 EPUB/TXT，或点击“管理”从文件夹批量导入。\n");
        } else {
            int count = Math.min(5, books.size());
            for (int index = 0; index < count; index++) {
                BookRecord book = books.get(index);
                builder.append(index + 1).append(". ").append(book.title)
                        .append("\n   ").append(book.author)
                        .append(" · ").append(Math.round(book.percent)).append("%")
                        .append(" · ").append(formatDate(book.updatedAt)).append("\n\n");
            }
            builder.append("点击“书架”可按最近、书名、作者或进度排序，并继续阅读。\n");
        }
        builder.append("\n手势：阅读时左右滑动切换章节，双击正文打开/关闭目录。");
        contentView.setText(builder.toString());
        scrollView.scrollTo(0, 0);
        updateNavigationState();
    }

    private void openDocumentPicker() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{"application/epub+zip", "text/plain"});
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        try {
            startActivityForResult(intent, REQUEST_OPEN_EPUB);
        } catch (Exception firstFailure) {
            Intent fallback = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            fallback.addCategory(Intent.CATEGORY_OPENABLE);
            fallback.setType("*/*");
            fallback.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
            startActivityForResult(fallback, REQUEST_OPEN_EPUB);
        }
    }

    private void openFolderPicker() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_OPEN_FOLDER);
    }

    private void showImportManager() {
        new AlertDialog.Builder(this)
                .setTitle("导入管理")
                .setItems(new String[]{"从文件夹批量导入", "重新扫描上次文件夹", "删除书架书籍"}, (dialog, which) -> {
                    if (which == 0) {
                        openFolderPicker();
                    } else if (which == 1) {
                        rescanLastFolder();
                    } else {
                        showDeleteBooks();
                    }
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private void rescanLastFolder() {
        String folderUri = progressStore.getImportFolderUri();
        if (folderUri == null) {
            Toast.makeText(this, "还没有选择过导入文件夹", Toast.LENGTH_SHORT).show();
            return;
        }
        scanFolder(Uri.parse(folderUri));
    }

    private void scanFolder(Uri folderUri) {
        statusView.setText("正在扫描文件夹...");
        executorService.execute(() -> {
            int[] counts = new int[]{0, 0};
            List<String> failures = new ArrayList<>();
            try {
                String treeId = DocumentsContract.getTreeDocumentId(folderUri);
                Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(folderUri, treeId);
                String[] projection = new String[]{DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME, DocumentsContract.Document.COLUMN_MIME_TYPE};
                try (Cursor cursor = getContentResolver().query(childrenUri, projection, null, null, null)) {
                    if (cursor != null) {
                        while (cursor.moveToNext()) {
                            String documentId = cursor.getString(0);
                            String name = cursor.getString(1);
                            String mimeType = cursor.getString(2);
                            if (!isReadableBookFile(name, mimeType)) {
                                continue;
                            }
                            Uri documentUri = DocumentsContract.buildDocumentUriUsingTree(folderUri, documentId);
                            try {
                                EpubBook book = parseBook(documentUri, name, mimeType);
                                progressStore.upsertScannedBook(documentUri.toString(), book.getTitle(), book.getAuthor(), book.getChapters().size());
                                counts[0]++;
                            } catch (Exception error) {
                                counts[1]++;
                                failures.add((name == null ? documentUri.toString() : name) + "：" + error.getMessage());
                            }
                        }
                    }
                }
                runOnUiThread(() -> showImportReport(counts[0], counts[1], failures));
            } catch (Exception error) {
                runOnUiThread(() -> Toast.makeText(this, "扫描失败：" + error.getMessage(), Toast.LENGTH_LONG).show());
            }
        });
    }

    private void requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission("android.permission.POST_NOTIFICATIONS") != android.content.pm.PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[]{"android.permission.POST_NOTIFICATIONS"}, 3001);
        }
    }

    private boolean isReadableBookFile(String name, String mimeType) {
        String lowerName = name == null ? "" : name.toLowerCase(Locale.US);
        String lowerMime = mimeType == null ? "" : mimeType.toLowerCase(Locale.US);
        return lowerName.endsWith(".epub") || lowerName.endsWith(".txt") || lowerMime.contains("epub") || lowerMime.startsWith("text/");
    }

    private EpubBook parseBook(Uri uri, String name, String mimeType) throws Exception {
        String lowerName = name == null ? "" : name.toLowerCase(Locale.US);
        String lowerMime = mimeType == null ? "" : mimeType.toLowerCase(Locale.US);
        if (lowerName.endsWith(".txt") || lowerMime.startsWith("text/")) {
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

    private String getMimeType(Uri uri) {
        String type = getContentResolver().getType(uri);
        return type == null ? "" : type;
    }

    private void showImportReport(int imported, int failed, List<String> failures) {
        statusView.setText("扫描完成：导入 " + imported + " 本，失败 " + failed + " 本");
        if (failures.isEmpty()) {
            Toast.makeText(this, "导入完成：" + imported + " 本", Toast.LENGTH_SHORT).show();
            return;
        }
        StringBuilder builder = new StringBuilder();
        for (String failure : failures) {
            builder.append(failure).append("\n\n");
        }
        new AlertDialog.Builder(this)
                .setTitle("导入失败报告")
                .setMessage(builder.toString())
                .setPositiveButton("知道了", null)
                .show();
    }

    private void setupGestures() {
        gestureDetector = new GestureDetector(this, new GestureDetector.SimpleOnGestureListener() {
            @Override
            public boolean onSingleTapConfirmed(MotionEvent event) {
                if (currentBook == null) {
                    return false;
                }
                float x = event.getX();
                float y = event.getY();
                int width = scrollView.getWidth();
                int height = scrollView.getHeight();
                // Tap center to toggle immersive/chrome
                if (x > width * 0.3f && x < width * 0.7f && y > height * 0.3f && y < height * 0.7f) {
                    toggleChromeVisibility();
                    return true;
                }
                // Tap right or bottom: next page
                if (x > width * 0.7f || (y > height * 0.7f && x >= width * 0.3f)) {
                    scrollPage(true);
                    return true;
                }
                // Tap left or top: previous page
                if (x < width * 0.3f || y < height * 0.3f) {
                    scrollPage(false);
                    return true;
                }
                return false;
            }

            @Override
            public boolean onDoubleTap(MotionEvent event) {
                if (currentBook != null) {
                    showToc();
                }
                return true;
            }

            @Override
            public boolean onFling(MotionEvent down, MotionEvent up, float velocityX, float velocityY) {
                if (currentBook == null || down == null || up == null) {
                    return false;
                }
                float deltaX = up.getX() - down.getX();
                float deltaY = up.getY() - down.getY();
                if (Math.abs(deltaX) > dp(72) && Math.abs(deltaX) > Math.abs(deltaY) * 1.4f) {
                    if (deltaX < 0) {
                        showChapter(currentChapterIndex + 1, 0);
                    } else {
                        showChapter(currentChapterIndex - 1, 0);
                    }
                    return true;
                }
                return false;
            }
        });
        scrollView.setOnTouchListener((view, event) -> {
            if (gestureDetector != null) {
                gestureDetector.onTouchEvent(event);
            }
            return false;
        });
    }

    private void scrollPage(boolean forward) {
        int pageHeight = scrollView.getHeight() - dp(40);
        if (forward) {
            int maxScroll = scrollView.getChildAt(0).getHeight() - scrollView.getHeight();
            if (scrollView.getScrollY() >= maxScroll - dp(10)) {
                // At bottom of chapter, go to next chapter
                if (currentBook != null && currentChapterIndex < currentBook.getChapters().size() - 1) {
                    showChapter(currentChapterIndex + 1, 0);
                }
            } else {
                scrollView.smoothScrollBy(0, pageHeight);
            }
        } else {
            if (scrollView.getScrollY() <= dp(10)) {
                // At top of chapter, go to previous chapter
                if (currentBook != null && currentChapterIndex > 0) {
                    showChapter(currentChapterIndex - 1, 0);
                }
            } else {
                scrollView.smoothScrollBy(0, -pageHeight);
            }
        }
    }

    private void toggleChromeVisibility() {
        immersiveMode = !immersiveMode;
        int visibility = immersiveMode ? View.GONE : View.VISIBLE;
        topBar.setVisibility(visibility);
        infoRow.setVisibility(visibility);
        actionRow.setVisibility(visibility);
        genderGroup.setVisibility(visibility);
        controlsRow.setVisibility(visibility);
        statusView.setVisibility(visibility);
        if (immersiveMode) {
            enterImmersiveMode();
            root.setPadding(dp(8), dp(4), dp(8), dp(4));
        } else {
            exitImmersiveMode();
            applyResponsiveLayout();
        }
    }

    @SuppressWarnings("deprecation")
    private void enterImmersiveMode() {
        if (Build.VERSION.SDK_INT >= 30) {
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.hide(WindowInsets.Type.statusBars() | WindowInsets.Type.navigationBars());
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(
                    View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                    | View.SYSTEM_UI_FLAG_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                    | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                    | View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        }
    }

    @SuppressWarnings("deprecation")
    private void exitImmersiveMode() {
        if (Build.VERSION.SDK_INT >= 30) {
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.show(WindowInsets.Type.statusBars() | WindowInsets.Type.navigationBars());
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        }
    }

    private void registerBackCallback() {
        if (Build.VERSION.SDK_INT >= 33) {
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(
                    android.window.OnBackInvokedDispatcher.PRIORITY_DEFAULT,
                    () -> handleBack());
        }
    }

    @SuppressWarnings("deprecation")
    @Override
    public void onBackPressed() {
        if (!handleBack()) {
            super.onBackPressed();
        }
    }

    private boolean handleBack() {
        if (immersiveMode) {
            toggleChromeVisibility();
            return true;
        }
        if (currentBook != null && !showingHome) {
            showHome();
            return true;
        }
        return false;
    }

    private void showDeleteBooks() {
        List<BookRecord> books = progressStore.getBooks();
        if (books.isEmpty()) {
            Toast.makeText(this, "书架为空", Toast.LENGTH_SHORT).show();
            return;
        }
        String[] labels = new String[books.size()];
        for (int index = 0; index < books.size(); index++) {
            BookRecord book = books.get(index);
            labels[index] = book.title + "\n" + book.author + " · " + Math.round(book.percent) + "%";
        }
        new AlertDialog.Builder(this)
                .setTitle("删除书籍")
                .setItems(labels, (dialog, which) -> confirmDeleteBook(books.get(which)))
                .setNegativeButton("关闭", null)
                .show();
    }

    private void confirmDeleteBook(BookRecord book) {
        new AlertDialog.Builder(this)
                .setTitle("删除书籍")
                .setMessage("从书架删除《" + book.title + "》？不会删除原始 EPUB 文件。")
                .setPositiveButton("删除", (dialog, which) -> {
                    progressStore.removeBook(book.uri);
                    if (currentBookUri != null && book.uri.equals(currentBookUri.toString())) {
                        stopPlaybackServiceOnly();
                        currentBook = null;
                        currentBookUri = null;
                        titleView.setText("ListeenB");
                        metaView.setText("本地 EPUB 阅读器");
                        chapterView.setText("请选择一本 EPUB 书籍");
                        contentView.setText("书籍已从书架删除。可以继续导入 EPUB，或从书架打开其他书。");
                        updateCover(null);
                        updateNavigationState();
                    }
                    Toast.makeText(this, "已删除书架记录", Toast.LENGTH_SHORT).show();
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private void loadBook(Uri uri, boolean saveUri, int chapterIndex, int scrollY) {
        statusView.setText("正在解析 EPUB...");
        setReaderEnabled(false);
        executorService.execute(() -> {
            try {
                EpubBook parsed = parseBook(uri, getDisplayName(uri), getMimeType(uri));
                runOnUiThread(() -> {
                    showingHome = false;
                    currentBook = parsed;
                    currentBookUri = uri;
                    titleView.setText(parsed.getTitle());
                    metaView.setText(parsed.getAuthor() + " · " + parsed.getChapters().size() + " 章");
                    updateCover(parsed.getCoverImage());
                    if (saveUri) {
                        progressStore.saveBookUri(uri.toString());
                    }
                    progressStore.upsertBook(uri.toString(), parsed.getTitle(), parsed.getAuthor(), parsed.getChapters().size(), chapterIndex, scrollY, calculatePercent(chapterIndex));
                    int safeChapter = Math.max(0, Math.min(chapterIndex, parsed.getChapters().size() - 1));
                    showChapter(safeChapter, scrollY);
                    statusView.setText("已载入 " + parsed.getChapters().size() + " 个章节");
                    setReaderEnabled(true);
                });
            } catch (Exception error) {
                runOnUiThread(() -> {
                    setReaderEnabled(true);
                    progressStore.clearBook();
                    statusView.setText("书籍解析失败");
                    Toast.makeText(this, error.getMessage(), Toast.LENGTH_LONG).show();
                });
            }
        });
    }

    private void updateCover(byte[] coverImage) {
        if (coverImage == null || coverImage.length == 0) {
            coverView.setImageBitmap(null);
            coverView.setBackgroundColor(nightMode ? 0xFF303030 : 0xFFE0E0E0);
            return;
        }
        Bitmap bitmap = BitmapFactory.decodeByteArray(coverImage, 0, coverImage.length);
        coverView.setImageBitmap(bitmap);
    }

    private void showChapter(int chapterIndex, int scrollY) {
        showChapterInternal(chapterIndex, scrollY, true);
    }

    private void showChapterInternal(int chapterIndex, int scrollY, boolean stopPlayback) {
        if (currentBook == null || currentBook.isEmpty()) {
            return;
        }
        showingHome = false;
        if (stopPlayback) {
            stopSpeaking();
        }
        int maxIndex = currentBook.getChapters().size() - 1;
        currentChapterIndex = Math.max(0, Math.min(chapterIndex, maxIndex));
        EpubChapter chapter = currentBook.getChapters().get(currentChapterIndex);
        chapterView.setText((currentChapterIndex + 1) + "/" + currentBook.getChapters().size() + "  " + chapter.getTitle());
        contentView.setText(chapter.getText());
        scrollView.post(() -> {
            scrollView.scrollTo(0, Math.max(0, scrollY));
            saveCurrentProgress();
        });
        updateNavigationState();
    }

    private void registerPlaybackReceiver() {
        IntentFilter filter = new IntentFilter(ReaderPlaybackService.ACTION_PROGRESS);
        if (Build.VERSION.SDK_INT >= 33) {
            registerReceiver(playbackReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(playbackReceiver, filter);
        }
    }

    private void handlePlaybackProgress(Intent intent) {
        int chapterIndex = intent.getIntExtra(ReaderPlaybackService.EXTRA_CHAPTER_INDEX, currentChapterIndex);
        int chunkIndex = intent.getIntExtra(ReaderPlaybackService.EXTRA_CHUNK_INDEX, 0);
        int chunkTotal = Math.max(1, intent.getIntExtra(ReaderPlaybackService.EXTRA_CHUNK_TOTAL, 1));
        boolean playing = intent.getBooleanExtra(ReaderPlaybackService.EXTRA_IS_PLAYING, false);
        boolean active = intent.getBooleanExtra(ReaderPlaybackService.EXTRA_IS_ACTIVE, playing);
        speakingActive = active;
        speakingPaused = intent.getBooleanExtra(ReaderPlaybackService.EXTRA_IS_PAUSED, active && !playing);
        if (currentBook != null && chapterIndex != currentChapterIndex) {
            showChapterInternal(chapterIndex, 0, false);
        }
        autoScrollForSpeech(chunkIndex, chunkTotal);
        highlightSpeechChunk(chunkIndex, chunkTotal);
        statusView.setText(playing ? "后台听书中" : (speakingPaused ? "听书已暂停" : "听书已停止"));
        updateNavigationState();
    }

    private void highlightSpeechChunk(int chunkIndex, int chunkTotal) {
        if (currentBook == null || contentView == null) {
            return;
        }
        CharSequence content = contentView.getText();
        if (content == null || content.length() == 0) {
            return;
        }
        int length = content.length();
        int start = Math.max(0, Math.min(length, Math.round(length * (chunkIndex / (float) Math.max(1, chunkTotal)))));
        int end = Math.max(start, Math.min(length, Math.round(length * ((chunkIndex + 1f) / Math.max(1, chunkTotal)))));
        SpannableString highlighted = new SpannableString(content.toString());
        highlighted.setSpan(new BackgroundColorSpan(nightMode ? 0xFF5D4A1F : 0xFFFFF59D), start, end, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        contentView.setText(highlighted);
    }

    private void autoScrollForSpeech(int chunkIndex, int chunkTotal) {
        if (scrollView == null || scrollView.getChildCount() == 0) {
            return;
        }
        scrollView.post(() -> {
            int maxScroll = Math.max(0, scrollView.getChildAt(0).getHeight() - scrollView.getHeight());
            int target = chunkTotal <= 1 ? 0 : Math.round(maxScroll * (chunkIndex / (float) chunkTotal));
            scrollView.smoothScrollTo(0, Math.max(0, target));
        });
    }

    private void startPlaybackService(String action) {
        if (ReaderPlaybackService.ACTION_PLAY.equals(action) && !hasBook()) {
            return;
        }
        saveCurrentProgress();
        Intent intent = new Intent(this, ReaderPlaybackService.class);
        intent.setAction(action);
        if (currentBookUri != null) {
            intent.putExtra(ReaderPlaybackService.EXTRA_BOOK_URI, currentBookUri.toString());
            intent.putExtra(ReaderPlaybackService.EXTRA_CHAPTER_INDEX, currentChapterIndex);
        }
        if (Build.VERSION.SDK_INT >= 26 && ReaderPlaybackService.ACTION_PLAY.equals(action)) {
            startForegroundService(intent);
        } else {
            startService(intent);
        }
    }

    private void showBookshelfOptions() {
        new AlertDialog.Builder(this)
                .setTitle("书架")
                .setItems(new String[]{"最近阅读排序", "书名排序", "作者排序", "进度排序", bookshelfOnlyUnread ? "显示全部" : "只看未读"}, (dialog, which) -> {
                    if (which == 4) {
                        bookshelfOnlyUnread = !bookshelfOnlyUnread;
                    } else {
                        bookshelfSort = which;
                    }
                    showBookshelf();
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private void showBookshelf() {
        final List<BookRecord> books = filterAndSortBooks(progressStore.getBooks());
        if (books.isEmpty()) {
            Toast.makeText(this, "书架为空或没有匹配书籍", Toast.LENGTH_SHORT).show();
            return;
        }
        String[] labels = new String[books.size()];
        for (int index = 0; index < books.size(); index++) {
            BookRecord book = books.get(index);
            labels[index] = book.title + "\n" + book.author + " · " + Math.round(book.percent) + "% · " + formatDate(book.updatedAt);
        }
        new AlertDialog.Builder(this)
                .setTitle("书架")
                .setItems(labels, (dialog, which) -> {
                    BookRecord book = books.get(which);
                    loadBook(Uri.parse(book.uri), true, book.chapterIndex, book.scrollY);
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private List<BookRecord> filterAndSortBooks(List<BookRecord> source) {
        List<BookRecord> books = new ArrayList<>();
        for (BookRecord book : source) {
            if (!bookshelfOnlyUnread || book.percent < 99f) {
                books.add(book);
            }
        }
        Comparator<BookRecord> comparator;
        if (bookshelfSort == SORT_TITLE) {
            comparator = (left, right) -> left.title.compareToIgnoreCase(right.title);
        } else if (bookshelfSort == SORT_AUTHOR) {
            comparator = (left, right) -> left.author.compareToIgnoreCase(right.author);
        } else if (bookshelfSort == SORT_PROGRESS) {
            comparator = (left, right) -> Float.compare(right.percent, left.percent);
        } else {
            comparator = (left, right) -> Long.compare(right.updatedAt, left.updatedAt);
        }
        Collections.sort(books, comparator);
        return books;
    }

    private void showToc() {
        if (!hasBook()) {
            return;
        }
        String[] chapters = new String[currentBook.getChapters().size()];
        for (int index = 0; index < chapters.length; index++) {
            chapters[index] = (index + 1) + ". " + currentBook.getChapters().get(index).getTitle();
        }
        new AlertDialog.Builder(this)
                .setTitle("目录")
                .setItems(chapters, (dialog, which) -> showChapter(which, 0))
                .setNegativeButton("关闭", null)
                .show();
    }

    private void showReadingSettings() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setPadding(dp(12), dp(8), dp(12), 0);
        TextView preview = new TextView(this);
        preview.setText(settingsSummary());
        panel.addView(preview);

        Button fontMinus = commandButton("字号 -", view -> {
            textSize = Math.max(14f, textSize - 1f);
            saveAndApplySettings();
            preview.setText(settingsSummary());
        });
        Button fontPlus = commandButton("字号 +", view -> {
            textSize = Math.min(28f, textSize + 1f);
            saveAndApplySettings();
            preview.setText(settingsSummary());
        });
        Button lineButton = commandButton("行距", view -> {
            lineSpacing = lineSpacing >= 1.6f ? 1.05f : lineSpacing + 0.1f;
            saveAndApplySettings();
            preview.setText(settingsSummary());
        });
        Button nightButton = commandButton("主题", view -> {
            cycleTheme();
            saveAndApplySettings();
            preview.setText(settingsSummary());
        });
        Button speedButton = commandButton("语速", view -> {
            speechRate = speechRate >= 1.6f ? 0.8f : speechRate + 0.1f;
            saveSpeechSettings();
            preview.setText(settingsSummary());
        });
        Button pitchButton = commandButton("音调", view -> {
            speechPitch = speechPitch >= 1.4f ? 0.8f : speechPitch + 0.1f;
            saveSpeechSettings();
            preview.setText(settingsSummary());
        });
        panel.addView(fontMinus);
        panel.addView(fontPlus);
        panel.addView(lineButton);
        panel.addView(nightButton);
        panel.addView(speedButton);
        panel.addView(pitchButton);

        new AlertDialog.Builder(this)
                .setTitle("阅读与听书设置")
                .setView(panel)
                .setPositiveButton("完成", null)
                .show();
    }

    private String settingsSummary() {
        return "字号 " + Math.round(textSize)
                + " · 行距 " + String.format(Locale.US, "%.2f", lineSpacing)
                + " · " + themeLabel()
                + "\n语速 " + String.format(Locale.US, "%.1f", speechRate)
                + " · 音调 " + String.format(Locale.US, "%.1f", speechPitch);
    }

    private void cycleTheme() {
        if ("paper".equals(themeName)) {
            themeName = "night";
        } else if ("night".equals(themeName)) {
            themeName = "eye";
        } else if ("eye".equals(themeName)) {
            themeName = "ink";
        } else {
            themeName = "paper";
        }
        nightMode = "night".equals(themeName);
    }

    private String themeLabel() {
        if ("night".equals(themeName)) {
            return "夜间";
        }
        if ("eye".equals(themeName)) {
            return "护眼";
        }
        if ("ink".equals(themeName)) {
            return "墨水屏";
        }
        return "纸张";
    }

    private void showSearchDialog() {
        if (!hasBook()) {
            return;
        }
        EditText input = new EditText(this);
        input.setHint("输入关键词");
        input.setSingleLine(true);
        new AlertDialog.Builder(this)
                .setTitle("全文搜索")
                .setView(input)
                .setPositiveButton("搜索", (dialog, which) -> showSearchResults(input.getText().toString().trim()))
                .setNegativeButton("取消", null)
                .show();
    }

    private void showSearchResults(String query) {
        if (query.isEmpty()) {
            return;
        }
        List<Integer> chapterIndexes = new ArrayList<>();
        List<Integer> offsets = new ArrayList<>();
        List<String> labels = new ArrayList<>();
        String lowerQuery = query.toLowerCase(Locale.getDefault());
        for (int chapterIndex = 0; chapterIndex < currentBook.getChapters().size(); chapterIndex++) {
            EpubChapter chapter = currentBook.getChapters().get(chapterIndex);
            String lowerText = chapter.getText().toLowerCase(Locale.getDefault());
            int offset = lowerText.indexOf(lowerQuery);
            while (offset >= 0 && labels.size() < 50) {
                int start = Math.max(0, offset - 28);
                int end = Math.min(chapter.getText().length(), offset + query.length() + 42);
                labels.add(chapter.getTitle() + "\n..." + chapter.getText().substring(start, end).replace('\n', ' ') + "...");
                chapterIndexes.add(chapterIndex);
                offsets.add(offset);
                offset = lowerText.indexOf(lowerQuery, offset + query.length());
            }
        }
        if (labels.isEmpty()) {
            Toast.makeText(this, "没有找到匹配内容", Toast.LENGTH_SHORT).show();
            return;
        }
        new AlertDialog.Builder(this)
                .setTitle("搜索结果")
                .setItems(labels.toArray(new String[0]), (dialog, which) -> {
                    showChapter(chapterIndexes.get(which), 0);
                    contentView.post(() -> {
                        int line = contentView.getLayout() == null ? 0 : contentView.getLayout().getLineForOffset(offsets.get(which));
                        scrollView.scrollTo(0, Math.max(0, line * contentView.getLineHeight() - dp(40)));
                    });
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private void showBookmarkMenu() {
        if (!hasBook()) {
            return;
        }
        new AlertDialog.Builder(this)
                .setTitle("书签与笔记")
                .setItems(new String[]{"添加书签/笔记", "查看书签", "导出笔记"}, (dialog, which) -> {
                    if (which == 0) {
                        addBookmark();
                    } else if (which == 1) {
                        showBookmarks();
                    } else {
                        exportBookmarks();
                    }
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private void addBookmark() {
        EditText input = new EditText(this);
        input.setHint("笔记内容，可留空");
        input.setMinLines(2);
        input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE);
        new AlertDialog.Builder(this)
                .setTitle("添加书签")
                .setView(input)
                .setPositiveButton("保存", (dialog, which) -> {
                    EpubChapter chapter = currentBook.getChapters().get(currentChapterIndex);
                    progressStore.addBookmark(currentBookUri.toString(), currentBook.getTitle(), currentChapterIndex, chapter.getTitle(), scrollView.getScrollY(), input.getText().toString());
                    Toast.makeText(this, "书签已保存", Toast.LENGTH_SHORT).show();
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private void showBookmarks() {
        List<Bookmark> bookmarks = progressStore.getBookmarks(currentBookUri.toString());
        if (bookmarks.isEmpty()) {
            Toast.makeText(this, "当前书籍暂无书签", Toast.LENGTH_SHORT).show();
            return;
        }
        String[] labels = new String[bookmarks.size()];
        for (int index = 0; index < bookmarks.size(); index++) {
            Bookmark bookmark = bookmarks.get(index);
            labels[index] = (bookmark.chapterIndex + 1) + ". " + bookmark.chapterTitle
                    + "\n" + (bookmark.note.isEmpty() ? "无笔记" : bookmark.note)
                    + " · " + formatDate(bookmark.createdAt);
        }
        new AlertDialog.Builder(this)
                .setTitle("书签")
                .setItems(labels, (dialog, which) -> {
                    Bookmark bookmark = bookmarks.get(which);
                    showChapter(bookmark.chapterIndex, bookmark.scrollY);
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private void exportBookmarks() {
        if (!hasBook()) {
            return;
        }
        List<Bookmark> bookmarks = progressStore.getBookmarks(currentBookUri.toString());
        if (bookmarks.isEmpty()) {
            Toast.makeText(this, "当前书籍暂无可导出的笔记", Toast.LENGTH_SHORT).show();
            return;
        }
        StringBuilder builder = new StringBuilder();
        builder.append("# ").append(currentBook.getTitle()).append(" 笔记\n\n");
        for (Bookmark bookmark : bookmarks) {
            builder.append("## 第 ").append(bookmark.chapterIndex + 1).append(" 章：").append(bookmark.chapterTitle).append("\n")
                    .append("- 时间：").append(formatDate(bookmark.createdAt)).append("\n")
                    .append("- 位置：").append(bookmark.scrollY).append("\n")
                    .append("- 笔记：").append(bookmark.note.isEmpty() ? "无" : bookmark.note).append("\n\n");
        }
        Intent intent = new Intent(Intent.ACTION_SEND);
        intent.setType("text/markdown");
        intent.putExtra(Intent.EXTRA_SUBJECT, currentBook.getTitle() + " 笔记");
        intent.putExtra(Intent.EXTRA_TEXT, builder.toString());
        startActivity(Intent.createChooser(intent, "导出笔记"));
    }

    private void speakCurrentChapter(boolean resume) {
        if (!hasBook()) {
            return;
        }
        if (!ttsReady) {
            Toast.makeText(this, "语音引擎尚未就绪", Toast.LENGTH_SHORT).show();
            return;
        }
        if (!resume) {
            speechChunks = splitForTts(currentBook.getChapters().get(currentChapterIndex).getText());
            speechChunkIndex = 0;
        }
        selectVoice(progressStore.getVoiceGender());
        textToSpeech.setSpeechRate(speechRate);
        textToSpeech.setPitch(speechPitch);
        textToSpeech.stop();
        speakingPaused = false;
        speakingActive = true;
        speakRemainingChunks();
        statusView.setText("正在听书");
        updateNavigationState();
    }

    private void speakRemainingChunks() {
        for (int index = speechChunkIndex; index < speechChunks.size(); index++) {
            int queueMode = index == speechChunkIndex ? TextToSpeech.QUEUE_FLUSH : TextToSpeech.QUEUE_ADD;
            textToSpeech.speak(speechChunks.get(index), queueMode, null, "chapter-" + currentChapterIndex + "-" + index);
        }
    }

    private void togglePauseResume() {
        if (!ttsReady || !speakingActive) {
            return;
        }
        if (speakingPaused) {
            speakCurrentChapter(true);
        } else {
            textToSpeech.stop();
            speakingPaused = true;
            statusView.setText("已暂停听书");
            updateNavigationState();
        }
    }

    private void stopSpeaking() {
        if (textToSpeech != null) {
            textToSpeech.stop();
        }
        stopPlaybackServiceOnly();
        speakingActive = false;
        speakingPaused = false;
        if (statusView != null) {
            statusView.setText("已停止播放");
        }
        updateNavigationState();
    }

    private void stopPlaybackServiceOnly() {
        Intent intent = new Intent(this, ReaderPlaybackService.class);
        intent.setAction(ReaderPlaybackService.ACTION_STOP);
        try {
            startService(intent);
        } catch (RuntimeException ignored) {
        }
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
            if (!voice.getLocale().getLanguage().equals(Locale.getDefault().getLanguage())) {
                if (fallback == null) {
                    fallback = voice;
                }
                continue;
            }
            if (fallback == null) {
                fallback = voice;
            }
            if (GENDER_MALE.equals(gender) && looksMale(voice)) {
                textToSpeech.setVoice(voice);
                return;
            }
            if (GENDER_FEMALE.equals(gender) && looksFemale(voice)) {
                textToSpeech.setVoice(voice);
                return;
            }
        }
        if (fallback != null) {
            textToSpeech.setVoice(fallback);
        }
    }

    private boolean looksMale(Voice voice) {
        String value = voiceDescriptor(voice);
        return (value.contains("male") && !value.contains("female")) || value.contains("man") || value.contains("男");
    }

    private boolean looksFemale(Voice voice) {
        String value = voiceDescriptor(voice);
        return value.contains("female") || value.contains("woman") || value.contains("女");
    }

    private String voiceDescriptor(Voice voice) {
        StringBuilder builder = new StringBuilder(voice.getName().toLowerCase(Locale.US));
        if (voice.getFeatures() != null) {
            for (String feature : voice.getFeatures()) {
                builder.append(' ').append(feature.toLowerCase(Locale.US));
            }
        }
        return builder.toString();
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

    private void saveCurrentProgress() {
        if (currentBook != null && currentBookUri != null) {
            float percent = calculatePercent(currentChapterIndex);
            progressStore.saveBookUri(currentBookUri.toString());
            progressStore.upsertBook(currentBookUri.toString(), currentBook.getTitle(), currentBook.getAuthor(), currentBook.getChapters().size(), currentChapterIndex, scrollView == null ? 0 : scrollView.getScrollY(), percent);
        }
    }

    private float calculatePercent(int chapterIndex) {
        if (currentBook == null || currentBook.getChapters().isEmpty()) {
            return 0f;
        }
        float chapterBase = chapterIndex * 100f / currentBook.getChapters().size();
        float chapterPart = 0f;
        if (scrollView != null && scrollView.getChildCount() > 0) {
            int maxScroll = Math.max(1, scrollView.getChildAt(0).getHeight() - scrollView.getHeight());
            chapterPart = Math.min(1f, Math.max(0f, scrollView.getScrollY() / (float) maxScroll)) * (100f / currentBook.getChapters().size());
        }
        return Math.min(100f, chapterBase + chapterPart);
    }

    private void saveAndApplySettings() {
        progressStore.saveReadingSettings(textSize, lineSpacing, nightMode, themeName);
        applyReadingTheme();
    }

    private void saveSpeechSettings() {
        progressStore.saveSpeechSettings(speechRate, speechPitch);
        if (textToSpeech != null) {
            textToSpeech.setSpeechRate(speechRate);
            textToSpeech.setPitch(speechPitch);
        }
    }

    private void applyReadingTheme() {
        int background;
        int primary;
        int secondary;
        if ("night".equals(themeName)) {
            background = 0xFF121212;
            primary = 0xFFEDEDED;
            secondary = 0xFFBDBDBD;
        } else if ("eye".equals(themeName)) {
            background = 0xFFEAF4E2;
            primary = 0xFF1F2A1F;
            secondary = 0xFF52624D;
        } else if ("ink".equals(themeName)) {
            background = 0xFFF8F8F4;
            primary = Color.BLACK;
            secondary = 0xFF555555;
        } else {
            background = 0xFFFAFAF7;
            primary = 0xFF1F1F1F;
            secondary = 0xFF666666;
        }
        root.setBackgroundColor(background);
        titleView.setTextColor(primary);
        metaView.setTextColor(secondary);
        chapterView.setTextColor(secondary);
        contentView.setTextColor(primary);
        statusView.setTextColor(secondary);
        contentView.setTextSize(textSize);
        contentView.setLineSpacing(dp(4), lineSpacing);
        if (currentBook == null || currentBook.getCoverImage() == null) {
            coverView.setBackgroundColor("night".equals(themeName) ? 0xFF303030 : 0xFFE0E0E0);
        }
    }

    private void applyResponsiveLayout() {
        int screenWidthDp = getResources().getConfiguration().screenWidthDp;
        int horizontalPadding = screenWidthDp >= 700 ? 48 : 12;
        int verticalPadding = screenWidthDp >= 700 ? 18 : 10;
        root.setPadding(dp(horizontalPadding), dp(verticalPadding), dp(horizontalPadding), dp(verticalPadding));
        if (screenWidthDp >= 700) {
            contentView.setMaxWidth(dp(760));
            contentView.setGravity(Gravity.CENTER_HORIZONTAL);
        }
    }

    private void setReaderEnabled(boolean enabled) {
        previousButton.setEnabled(enabled);
        nextButton.setEnabled(enabled);
        listenButton.setEnabled(enabled);
        pauseButton.setEnabled(enabled);
        stopButton.setEnabled(enabled);
        if (enabled) {
            updateNavigationState();
        }
    }

    private void updateNavigationState() {
        boolean hasBook = currentBook != null && !currentBook.isEmpty();
        previousButton.setEnabled(hasBook && currentChapterIndex > 0);
        nextButton.setEnabled(hasBook && currentChapterIndex < currentBook.getChapters().size() - 1);
        listenButton.setEnabled(hasBook && ttsReady);
        pauseButton.setEnabled(ttsReady && speakingActive);
        pauseButton.setText(speakingPaused ? "继续" : "暂停");
        stopButton.setEnabled(ttsReady && speakingActive);
    }

    private boolean hasBook() {
        if (currentBook == null || currentBook.isEmpty()) {
            Toast.makeText(this, "请先导入 EPUB/TXT", Toast.LENGTH_SHORT).show();
            return false;
        }
        return true;
    }

    private String formatDate(long millis) {
        if (millis <= 0) {
            return "未记录";
        }
        return new SimpleDateFormat("MM-dd HH:mm", Locale.getDefault()).format(new Date(millis));
    }

    private int dp(int value) {
        return (int) (value * getResources().getDisplayMetrics().density + 0.5f);
    }
}