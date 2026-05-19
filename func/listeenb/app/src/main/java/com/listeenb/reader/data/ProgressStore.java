package com.listeenb.reader.data;

import android.content.Context;
import android.content.SharedPreferences;

public class ProgressStore {
    private static final String PREFS = "reader_progress";
    private static final String KEY_BOOK_URI = "book_uri";
    private static final String KEY_CHAPTER_INDEX = "chapter_index";
    private static final String KEY_SCROLL_Y = "scroll_y";
    private static final String KEY_VOICE_GENDER = "voice_gender";
    private static final String KEY_UPDATED_AT = "updated_at";

    private final SharedPreferences preferences;

    public ProgressStore(Context context) {
        preferences = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    public void saveBookUri(String uri) {
        preferences.edit()
                .putString(KEY_BOOK_URI, uri)
                .putLong(KEY_UPDATED_AT, System.currentTimeMillis())
                .apply();
    }

    public String getBookUri() {
        return preferences.getString(KEY_BOOK_URI, null);
    }

    public void saveProgress(int chapterIndex, int scrollY) {
        preferences.edit()
                .putInt(KEY_CHAPTER_INDEX, chapterIndex)
                .putInt(KEY_SCROLL_Y, scrollY)
                .putLong(KEY_UPDATED_AT, System.currentTimeMillis())
                .apply();
    }

    public int getChapterIndex() {
        return preferences.getInt(KEY_CHAPTER_INDEX, 0);
    }

    public int getScrollY() {
        return preferences.getInt(KEY_SCROLL_Y, 0);
    }

    public void saveVoiceGender(String gender) {
        preferences.edit().putString(KEY_VOICE_GENDER, gender).apply();
    }

    public String getVoiceGender() {
        return preferences.getString(KEY_VOICE_GENDER, "female");
    }

    public void clearBook() {
        preferences.edit()
                .remove(KEY_BOOK_URI)
                .remove(KEY_CHAPTER_INDEX)
                .remove(KEY_SCROLL_Y)
                .apply();
    }
}