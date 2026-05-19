package com.lisb.reader.ui

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Color
import android.os.Bundle
import android.view.MotionEvent
import android.view.View
import android.view.WindowManager
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.SeekBar
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.lisb.reader.R
import com.lisb.reader.data.SettingsManager
import com.lisb.reader.epub.EpubBook
import com.lisb.reader.tts.TtsManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

class ReaderActivity : AppCompatActivity() {

    private lateinit var settings: SettingsManager
    private lateinit var webView: WebView
    private lateinit var topBar: View
    private lateinit var bottomBar: View
    private lateinit var titleText: TextView
    private lateinit var progressText: TextView
    private lateinit var seekChapter: SeekBar
    private lateinit var touchOverlay: TouchOverlay

    private var book: EpubBook? = null
    private var currentChapter: Int = 0
    private var pendingScrollY: Int = 0
    private var menuVisible = false

    private var tts: TtsManager? = null
    private var ttsActive = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_reader)
        settings = SettingsManager.get(this)

        webView = findViewById(R.id.webview)
        topBar = findViewById(R.id.topBar)
        bottomBar = findViewById(R.id.bottomBar)
        titleText = findViewById(R.id.titleText)
        progressText = findViewById(R.id.progressText)
        seekChapter = findViewById(R.id.seekChapter)
        touchOverlay = findViewById(R.id.touchOverlay)

        setupWebView()
        setupTouchZones()
        setupMenuButtons()
        applyBrightness()

        val bookId = intent.getStringExtra(EXTRA_BOOK_ID)
        if (bookId.isNullOrEmpty()) { finish(); return }
        loadBook(bookId)

        hideMenu(animate = false)
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun setupWebView() {
        webView.settings.apply {
            javaScriptEnabled = false
            useWideViewPort = false
            loadWithOverviewMode = false
            defaultTextEncodingName = "UTF-8"
            cacheMode = WebSettings.LOAD_NO_CACHE
        }
        webView.setBackgroundColor(Color.TRANSPARENT)
        webView.isVerticalScrollBarEnabled = false
        webView.webViewClient = object : WebViewClient() {
            override fun onPageFinished(view: WebView?, url: String?) {
                if (pendingScrollY > 0) {
                    view?.postDelayed({ view.scrollTo(0, pendingScrollY); pendingScrollY = 0 }, 60)
                }
                updateProgressText()
            }
        }
    }

    private fun setupTouchZones() {
        touchOverlay.onTap = { x, y, w, h ->
            when {
                y < h * 0.20f -> toggleMenu()           // top → menu
                x < w * 0.33f -> goPreviousPage()      // left → prev
                x > w * 0.67f -> goNextPage()          // right → next
                else -> toggleMenu()                    // center → menu
            }
        }
    }

    private fun setupMenuButtons() {
        findViewById<View>(R.id.btnBack).setOnClickListener { finish() }
        findViewById<View>(R.id.btnFont).setOnClickListener { showFontDialog() }
        findViewById<View>(R.id.btnTheme).setOnClickListener { showThemeDialog() }
        findViewById<View>(R.id.btnToc).setOnClickListener { showTocDialog() }
        findViewById<View>(R.id.btnTts).setOnClickListener { toggleTts() }
        findViewById<View>(R.id.btnBrightness).setOnClickListener { showBrightnessDialog() }

        seekChapter.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(seekBar: SeekBar?, progress: Int, fromUser: Boolean) {}
            override fun onStartTrackingTouch(seekBar: SeekBar?) {}
            override fun onStopTrackingTouch(seekBar: SeekBar?) {
                val target = seekBar?.progress ?: return
                if (target != currentChapter) loadChapter(target, scrollY = 0)
            }
        })
    }

    private fun loadBook(bookId: String) {
        lifecycleScope.launch {
            val file = File(filesDir, "books/$bookId.epub")
            if (!file.exists()) {
                Toast.makeText(this@ReaderActivity, "找不到书籍文件", Toast.LENGTH_SHORT).show()
                finish(); return@launch
            }
            val b = try {
                withContext(Dispatchers.IO) { EpubBook.open(this@ReaderActivity, file, bookId) }
            } catch (t: Throwable) {
                Toast.makeText(this@ReaderActivity, "打开失败：${t.message}", Toast.LENGTH_LONG).show()
                finish(); return@launch
            }
            book = b
            titleText.text = b.title
            seekChapter.max = (b.chapters.size - 1).coerceAtLeast(0)
            val saved = settings.loadProgress(bookId)
            val ch = (saved?.chapter ?: 0).coerceIn(0, b.chapters.lastIndex.coerceAtLeast(0))
            loadChapter(ch, scrollY = saved?.scrollY ?: 0)
        }
    }

    private fun loadChapter(index: Int, scrollY: Int) {
        val b = book ?: return
        if (b.chapters.isEmpty()) return
        currentChapter = index.coerceIn(0, b.chapters.lastIndex)
        pendingScrollY = scrollY
        seekChapter.progress = currentChapter
        val html = buildHtml(b.chapters[currentChapter].bodyHtml)
        webView.loadDataWithBaseURL(null, html, "text/html", "UTF-8", null)
        saveProgress()
        if (ttsActive) startTtsForCurrentChapter()
    }

    private fun buildHtml(bodyHtml: String): String {
        val theme = settings.theme
        val font = settings.fontFamily
        val size = settings.fontSizePx
        val lh = settings.lineHeight
        return """
            <!DOCTYPE html>
            <html><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
            <style>
              html,body{margin:0;padding:0;${theme.css}}
              body{font-family:${font.css};font-size:${size}px;line-height:$lh;
                   padding:24px 20px 32px 20px;text-align:justify;word-wrap:break-word;}
              p{margin:0 0 0.9em 0;text-indent:2em;}
              h1,h2,h3{font-weight:600;margin:1em 0 0.6em;text-indent:0;}
              img{max-width:100%;height:auto;}
              a{color:inherit;text-decoration:none;}
            </style></head>
            <body>$bodyHtml</body></html>
        """.trimIndent()
    }

    private fun goNextPage() {
        val view = webView
        val pageH = view.height - dp(48)
        val maxY = (view.contentHeight * resources.displayMetrics.density).toInt() - view.height
        val newY = view.scrollY + pageH
        if (view.scrollY >= maxY - 4) {
            // End of chapter
            val b = book ?: return
            if (currentChapter < b.chapters.lastIndex) {
                loadChapter(currentChapter + 1, scrollY = 0)
            } else {
                Toast.makeText(this, "已是最后一页", Toast.LENGTH_SHORT).show()
            }
        } else {
            view.scrollTo(0, newY.coerceAtMost(maxY))
            saveProgress()
            updateProgressText()
        }
    }

    private fun goPreviousPage() {
        val view = webView
        val pageH = view.height - dp(48)
        if (view.scrollY <= 4) {
            if (currentChapter > 0) {
                // Jump to previous chapter, last page
                val prev = currentChapter - 1
                loadChapter(prev, scrollY = Int.MAX_VALUE / 2)
            } else {
                Toast.makeText(this, "已是第一页", Toast.LENGTH_SHORT).show()
            }
        } else {
            view.scrollTo(0, (view.scrollY - pageH).coerceAtLeast(0))
            saveProgress()
            updateProgressText()
        }
    }

    private fun saveProgress() {
        val b = book ?: return
        settings.saveProgress(b.id, currentChapter, webView.scrollY)
    }

    private fun updateProgressText() {
        val b = book ?: return
        val total = b.chapters.size
        progressText.text = "第 ${currentChapter + 1} / $total 章"
    }

    // ---- Menu ----

    private fun toggleMenu() { if (menuVisible) hideMenu() else showMenu() }

    private fun showMenu() {
        menuVisible = true
        topBar.visibility = View.VISIBLE
        bottomBar.visibility = View.VISIBLE
        topBar.alpha = 0f; bottomBar.alpha = 0f
        topBar.animate().alpha(1f).setDuration(150).start()
        bottomBar.animate().alpha(1f).setDuration(150).start()
    }

    private fun hideMenu(animate: Boolean = true) {
        menuVisible = false
        if (animate) {
            topBar.animate().alpha(0f).setDuration(120).withEndAction { topBar.visibility = View.GONE }.start()
            bottomBar.animate().alpha(0f).setDuration(120).withEndAction { bottomBar.visibility = View.GONE }.start()
        } else {
            topBar.visibility = View.GONE; bottomBar.visibility = View.GONE
        }
    }

    // ---- Dialogs ----

    private fun showFontDialog() {
        val dialogView = layoutInflater.inflate(R.layout.dialog_font, null)
        val families = SettingsManager.FontFamily.values()
        val radioGroup = dialogView.findViewById<android.widget.RadioGroup>(R.id.fontGroup)
        families.forEachIndexed { i, f ->
            val rb = android.widget.RadioButton(this).apply {
                id = View.generateViewId(); text = f.label
                isChecked = settings.fontFamily == f
                setOnClickListener { settings.fontFamily = f }
            }
            radioGroup.addView(rb)
        }
        val sizeSeek = dialogView.findViewById<SeekBar>(R.id.sizeSeek)
        val sizeLabel = dialogView.findViewById<TextView>(R.id.sizeLabel)
        sizeSeek.max = 28 // 12..40
        sizeSeek.progress = settings.fontSizePx - 12
        sizeLabel.text = "字号：${settings.fontSizePx}"
        sizeSeek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                settings.fontSizePx = 12 + p
                sizeLabel.text = "字号：${settings.fontSizePx}"
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })
        AlertDialog.Builder(this).setTitle("字体设置").setView(dialogView)
            .setPositiveButton("确定") { _, _ -> reloadCurrentChapter() }
            .setNegativeButton("取消", null).show()
    }

    private fun showThemeDialog() {
        val themes = SettingsManager.Theme.values()
        val labels = themes.map { it.label }.toTypedArray()
        val current = themes.indexOf(settings.theme)
        AlertDialog.Builder(this).setTitle("主题").setSingleChoiceItems(labels, current) { d, i ->
            settings.theme = themes[i]
            reloadCurrentChapter()
            d.dismiss()
        }.setNegativeButton("取消", null).show()
    }

    private fun showTocDialog() {
        val b = book ?: return
        val titles = b.chapters.mapIndexed { i, c -> "${i + 1}. ${c.title}" }.toTypedArray()
        AlertDialog.Builder(this).setTitle("目录")
            .setSingleChoiceItems(titles, currentChapter) { d, which ->
                loadChapter(which, scrollY = 0); d.dismiss()
            }.setNegativeButton("关闭", null).show()
    }

    private fun showBrightnessDialog() {
        val view = layoutInflater.inflate(R.layout.dialog_brightness, null)
        val seek = view.findViewById<SeekBar>(R.id.brightnessSeek)
        val current = settings.brightness
        seek.max = 100
        seek.progress = if (current < 0) 50 else (current * 100).toInt()
        seek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                settings.brightness = p / 100f
                applyBrightness()
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })
        AlertDialog.Builder(this).setTitle("亮度").setView(view)
            .setPositiveButton("确定", null)
            .setNeutralButton("跟随系统") { _, _ ->
                settings.brightness = -1f; applyBrightness()
            }.show()
    }

    private fun applyBrightness() {
        val lp = window.attributes
        val b = settings.brightness
        lp.screenBrightness = if (b < 0) WindowManager.LayoutParams.BRIGHTNESS_OVERRIDE_NONE else b.coerceIn(0.05f, 1f)
        window.attributes = lp
    }

    private fun reloadCurrentChapter() {
        loadChapter(currentChapter, scrollY = webView.scrollY)
    }

    // ---- TTS ----

    private fun toggleTts() {
        if (ttsActive) {
            ttsActive = false
            tts?.stop()
            Toast.makeText(this, "已停止朗读", Toast.LENGTH_SHORT).show()
            return
        }
        val view = layoutInflater.inflate(R.layout.dialog_tts, null)
        val rateSeek = view.findViewById<SeekBar>(R.id.rateSeek)
        val rateLabel = view.findViewById<TextView>(R.id.rateLabel)
        rateSeek.max = 30 // 0..3.0 in steps of 0.1
        rateSeek.progress = ((settings.ttsRate) * 10).toInt().coerceIn(0, 30)
        rateLabel.text = "语速：${"%.1f".format(settings.ttsRate)}x"
        rateSeek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                val r = (p / 10f).coerceAtLeast(0.5f)
                settings.ttsRate = r
                rateLabel.text = "语速：${"%.1f".format(r)}x"
                tts?.setRate(r)
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })
        AlertDialog.Builder(this).setTitle("AI 朗读").setView(view)
            .setPositiveButton("开始朗读") { _, _ ->
                ttsActive = true
                ensureTtsReady { startTtsForCurrentChapter() }
            }.setNegativeButton("取消", null).show()
    }

    private fun ensureTtsReady(then: () -> Unit) {
        if (tts?.isReady == true) { then(); return }
        if (tts == null) {
            tts = TtsManager(this).apply {
                setRate(settings.ttsRate)
                onReady = { runOnUiThread(then) }
                onAllFinished = { runOnUiThread { onChapterTtsFinished() } }
            }
        } else {
            tts?.onReady = { runOnUiThread(then) }
        }
    }

    private fun startTtsForCurrentChapter() {
        val b = book ?: return
        val text = b.chapters[currentChapter].plainText
        tts?.speak(text, "ch_$currentChapter")
        Toast.makeText(this, "开始朗读，结束后将自动翻章", Toast.LENGTH_SHORT).show()
    }

    private fun onChapterTtsFinished() {
        if (!ttsActive) return
        val b = book ?: return
        if (currentChapter < b.chapters.lastIndex) {
            loadChapter(currentChapter + 1, scrollY = 0)
            // loadChapter triggers startTtsForCurrentChapter while ttsActive
        } else {
            ttsActive = false
            Toast.makeText(this, "朗读完毕", Toast.LENGTH_SHORT).show()
        }
    }

    private fun dp(v: Int): Int = (v * resources.displayMetrics.density).toInt()

    override fun onPause() { super.onPause(); saveProgress() }
    override fun onDestroy() {
        super.onDestroy()
        saveProgress()
        tts?.shutdown()
    }

    /** A transparent View placed above the WebView. Forwards tap coordinates while
     * letting long-press / scroll fall through to the WebView. */
    class TouchOverlay @JvmOverloads constructor(
        context: Context, attrs: android.util.AttributeSet? = null
    ) : View(context, attrs) {
        var onTap: ((x: Float, y: Float, w: Int, h: Int) -> Unit)? = null
        private var downX = 0f
        private var downY = 0f
        private var downTime = 0L

        @SuppressLint("ClickableViewAccessibility")
        override fun onTouchEvent(event: MotionEvent): Boolean {
            when (event.actionMasked) {
                MotionEvent.ACTION_DOWN -> {
                    downX = event.x; downY = event.y; downTime = System.currentTimeMillis()
                    return true
                }
                MotionEvent.ACTION_UP -> {
                    val dx = event.x - downX; val dy = event.y - downY
                    val dist2 = dx * dx + dy * dy
                    val elapsed = System.currentTimeMillis() - downTime
                    val slop = 12 * resources.displayMetrics.density
                    if (dist2 < slop * slop && elapsed < 500) {
                        onTap?.invoke(event.x, event.y, width, height)
                        performClick()
                        return true
                    }
                }
            }
            return false
        }

        override fun performClick(): Boolean { super.performClick(); return true }
    }

    companion object { const val EXTRA_BOOK_ID = "book_id" }
}
