package com.lisb.reader.ui

import android.Manifest
import android.annotation.SuppressLint
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.pm.PackageManager
import android.graphics.Color
import android.os.Build
import android.os.Bundle
import android.os.IBinder
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
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.lifecycle.lifecycleScope
import com.lisb.reader.R
import com.lisb.reader.data.SettingsManager
import com.lisb.reader.epub.EpubBook
import com.lisb.reader.tts.TtsManager
import com.lisb.reader.tts.TtsService
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import kotlin.math.ceil
import kotlin.math.max

class ReaderActivity : AppCompatActivity() {

    private lateinit var settings: SettingsManager
    private lateinit var rootView: View
    private lateinit var webView: WebView
    private lateinit var topBar: View
    private lateinit var bottomBar: View
    private lateinit var titleText: TextView
    private lateinit var progressText: TextView
    private lateinit var seekChapter: SeekBar
    private lateinit var touchOverlay: TouchOverlay
    private lateinit var headerIndicator: TextView
    private lateinit var footerIndicator: TextView

    private var book: EpubBook? = null
    private var currentChapter: Int = 0
    private var pendingScrollY: Int = 0
    private var menuVisible = false

    /** Heights reserved for the always-visible indicators (above + below text). */
    private val indicatorHeightDp = 22
    /** Extra margin between text and indicator so glyphs never touch the bar. */
    private val textMarginDp = 6

    // ---- TTS service ----
    private var ttsService: TtsService? = null
    private var ttsBound = false
    private val ttsConn = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, b: IBinder?) {
            ttsService = (b as TtsService.LocalBinder).service
            ttsBound = true
            ttsService?.onAllFinishedExternal = { runOnUiThread { onChapterTtsFinished() } }
            ttsService?.onChapterChangedExternal = { idx ->
                runOnUiThread {
                    if (idx != currentChapter) loadChapter(idx, scrollY = 0, skipTtsRestart = true)
                }
            }
            pushChaptersToServiceIfReady()
        }
        override fun onServiceDisconnected(name: ComponentName?) {
            ttsService = null; ttsBound = false
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_reader)
        settings = SettingsManager.get(this)

        rootView = findViewById(R.id.readerRoot)
        webView = findViewById(R.id.webview)
        topBar = findViewById(R.id.topBar)
        bottomBar = findViewById(R.id.bottomBar)
        titleText = findViewById(R.id.titleText)
        progressText = findViewById(R.id.progressText)
        seekChapter = findViewById(R.id.seekChapter)
        touchOverlay = findViewById(R.id.touchOverlay)
        headerIndicator = findViewById(R.id.headerIndicator)
        footerIndicator = findViewById(R.id.footerIndicator)

        applySystemInsets()
        setupWebView()
        setupTouchZones()
        setupMenuButtons()
        applyBrightness()
        applyReaderThemeColors()

        val bookId = intent.getStringExtra(EXTRA_BOOK_ID)
        if (bookId.isNullOrEmpty()) { finish(); return }
        loadBook(bookId)

        hideMenu(animate = false)

        bindService(Intent(this, TtsService::class.java), ttsConn, Context.BIND_AUTO_CREATE)
    }

    /** Pad WebView and indicator bars for system bars + reserved indicator strips. */
    private fun applySystemInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(rootView) { _, insets ->
            val sb = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            val ind = dp(indicatorHeightDp)
            val gap = dp(textMarginDp)
            // WebView reserves: system bars + indicator strip + small gap on both ends
            webView.setPadding(0, sb.top + ind + gap, 0, sb.bottom + ind + gap)
            headerIndicator.setPadding(
                headerIndicator.paddingLeft, sb.top + dp(4),
                headerIndicator.paddingRight, dp(4)
            )
            footerIndicator.setPadding(
                footerIndicator.paddingLeft, dp(4),
                footerIndicator.paddingRight, sb.bottom + dp(4)
            )
            topBar.setPadding(topBar.paddingLeft, sb.top + dp(12), topBar.paddingRight, topBar.paddingBottom)
            bottomBar.setPadding(bottomBar.paddingLeft, bottomBar.paddingTop, bottomBar.paddingRight, sb.bottom + dp(12))
            insets
        }
    }

    /** Choose indicator text color based on theme to stay legible. */
    private fun applyReaderThemeColors() {
        val onLight = when (settings.theme) {
            SettingsManager.Theme.LIGHT, SettingsManager.Theme.SEPIA -> true
            SettingsManager.Theme.DARK, SettingsManager.Theme.BLACK -> false
        }
        val bg = when (settings.theme) {
            SettingsManager.Theme.LIGHT -> Color.parseColor("#FFFFFF")
            SettingsManager.Theme.SEPIA -> Color.parseColor("#F4ECD8")
            SettingsManager.Theme.DARK  -> Color.parseColor("#121212")
            SettingsManager.Theme.BLACK -> Color.BLACK
        }
        rootView.setBackgroundColor(bg)
        val ind = if (onLight) Color.parseColor("#88000000") else Color.parseColor("#AAFFFFFF")
        headerIndicator.setTextColor(ind)
        footerIndicator.setTextColor(ind)
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
                    view?.postDelayed({
                        view.scrollTo(0, pendingScrollY); pendingScrollY = 0
                        updateIndicators()
                    }, 80)
                } else {
                    view?.postDelayed({ updateIndicators() }, 60)
                }
                updateProgressText()
            }
        }
        webView.viewTreeObserver.addOnScrollChangedListener { updateIndicators() }
    }

    private fun setupTouchZones() {
        touchOverlay.onTap = { x, y, w, h ->
            when {
                y < h * 0.20f -> toggleMenu()
                x < w * 0.33f -> goPreviousPage()
                x > w * 0.67f -> goNextPage()
                else -> toggleMenu()
            }
        }
        touchOverlay.onSwipeLeft = { goNextPage() }
        touchOverlay.onSwipeRight = { goPreviousPage() }
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
            pushChaptersToServiceIfReady()
            val saved = settings.loadProgress(bookId)
            val ch = (saved?.chapter ?: 0).coerceIn(0, b.chapters.lastIndex.coerceAtLeast(0))
            loadChapter(ch, scrollY = saved?.scrollY ?: 0)
        }
    }

    /** When both the book is loaded AND the service is bound, hand the
     *  service the lightweight chapter list so its prev/next buttons work. */
    private fun pushChaptersToServiceIfReady() {
        val b = book ?: return
        val svc = ttsService ?: return
        svc.setBook(
            b.id, b.title,
            b.chapters.map { TtsService.ChapterInfo(it.title, it.plainText) }
        )
    }

    private fun loadChapter(index: Int, scrollY: Int, skipTtsRestart: Boolean = false) {
        val b = book ?: return
        if (b.chapters.isEmpty()) return
        currentChapter = index.coerceIn(0, b.chapters.lastIndex)
        pendingScrollY = scrollY
        seekChapter.progress = currentChapter
        val html = buildHtml(b.chapters[currentChapter])
        webView.loadDataWithBaseURL(null, html, "text/html", "UTF-8", null)
        saveProgress()
    }

    private fun buildHtml(chapter: com.lisb.reader.epub.EpubBook.Chapter): String {
        val theme = settings.theme
        val font = settings.fontFamily
        val size = settings.fontSizePx
        val lh = settings.lineHeight
        val ls = settings.letterSpacing

        // Smaller padding than before — we reserve space at the OUTSIDE of the
        // WebView (paddingTop/Bottom on the View) so the indicator bars never
        // overlap the text. Inside the HTML we just need comfortable margins.
        return if (settings.preserveEpubStyle) {
            val overlay = """
                <style id="lisb-theme-overlay">
                  html,body{${theme.css}}
                  body{padding:12px 20px 24px 20px;}
                  img{max-width:100%;height:auto;}
                </style>
            """.trimIndent()
            """
                <!DOCTYPE html>
                <html><head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
                ${chapter.headHtml}
                $overlay
                </head>
                <body>${chapter.bodyHtml}</body></html>
            """.trimIndent()
        } else {
            """
                <!DOCTYPE html>
                <html><head><meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
                <style>
                  html,body{margin:0;padding:0;${theme.css}}
                  body{font-family:${font.css};font-size:${size}px;line-height:$lh;
                       letter-spacing:${"%.3f".format(ls)}em;
                       padding:12px 20px 24px 20px;text-align:justify;word-wrap:break-word;}
                  p{margin:0 0 0.9em 0;text-indent:2em;}
                  h1,h2,h3{font-weight:600;margin:1em 0 0.6em;text-indent:0;letter-spacing:0;}
                  img{max-width:100%;height:auto;}
                  a{color:inherit;text-decoration:none;}
                </style></head>
                <body>${chapter.bodyHtml}</body></html>
            """.trimIndent()
        }
    }

    // ---- Pagination helpers ----

    private fun pageStepPx(): Int {
        // Each "page" is exactly the WebView's visible inner height (it
        // already has system-bar + indicator-strip padding subtracted).
        return max(1, webView.height - webView.paddingTop - webView.paddingBottom)
    }

    private fun totalPagesInChapter(): Int {
        val totalContent = (webView.contentHeight * resources.displayMetrics.density).toInt()
        return max(1, ceil(totalContent.toDouble() / pageStepPx()).toInt())
    }

    private fun currentPageInChapter(): Int =
        (webView.scrollY / pageStepPx()) + 1

    private fun goNextPage() {
        val view = webView
        val step = pageStepPx()
        val totalContent = (view.contentHeight * resources.displayMetrics.density).toInt()
        val maxY = (totalContent - view.height + view.paddingBottom).coerceAtLeast(0)
        if (view.scrollY >= maxY - 4) {
            val b = book ?: return
            if (currentChapter < b.chapters.lastIndex) {
                loadChapter(currentChapter + 1, scrollY = 0)
            } else {
                Toast.makeText(this, "已是最后一页", Toast.LENGTH_SHORT).show()
            }
        } else {
            view.scrollTo(0, (view.scrollY + step).coerceAtMost(maxY))
            saveProgress(); updateProgressText(); updateIndicators()
        }
    }

    private fun goPreviousPage() {
        val view = webView
        val step = pageStepPx()
        if (view.scrollY <= 4) {
            if (currentChapter > 0) {
                loadChapter(currentChapter - 1, scrollY = Int.MAX_VALUE / 2)
            } else {
                Toast.makeText(this, "已是第一页", Toast.LENGTH_SHORT).show()
            }
        } else {
            view.scrollTo(0, (view.scrollY - step).coerceAtLeast(0))
            saveProgress(); updateProgressText(); updateIndicators()
        }
    }

    private fun saveProgress() {
        val b = book ?: return
        settings.saveProgress(b.id, currentChapter, webView.scrollY)
    }

    private fun updateProgressText() {
        val b = book ?: return
        progressText.text = "第 ${currentChapter + 1} / ${b.chapters.size} 章"
    }

    private fun updateIndicators() {
        val b = book ?: return
        if (webView.height <= 0) return
        val total = b.chapters.size
        val page = currentPageInChapter()
        val pages = totalPagesInChapter()
        val chapterName = b.chapters.getOrNull(currentChapter)?.title.orEmpty()
        headerIndicator.text = "第 ${currentChapter + 1}/${total} 章 · ${page}/${pages} 页  ·  $chapterName"

        // Whole-book progress: chapter portion + page-within-chapter portion.
        val chapterFrac = if (total > 0) currentChapter.toFloat() / total else 0f
        val inChapterFrac = if (pages > 0) (page - 1).toFloat() / pages else 0f
        val pct = ((chapterFrac + inChapterFrac / total) * 100).toInt().coerceIn(0, 100)
        footerIndicator.text = "进度 ${pct}%  ·  本章 ${page}/${pages} 页"
    }

    // ---- Menu ----

    private fun toggleMenu() { if (menuVisible) hideMenu() else showMenu() }

    private fun showMenu() {
        menuVisible = true
        topBar.visibility = View.VISIBLE; bottomBar.visibility = View.VISIBLE
        topBar.alpha = 0f; bottomBar.alpha = 0f
        topBar.animate().alpha(1f).setDuration(150).start()
        bottomBar.animate().alpha(1f).setDuration(150).start()
        // Indicators hide while the menu (which carries fuller info) is up.
        headerIndicator.animate().alpha(0f).setDuration(120).start()
        footerIndicator.animate().alpha(0f).setDuration(120).start()
    }

    private fun hideMenu(animate: Boolean = true) {
        menuVisible = false
        if (animate) {
            topBar.animate().alpha(0f).setDuration(120).withEndAction { topBar.visibility = View.GONE }.start()
            bottomBar.animate().alpha(0f).setDuration(120).withEndAction { bottomBar.visibility = View.GONE }.start()
            headerIndicator.animate().alpha(1f).setDuration(150).start()
            footerIndicator.animate().alpha(1f).setDuration(150).start()
        } else {
            topBar.visibility = View.GONE; bottomBar.visibility = View.GONE
            headerIndicator.alpha = 1f; footerIndicator.alpha = 1f
        }
    }

    // ---- Dialogs ----

    private fun showFontDialog() {
        val dialogView = layoutInflater.inflate(R.layout.dialog_font, null)
        val families = SettingsManager.FontFamily.values()
        val radioGroup = dialogView.findViewById<android.widget.RadioGroup>(R.id.fontGroup)
        families.forEach { f ->
            val rb = android.widget.RadioButton(this).apply {
                id = View.generateViewId(); text = f.label
                isChecked = settings.fontFamily == f
                setOnClickListener { settings.fontFamily = f }
            }
            radioGroup.addView(rb)
        }
        val sizeSeek = dialogView.findViewById<SeekBar>(R.id.sizeSeek)
        val sizeLabel = dialogView.findViewById<TextView>(R.id.sizeLabel)
        sizeSeek.max = 28
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

        val spacingSeek = dialogView.findViewById<SeekBar>(R.id.spacingSeek)
        val spacingLabel = dialogView.findViewById<TextView>(R.id.spacingLabel)
        spacingSeek.max = 30 // 0.00em .. 0.30em in 0.01 steps
        spacingSeek.progress = (settings.letterSpacing * 100).toInt().coerceIn(0, 30)
        spacingLabel.text = "字间距：${"%.2f".format(settings.letterSpacing)}em"
        spacingSeek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                val v = p / 100f
                settings.letterSpacing = v
                spacingLabel.text = "字间距：${"%.2f".format(v)}em"
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })

        val lhSeek = dialogView.findViewById<SeekBar>(R.id.lineHeightSeek)
        val lhLabel = dialogView.findViewById<TextView>(R.id.lineHeightLabel)
        lhSeek.max = 20 // 1.0 .. 3.0 in 0.1 steps
        lhSeek.progress = ((settings.lineHeight - 1.0f) * 10).toInt().coerceIn(0, 20)
        lhLabel.text = "行间距：${"%.1f".format(settings.lineHeight)}"
        lhSeek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                val v = 1.0f + p / 10f
                settings.lineHeight = v
                lhLabel.text = "行间距：${"%.1f".format(v)}"
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })

        val preserveBox = dialogView.findViewById<android.widget.CheckBox>(R.id.preserveStyle)
        preserveBox.isChecked = settings.preserveEpubStyle
        preserveBox.setOnCheckedChangeListener { _, checked -> settings.preserveEpubStyle = checked }
        AlertDialog.Builder(this).setTitle("字体设置").setView(dialogView)
            .setPositiveButton("确定") { _, _ -> reloadCurrentChapter() }
            .setNegativeButton("取消", null).show()
    }

    private fun showThemeDialog() {
        val themes = SettingsManager.Theme.values()
        val labels = themes.map { it.label }.toTypedArray()
        val current = themes.indexOf(settings.theme)
        AlertDialog.Builder(this).setTitle("主题").setSingleChoiceItems(labels, current) { d, i ->
            settings.theme = themes[i]; applyReaderThemeColors(); reloadCurrentChapter(); d.dismiss()
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
                settings.brightness = p / 100f; applyBrightness()
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })
        AlertDialog.Builder(this).setTitle("亮度").setView(view)
            .setPositiveButton("确定", null)
            .setNeutralButton("跟随系统") { _, _ -> settings.brightness = -1f; applyBrightness() }
            .show()
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

    // ==================== TTS ====================

    private fun toggleTts() {
        val svc = ttsService
        if (svc != null && svc.playing) {
            svc.doPause()
            Toast.makeText(this, "已暂停朗读", Toast.LENGTH_SHORT).show()
            return
        }
        ensureNotificationPermission()
        val view = layoutInflater.inflate(R.layout.dialog_tts, null)
        val rateSeek = view.findViewById<SeekBar>(R.id.rateSeek)
        val rateLabel = view.findViewById<TextView>(R.id.rateLabel)
        rateSeek.max = 30
        rateSeek.progress = ((settings.ttsRate) * 10).toInt().coerceIn(0, 30)
        rateLabel.text = "语速：${"%.1f".format(settings.ttsRate)}x"
        rateSeek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                val r = (p / 10f).coerceAtLeast(0.5f)
                settings.ttsRate = r
                rateLabel.text = "语速：${"%.1f".format(r)}x"
                ttsService?.setRate(r)
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })
        AlertDialog.Builder(this).setTitle("AI 朗读").setView(view)
            .setPositiveButton("开始朗读") { _, _ -> promptStartPosition() }
            .setNegativeButton("取消", null).show()
    }

    private fun promptStartPosition() {
        val b = book ?: return
        val currentChunkIdx = currentPageChunkIndex(b.chapters[currentChapter].plainText)
        val saved = settings.loadTtsProgress(b.id)
        val savedDifferent = saved != null &&
                (saved.chapter != currentChapter || kotlin.math.abs(saved.chunkIndex - currentChunkIdx) > 1)
        if (savedDifferent && saved != null) {
            AlertDialog.Builder(this).setTitle("继续上次朗读？")
                .setMessage("上次朗读到第 ${saved.chapter + 1} 章 · 第 ${saved.chunkIndex + 1} 句")
                .setPositiveButton("从上次位置") { _, _ ->
                    if (saved.chapter != currentChapter) {
                        loadChapter(saved.chapter, scrollY = 0, skipTtsRestart = true)
                        webView.postDelayed({ startSpeakingHere(saved.chapter, saved.chunkIndex) }, 220)
                    } else {
                        startSpeakingHere(currentChapter, saved.chunkIndex)
                    }
                }
                .setNegativeButton("从当前页") { _, _ -> startSpeakingHere(currentChapter, currentChunkIdx) }
                .show()
        } else {
            startSpeakingHere(currentChapter, currentChunkIdx)
        }
    }

    private fun currentPageChunkIndex(plainText: String): Int {
        val chunks = TtsManager.splitForTts(plainText)
        if (chunks.isEmpty()) return 0
        val contentPx = (webView.contentHeight * resources.displayMetrics.density).toInt()
        val visible = max(1, contentPx - webView.height)
        val ratio = webView.scrollY.toFloat() / visible.toFloat()
        return (chunks.size * ratio.coerceIn(0f, 1f)).toInt().coerceIn(0, chunks.lastIndex)
    }

    private fun startSpeakingHere(chapterIdx: Int, startChunk: Int) {
        val svc = ttsService ?: run {
            webView.postDelayed({ startSpeakingHere(chapterIdx, startChunk) }, 200)
            return
        }
        pushChaptersToServiceIfReady()
        ContextCompat.startForegroundService(this, Intent(this, TtsService::class.java))
        svc.setRate(settings.ttsRate)
        svc.startSpeaking(chapterIdx, startChunk)
        Toast.makeText(this, "开始朗读", Toast.LENGTH_SHORT).show()
    }

    private fun onChapterTtsFinished() {
        val b = book ?: return
        if (currentChapter < b.chapters.lastIndex) {
            // Service has already advanced its own pointer; just sync the UI.
            loadChapter(currentChapter + 1, scrollY = 0, skipTtsRestart = true)
        } else {
            Toast.makeText(this, "朗读完毕", Toast.LENGTH_SHORT).show()
            ttsService?.doStop()
        }
    }

    private fun ensureNotificationPermission() {
        if (Build.VERSION.SDK_INT < 33) return
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) ==
            PackageManager.PERMISSION_GRANTED) return
        requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), REQ_NOTIF)
    }

    private fun dp(v: Int): Int = (v * resources.displayMetrics.density).toInt()

    override fun onPause() { super.onPause(); saveProgress() }
    override fun onDestroy() {
        super.onDestroy()
        saveProgress()
        if (ttsBound) { try { unbindService(ttsConn) } catch (_: Throwable) {}; ttsBound = false }
    }

    companion object {
        const val EXTRA_BOOK_ID = "book_id"
        private const val REQ_NOTIF = 1101
    }
}
