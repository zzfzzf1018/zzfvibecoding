package com.listeenb.reader;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.Voice;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import com.listeenb.reader.data.ProgressStore;
import com.listeenb.reader.epub.EpubBook;
import com.listeenb.reader.epub.EpubChapter;
import com.listeenb.reader.epub.EpubParser;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends Activity implements TextToSpeech.OnInitListener {
    private static final int REQUEST_OPEN_EPUB = 1001;
    private static final String GENDER_MALE = "male";
    private static final String GENDER_FEMALE = "female";
    private static final int TTS_CHUNK_SIZE = 3200;

    private final EpubParser epubParser = new EpubParser();
    private final ExecutorService executorService = Executors.newSingleThreadExecutor();

    private ProgressStore progressStore;
    private TextToSpeech textToSpeech;
    private boolean ttsReady;
    private EpubBook currentBook;
    private Uri currentBookUri;
    private int currentChapterIndex;

    private TextView titleView;
    private TextView chapterView;
    private TextView contentView;
    private TextView statusView;
    private ScrollView scrollView;
    private Button previousButton;
    private Button nextButton;
    private Button listenButton;
    private Button stopButton;
    private RadioGroup genderGroup;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        progressStore = new ProgressStore(this);
        textToSpeech = new TextToSpeech(this, this);
        buildUi();
        restoreVoicePreference();
        restoreLastBook();
    }

    @Override
    protected void onDestroy() {
        saveCurrentProgress();
        executorService.shutdownNow();
        if (textToSpeech != null) {
            textToSpeech.stop();
            textToSpeech.shutdown();
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
            statusView.setText("语音已就绪");
        } else {
            statusView.setText("当前设备没有可用的语音引擎");
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_OPEN_EPUB || resultCode != RESULT_OK || data == null || data.getData() == null) {
            return;
        }
        Uri uri = data.getData();
        int flags = data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        try {
            getContentResolver().takePersistableUriPermission(uri, flags & Intent.FLAG_GRANT_READ_URI_PERMISSION);
        } catch (RuntimeException ignored) {
        }
        loadBook(uri, true, 0, 0);
    }

    private void buildUi() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(16), dp(12), dp(16), dp(12));
        root.setBackgroundColor(0xFFFAFAF7);

        LinearLayout topBar = new LinearLayout(this);
        topBar.setGravity(Gravity.CENTER_VERTICAL);
        topBar.setOrientation(LinearLayout.HORIZONTAL);

        titleView = new TextView(this);
        titleView.setText("ListeenB");
        titleView.setTextColor(0xFF202124);
        titleView.setTextSize(20);
        titleView.setSingleLine(true);
        titleView.setEllipsize(TextUtils.TruncateAt.END);
        topBar.addView(titleView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        Button importButton = new Button(this);
        importButton.setText("导入 EPUB");
        importButton.setOnClickListener(view -> openDocumentPicker());
        topBar.addView(importButton);
        root.addView(topBar);

        chapterView = new TextView(this);
        chapterView.setText("请选择一本 EPUB 书籍");
        chapterView.setTextColor(0xFF4D4D4D);
        chapterView.setTextSize(15);
        chapterView.setPadding(0, dp(8), 0, dp(8));
        root.addView(chapterView);

        scrollView = new ScrollView(this);
        contentView = new TextView(this);
        contentView.setText("点击右上角导入本地 EPUB 文件。\n\n导入后可阅读正文，也可以选择男声或女声开始听书。阅读位置会自动保存。");
        contentView.setTextColor(0xFF1F1F1F);
        contentView.setTextSize(18);
        contentView.setLineSpacing(dp(4), 1.15f);
        contentView.setPadding(0, dp(8), 0, dp(24));
        scrollView.addView(contentView);
        scrollView.setOnScrollChangeListener((view, scrollX, scrollY, oldScrollX, oldScrollY) -> saveCurrentProgress());
        root.addView(scrollView, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));

        genderGroup = new RadioGroup(this);
        genderGroup.setOrientation(RadioGroup.HORIZONTAL);
        RadioButton femaleButton = new RadioButton(this);
        femaleButton.setText("女声");
        femaleButton.setId(View.generateViewId());
        femaleButton.setTag(GENDER_FEMALE);
        RadioButton maleButton = new RadioButton(this);
        maleButton.setText("男声");
        maleButton.setId(View.generateViewId());
        maleButton.setTag(GENDER_MALE);
        genderGroup.addView(femaleButton);
        genderGroup.addView(maleButton);
        genderGroup.setOnCheckedChangeListener((group, checkedId) -> {
            View checked = group.findViewById(checkedId);
            if (checked != null && checked.getTag() != null) {
                progressStore.saveVoiceGender(checked.getTag().toString());
            }
        });
        root.addView(genderGroup);

        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.HORIZONTAL);
        controls.setGravity(Gravity.CENTER_VERTICAL);

        previousButton = new Button(this);
        previousButton.setText("上一章");
        previousButton.setOnClickListener(view -> showChapter(currentChapterIndex - 1, 0));
        controls.addView(previousButton, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        listenButton = new Button(this);
        listenButton.setText("听书");
        listenButton.setOnClickListener(view -> speakCurrentChapter());
        controls.addView(listenButton, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        stopButton = new Button(this);
        stopButton.setText("停止");
        stopButton.setOnClickListener(view -> stopSpeaking());
        controls.addView(stopButton, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        nextButton = new Button(this);
        nextButton.setText("下一章");
        nextButton.setOnClickListener(view -> showChapter(currentChapterIndex + 1, 0));
        controls.addView(nextButton, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        root.addView(controls);

        statusView = new TextView(this);
        statusView.setText("准备就绪");
        statusView.setTextColor(0xFF666666);
        statusView.setTextSize(13);
        statusView.setPadding(0, dp(6), 0, 0);
        root.addView(statusView);

        setContentView(root);
        updateNavigationState();
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
        if (uriText == null) {
            return;
        }
        loadBook(Uri.parse(uriText), false, progressStore.getChapterIndex(), progressStore.getScrollY());
    }

    private void openDocumentPicker() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("application/epub+zip");
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

    private void loadBook(Uri uri, boolean saveUri, int chapterIndex, int scrollY) {
        statusView.setText("正在解析 EPUB...");
        setReaderEnabled(false);
        executorService.execute(() -> {
            try {
                EpubBook parsed = epubParser.parse(getContentResolver(), uri);
                runOnUiThread(() -> {
                    currentBook = parsed;
                    currentBookUri = uri;
                    titleView.setText(parsed.getTitle());
                    if (saveUri) {
                        progressStore.saveBookUri(uri.toString());
                    }
                    int safeChapter = Math.max(0, Math.min(chapterIndex, parsed.getChapters().size() - 1));
                    showChapter(safeChapter, scrollY);
                    statusView.setText("已载入 " + parsed.getChapters().size() + " 个章节");
                    setReaderEnabled(true);
                });
            } catch (Exception error) {
                runOnUiThread(() -> {
                    setReaderEnabled(true);
                    progressStore.clearBook();
                    statusView.setText("EPUB 解析失败");
                    Toast.makeText(this, error.getMessage(), Toast.LENGTH_LONG).show();
                });
            }
        });
    }

    private void showChapter(int chapterIndex, int scrollY) {
        if (currentBook == null || currentBook.isEmpty()) {
            return;
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

    private void speakCurrentChapter() {
        if (currentBook == null || currentBook.isEmpty()) {
            Toast.makeText(this, "请先导入 EPUB", Toast.LENGTH_SHORT).show();
            return;
        }
        if (!ttsReady) {
            Toast.makeText(this, "语音引擎尚未就绪", Toast.LENGTH_SHORT).show();
            return;
        }
        selectVoice(progressStore.getVoiceGender());
        textToSpeech.stop();
        List<String> chunks = splitForTts(currentBook.getChapters().get(currentChapterIndex).getText());
        for (int index = 0; index < chunks.size(); index++) {
            int queueMode = index == 0 ? TextToSpeech.QUEUE_FLUSH : TextToSpeech.QUEUE_ADD;
            textToSpeech.speak(chunks.get(index), queueMode, null, "chapter-" + currentChapterIndex + "-" + index);
        }
        statusView.setText("正在听书");
    }

    private void stopSpeaking() {
        if (textToSpeech != null) {
            textToSpeech.stop();
        }
        statusView.setText("已停止播放");
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
                statusView.setText("已选择男声");
                return;
            }
            if (GENDER_FEMALE.equals(gender) && looksFemale(voice)) {
                textToSpeech.setVoice(voice);
                statusView.setText("已选择女声");
                return;
            }
        }
        if (fallback != null) {
            textToSpeech.setVoice(fallback);
            statusView.setText("未找到匹配声线，已使用默认语音");
        }
    }

    private boolean looksMale(Voice voice) {
        String value = voiceDescriptor(voice);
        return (value.contains("male") && !value.contains("female"))
                || value.contains("man")
                || value.contains("男");
    }

    private boolean looksFemale(Voice voice) {
        String value = voiceDescriptor(voice);
        return value.contains("female")
                || value.contains("woman")
                || value.contains("女");
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
            progressStore.saveBookUri(currentBookUri.toString());
            progressStore.saveProgress(currentChapterIndex, scrollView == null ? 0 : scrollView.getScrollY());
        }
    }

    private void setReaderEnabled(boolean enabled) {
        previousButton.setEnabled(enabled);
        nextButton.setEnabled(enabled);
        listenButton.setEnabled(enabled);
        stopButton.setEnabled(enabled);
        if (enabled) {
            updateNavigationState();
        }
    }

    private void updateNavigationState() {
        boolean hasBook = currentBook != null && !currentBook.isEmpty();
        previousButton.setEnabled(hasBook && currentChapterIndex > 0);
        nextButton.setEnabled(hasBook && currentChapterIndex < currentBook.getChapters().size() - 1);
        listenButton.setEnabled(hasBook);
        stopButton.setEnabled(ttsReady);
    }

    private int dp(int value) {
        return (int) (value * getResources().getDisplayMetrics().density + 0.5f);
    }
}