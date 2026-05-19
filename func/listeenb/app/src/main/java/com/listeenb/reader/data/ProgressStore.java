package com.listeenb.reader.data;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class ProgressStore {
    private static final String PREFS = "reader_progress";
    private static final String KEY_BOOK_URI = "book_uri";
    private static final String KEY_VOICE_GENDER = "voice_gender";
    private static final String KEY_BOOKS = "books";
    private static final String KEY_BOOKMARKS = "bookmarks_";
    private static final String KEY_TEXT_SIZE = "text_size";
    private static final String KEY_LINE_SPACING = "line_spacing";
    private static final String KEY_NIGHT_MODE = "night_mode";
    private static final String KEY_SPEECH_RATE = "speech_rate";
    private static final String KEY_SPEECH_PITCH = "speech_pitch";
    private static final String KEY_IMPORT_FOLDER_URI = "import_folder_uri";
    private static final String KEY_THEME = "theme";

    private final SharedPreferences preferences;

    public ProgressStore(Context context) {
        preferences = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    public void saveBookUri(String uri) {
        preferences.edit().putString(KEY_BOOK_URI, uri).apply();
    }

    public String getBookUri() {
        return preferences.getString(KEY_BOOK_URI, null);
    }

    public void saveProgress(int chapterIndex, int scrollY) {
        String uri = getBookUri();
        if (uri != null) {
            updateBookProgress(uri, chapterIndex, scrollY, 0f);
        }
    }

    public int getChapterIndex() {
        BookRecord record = findBook(getBookUri());
        return record == null ? 0 : record.chapterIndex;
    }

    public int getScrollY() {
        BookRecord record = findBook(getBookUri());
        return record == null ? 0 : record.scrollY;
    }

    public void saveVoiceGender(String gender) {
        preferences.edit().putString(KEY_VOICE_GENDER, gender).apply();
    }

    public String getVoiceGender() {
        return preferences.getString(KEY_VOICE_GENDER, "female");
    }

    public float getTextSize() {
        return preferences.getFloat(KEY_TEXT_SIZE, 18f);
    }

    public float getLineSpacing() {
        return preferences.getFloat(KEY_LINE_SPACING, 1.15f);
    }

    public boolean isNightMode() {
        return preferences.getBoolean(KEY_NIGHT_MODE, false);
    }

    public String getTheme() {
        return preferences.getString(KEY_THEME, isNightMode() ? "night" : "paper");
    }

    public void saveReadingSettings(float textSize, float lineSpacing, boolean nightMode) {
        saveReadingSettings(textSize, lineSpacing, nightMode, nightMode ? "night" : "paper");
    }

    public void saveReadingSettings(float textSize, float lineSpacing, boolean nightMode, String theme) {
        preferences.edit()
                .putFloat(KEY_TEXT_SIZE, textSize)
                .putFloat(KEY_LINE_SPACING, lineSpacing)
                .putBoolean(KEY_NIGHT_MODE, nightMode)
                .putString(KEY_THEME, theme)
                .apply();
    }

    public float getSpeechRate() {
        return preferences.getFloat(KEY_SPEECH_RATE, 1f);
    }

    public float getSpeechPitch() {
        return preferences.getFloat(KEY_SPEECH_PITCH, 1f);
    }

    public void saveSpeechSettings(float rate, float pitch) {
        preferences.edit()
                .putFloat(KEY_SPEECH_RATE, rate)
                .putFloat(KEY_SPEECH_PITCH, pitch)
                .apply();
    }

    public void saveImportFolderUri(String uri) {
        preferences.edit().putString(KEY_IMPORT_FOLDER_URI, uri).apply();
    }

    public String getImportFolderUri() {
        return preferences.getString(KEY_IMPORT_FOLDER_URI, null);
    }

    public void upsertBook(String uri, String title, String author, int chapterCount, int chapterIndex, int scrollY, float percent) {
        upsertBook(uri, title, author, chapterCount, chapterIndex, scrollY, percent, true);
    }

    public void upsertScannedBook(String uri, String title, String author, int chapterCount) {
        upsertBook(uri, title, author, chapterCount, 0, 0, 0f, false);
    }

    private void upsertBook(String uri, String title, String author, int chapterCount, int chapterIndex, int scrollY, float percent, boolean makeCurrent) {
        List<BookRecord> books = getBooks();
        BookRecord updated = new BookRecord(uri, title, author, chapterCount, chapterIndex, scrollY, percent, System.currentTimeMillis());
        boolean replaced = false;
        for (int index = 0; index < books.size(); index++) {
            if (books.get(index).uri.equals(uri)) {
                books.set(index, updated);
                replaced = true;
                break;
            }
        }
        if (!replaced) {
            books.add(0, updated);
        }
        saveBooks(books);
        if (makeCurrent) {
            saveBookUri(uri);
        }
    }

    public void updateBookProgress(String uri, int chapterIndex, int scrollY, float percent) {
        if (uri == null) {
            return;
        }
        List<BookRecord> books = getBooks();
        for (int index = 0; index < books.size(); index++) {
            BookRecord old = books.get(index);
            if (old.uri.equals(uri)) {
                books.set(index, new BookRecord(old.uri, old.title, old.author, old.chapterCount, chapterIndex, scrollY, percent, System.currentTimeMillis()));
                saveBooks(books);
                return;
            }
        }
    }

    public List<BookRecord> getBooks() {
        String raw = preferences.getString(KEY_BOOKS, "[]");
        List<BookRecord> books = new ArrayList<>();
        try {
            JSONArray array = new JSONArray(raw);
            for (int index = 0; index < array.length(); index++) {
                books.add(BookRecord.fromJson(array.getJSONObject(index)));
            }
        } catch (Exception ignored) {
        }
        Collections.sort(books, (left, right) -> Long.compare(right.updatedAt, left.updatedAt));
        return books;
    }

    public BookRecord findBook(String uri) {
        if (uri == null) {
            return null;
        }
        for (BookRecord record : getBooks()) {
            if (uri.equals(record.uri)) {
                return record;
            }
        }
        return null;
    }

    public void removeBook(String uri) {
        if (uri == null) {
            return;
        }
        List<BookRecord> books = getBooks();
        for (int index = books.size() - 1; index >= 0; index--) {
            if (uri.equals(books.get(index).uri)) {
                books.remove(index);
            }
        }
        saveBooks(books);
        preferences.edit()
                .remove(KEY_BOOKMARKS + safeKey(uri))
                .apply();
        if (uri.equals(getBookUri())) {
            clearBook();
        }
    }

    public void addBookmark(String bookUri, String bookTitle, int chapterIndex, String chapterTitle, int scrollY, String note) {
        List<Bookmark> bookmarks = getBookmarks(bookUri);
        bookmarks.add(0, new Bookmark(bookUri, bookTitle, chapterIndex, chapterTitle, scrollY, note, System.currentTimeMillis()));
        saveBookmarks(bookUri, bookmarks);
    }

    public List<Bookmark> getBookmarks(String bookUri) {
        String raw = preferences.getString(KEY_BOOKMARKS + safeKey(bookUri), "[]");
        List<Bookmark> bookmarks = new ArrayList<>();
        try {
            JSONArray array = new JSONArray(raw);
            for (int index = 0; index < array.length(); index++) {
                bookmarks.add(Bookmark.fromJson(array.getJSONObject(index)));
            }
        } catch (Exception ignored) {
        }
        return bookmarks;
    }

    public void clearBook() {
        preferences.edit().remove(KEY_BOOK_URI).apply();
    }

    private void saveBooks(List<BookRecord> books) {
        JSONArray array = new JSONArray();
        for (BookRecord record : books) {
            array.put(record.toJson());
        }
        preferences.edit().putString(KEY_BOOKS, array.toString()).apply();
    }

    private void saveBookmarks(String bookUri, List<Bookmark> bookmarks) {
        JSONArray array = new JSONArray();
        for (Bookmark bookmark : bookmarks) {
            array.put(bookmark.toJson());
        }
        preferences.edit().putString(KEY_BOOKMARKS + safeKey(bookUri), array.toString()).apply();
    }

    private String safeKey(String value) {
        return value == null ? "none" : Integer.toHexString(value.hashCode());
    }

    public static class BookRecord {
        public final String uri;
        public final String title;
        public final String author;
        public final int chapterCount;
        public final int chapterIndex;
        public final int scrollY;
        public final float percent;
        public final long updatedAt;

        public BookRecord(String uri, String title, String author, int chapterCount, int chapterIndex, int scrollY, float percent, long updatedAt) {
            this.uri = uri;
            this.title = title == null ? "未命名书籍" : title;
            this.author = author == null ? "未知作者" : author;
            this.chapterCount = chapterCount;
            this.chapterIndex = chapterIndex;
            this.scrollY = scrollY;
            this.percent = percent;
            this.updatedAt = updatedAt;
        }

        JSONObject toJson() {
            JSONObject json = new JSONObject();
            try {
                json.put("uri", uri);
                json.put("title", title);
                json.put("author", author);
                json.put("chapterCount", chapterCount);
                json.put("chapterIndex", chapterIndex);
                json.put("scrollY", scrollY);
                json.put("percent", percent);
                json.put("updatedAt", updatedAt);
            } catch (Exception ignored) {
            }
            return json;
        }

        static BookRecord fromJson(JSONObject json) {
            return new BookRecord(
                    json.optString("uri"),
                    json.optString("title", "未命名书籍"),
                    json.optString("author", "未知作者"),
                    json.optInt("chapterCount"),
                    json.optInt("chapterIndex"),
                    json.optInt("scrollY"),
                    (float) json.optDouble("percent"),
                    json.optLong("updatedAt")
            );
        }
    }

    public static class Bookmark {
        public final String bookUri;
        public final String bookTitle;
        public final int chapterIndex;
        public final String chapterTitle;
        public final int scrollY;
        public final String note;
        public final long createdAt;

        public Bookmark(String bookUri, String bookTitle, int chapterIndex, String chapterTitle, int scrollY, String note, long createdAt) {
            this.bookUri = bookUri;
            this.bookTitle = bookTitle == null ? "未命名书籍" : bookTitle;
            this.chapterIndex = chapterIndex;
            this.chapterTitle = chapterTitle == null ? "章节" : chapterTitle;
            this.scrollY = scrollY;
            this.note = note == null ? "" : note;
            this.createdAt = createdAt;
        }

        JSONObject toJson() {
            JSONObject json = new JSONObject();
            try {
                json.put("bookUri", bookUri);
                json.put("bookTitle", bookTitle);
                json.put("chapterIndex", chapterIndex);
                json.put("chapterTitle", chapterTitle);
                json.put("scrollY", scrollY);
                json.put("note", note);
                json.put("createdAt", createdAt);
            } catch (Exception ignored) {
            }
            return json;
        }

        static Bookmark fromJson(JSONObject json) {
            return new Bookmark(
                    json.optString("bookUri"),
                    json.optString("bookTitle"),
                    json.optInt("chapterIndex"),
                    json.optString("chapterTitle", "章节"),
                    json.optInt("scrollY"),
                    json.optString("note"),
                    json.optLong("createdAt")
            );
        }
    }
}