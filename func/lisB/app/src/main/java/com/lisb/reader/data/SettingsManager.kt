package com.lisb.reader.data

import android.content.Context
import android.content.SharedPreferences
import androidx.core.content.edit
import org.json.JSONArray
import org.json.JSONObject

/** Persists user settings + reading progress + bookshelf. */
class SettingsManager private constructor(private val prefs: SharedPreferences) {

    enum class Theme(val css: String, val label: String) {
        LIGHT("background:#FFFFFF;color:#1A1A1A;", "白天"),
        SEPIA("background:#F4ECD8;color:#5B4636;", "护眼"),
        DARK("background:#121212;color:#D6D6D6;", "夜间"),
        BLACK("background:#000000;color:#9E9E9E;", "纯黑");

        companion object {
            fun fromName(n: String?): Theme = values().firstOrNull { it.name == n } ?: LIGHT
        }
    }

    enum class FontFamily(val css: String, val label: String) {
        SERIF("'Noto Serif', 'Source Han Serif', serif", "宋体"),
        SANS("'Noto Sans', 'PingFang SC', 'Source Han Sans', sans-serif", "黑体"),
        SYSTEM("system-ui, sans-serif", "系统");

        companion object {
            fun fromName(n: String?): FontFamily = values().firstOrNull { it.name == n } ?: SERIF
        }
    }

    var theme: Theme
        get() = Theme.fromName(prefs.getString(KEY_THEME, null))
        set(value) = prefs.edit { putString(KEY_THEME, value.name) }

    var fontFamily: FontFamily
        get() = FontFamily.fromName(prefs.getString(KEY_FONT, null))
        set(value) = prefs.edit { putString(KEY_FONT, value.name) }

    /** Font size in CSS px. */
    var fontSizePx: Int
        get() = prefs.getInt(KEY_FONT_SIZE, 20).coerceIn(12, 40)
        set(value) = prefs.edit { putInt(KEY_FONT_SIZE, value.coerceIn(12, 40)) }

    var lineHeight: Float
        get() = prefs.getFloat(KEY_LINE_HEIGHT, 1.7f)
        set(value) = prefs.edit { putFloat(KEY_LINE_HEIGHT, value) }

    /** Letter spacing in em (0.00..0.30). Only applied in clean (non-preserve) layout. */
    var letterSpacing: Float
        get() = prefs.getFloat(KEY_LETTER_SPACING, 0.02f).coerceIn(0f, 0.3f)
        set(value) = prefs.edit { putFloat(KEY_LETTER_SPACING, value.coerceIn(0f, 0.3f)) }

    /** Brightness override (0..1), -1 = system default. */
    var brightness: Float
        get() = prefs.getFloat(KEY_BRIGHTNESS, -1f)
        set(value) = prefs.edit { putFloat(KEY_BRIGHTNESS, value) }

    /** TTS speech rate. */
    var ttsRate: Float
        get() = prefs.getFloat(KEY_TTS_RATE, 1.0f)
        set(value) = prefs.edit { putFloat(KEY_TTS_RATE, value) }

    /** Reader menu bar background alpha (0.3..1.0). Text/icons remain opaque. */
    var menuBarAlpha: Float
        get() = prefs.getFloat(KEY_MENU_ALPHA, 0.9f).coerceIn(0.3f, 1f)
        set(value) = prefs.edit { putFloat(KEY_MENU_ALPHA, value.coerceIn(0.3f, 1f)) }

    /**
     * When true, the EPUB's own <head>/<style>/inline CSS are kept and only
     * theme colors (+ optionally font size) are overlaid on top. When false,
     * EPUB styling is discarded and our clean reader template is used.
     */
    var preserveEpubStyle: Boolean
        get() = prefs.getBoolean(KEY_PRESERVE_STYLE, true)
        set(value) = prefs.edit { putBoolean(KEY_PRESERVE_STYLE, value) }

    data class Progress(val chapter: Int, val scrollY: Int, val updatedAt: Long)

    fun saveProgress(bookId: String, chapter: Int, scrollY: Int) {
        prefs.edit {
            putInt(progKey(bookId, "ch"), chapter)
            putInt(progKey(bookId, "y"), scrollY)
            putLong(progKey(bookId, "t"), System.currentTimeMillis())
        }
    }

    fun loadProgress(bookId: String): Progress? {
        if (!prefs.contains(progKey(bookId, "ch"))) return null
        return Progress(
            prefs.getInt(progKey(bookId, "ch"), 0),
            prefs.getInt(progKey(bookId, "y"), 0),
            prefs.getLong(progKey(bookId, "t"), 0L)
        )
    }

    // ---- TTS progress (per book) ----

    data class TtsProgress(val chapter: Int, val chunkIndex: Int, val updatedAt: Long)

    fun saveTtsProgress(bookId: String, chapter: Int, chunkIndex: Int) {
        prefs.edit {
            putInt(ttsKey(bookId, "ch"), chapter)
            putInt(ttsKey(bookId, "ck"), chunkIndex)
            putLong(ttsKey(bookId, "t"), System.currentTimeMillis())
        }
    }

    fun loadTtsProgress(bookId: String): TtsProgress? {
        if (!prefs.contains(ttsKey(bookId, "ch"))) return null
        return TtsProgress(
            prefs.getInt(ttsKey(bookId, "ch"), 0),
            prefs.getInt(ttsKey(bookId, "ck"), 0),
            prefs.getLong(ttsKey(bookId, "t"), 0L)
        )
    }

    fun clearTtsProgress(bookId: String) {
        prefs.edit {
            remove(ttsKey(bookId, "ch"))
            remove(ttsKey(bookId, "ck"))
            remove(ttsKey(bookId, "t"))
        }
    }

    private fun ttsKey(bookId: String, k: String) = "tts_${bookId}_$k"

    // ---- Bookshelf (list of imported books) ----

    data class ShelfEntry(val id: String, val title: String, val author: String, val addedAt: Long)

    fun addToShelf(entry: ShelfEntry) {
        val list = getShelf().toMutableList()
        if (list.any { it.id == entry.id }) return
        list.add(entry)
        writeShelf(list)
    }

    fun removeFromShelf(id: String) {
        writeShelf(getShelf().filterNot { it.id == id })
    }

    fun getShelf(): List<ShelfEntry> {
        val json = prefs.getString(KEY_SHELF, null) ?: return emptyList()
        val arr = JSONArray(json)
        return (0 until arr.length()).map { i ->
            val o = arr.getJSONObject(i)
            ShelfEntry(o.getString("id"), o.getString("title"), o.getString("author"), o.getLong("addedAt"))
        }
    }

    private fun writeShelf(list: List<ShelfEntry>) {
        val arr = JSONArray()
        list.forEach { e ->
            arr.put(JSONObject().apply {
                put("id", e.id); put("title", e.title)
                put("author", e.author); put("addedAt", e.addedAt)
            })
        }
        prefs.edit { putString(KEY_SHELF, arr.toString()) }
    }

    private fun progKey(bookId: String, k: String) = "progress_${bookId}_$k"

    companion object {
        private const val PREFS = "lisb_prefs"
        private const val KEY_THEME = "theme"
        private const val KEY_FONT = "font"
        private const val KEY_FONT_SIZE = "font_size"
        private const val KEY_LINE_HEIGHT = "line_height"
        private const val KEY_LETTER_SPACING = "letter_spacing"
        private const val KEY_BRIGHTNESS = "brightness"
        private const val KEY_TTS_RATE = "tts_rate"
        private const val KEY_MENU_ALPHA = "menu_alpha"
        private const val KEY_SHELF = "shelf"
        private const val KEY_PRESERVE_STYLE = "preserve_epub_style"

        @Volatile private var inst: SettingsManager? = null
        fun get(context: Context): SettingsManager = inst ?: synchronized(this) {
            inst ?: SettingsManager(context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE))
                .also { inst = it }
        }
    }
}
