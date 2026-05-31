package com.lisb.reader.ui

import android.Manifest
import android.annotation.SuppressLint
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.pm.PackageManager
import android.graphics.Color
import android.graphics.drawable.ColorDrawable
import android.os.Build
import android.os.Bundle
import android.os.IBinder
import android.view.GestureDetector
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
    private lateinit var headerIndicator: TextView
    private lateinit var footerIndicator: TextView

    private var book: EpubBook? = null
    private var currentChapter: Int = 0
    private var pendingScrollY: Int = 0
    // Cached state mirrored from the JS virtual-scroll engine. Updated on
    // every page turn / chapter load. The WebView never scrolls natively
    // anymore (overflow:hidden), so webView.scrollY would always be 0.
    private var currentVirtualY: Int = 0  // CSS px
    private var currentPage: Int = 1
    private var currentTotal: Int = 1
    private var menuVisible = false

    /** When true, the WebView shows a plain-text "TTS mode" rendering where
     *  each TTS chunk is wrapped in a span so we can highlight it and snap
     *  pages to it. Set true on first startSpeakingHere, cleared on doStop. */
    private var isTtsActive: Boolean = false
    /** If non-null, after the next onPageFinished the service is told to
     *  start speaking this chunk index for the current chapter. */
    private var pendingTtsStartChunk: Int? = null

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
                    if (idx != currentChapter) {
                        // The service auto-advanced to a new chapter.
                        // Load it in TTS mode; onPageFinished will pick up
                        // highlights from the service's onChunkStart callbacks.
                        loadChapter(idx, scrollY = 0, skipTtsRestart = true)
                    }
                }
            }
            ttsService?.onChunkStartExternal = { idx ->
                runOnUiThread { onTtsChunkStarted(idx) }
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
        headerIndicator = findViewById(R.id.headerIndicator)
        footerIndicator = findViewById(R.id.footerIndicator)

        applySystemInsets()
        setupWebView()
        setupTouchZones()
        setupMenuButtons()
        applyBrightness()
        applyReaderThemeColors()
        applyMenuBarAlpha()

        val bookId = intent.getStringExtra(EXTRA_BOOK_ID)
        if (bookId.isNullOrEmpty()) { finish(); return }
        loadBook(bookId)

        hideMenu(animate = false)

        bindService(Intent(this, TtsService::class.java), ttsConn, Context.BIND_AUTO_CREATE)
    }

    /**
     * Apply system bar insets to the header (top) and footer (bottom) bars.
     * The WebView is sandwiched between them inside a vertical LinearLayout,
     * so it never needs its own padding to avoid the system bars or the
     * indicator strips. (Setting padding on a WebView does NOT reliably push
     * rendered HTML content inward — content draws from y=0 of the view's
     * bounds regardless, which used to cause the header / footer to overlap
     * the text on first/last pages.)
     */
    private fun applySystemInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(rootView) { _, insets ->
            val sb = insets.getInsets(WindowInsetsCompat.Type.systemBars())
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

    /** Choose indicator text + background colors based on theme to stay legible. */
    private fun applyReaderThemeColors() {
        val onLight = when (settings.theme) {
            SettingsManager.Theme.LIGHT, SettingsManager.Theme.SEPIA, SettingsManager.Theme.GREEN -> true
            SettingsManager.Theme.DARK, SettingsManager.Theme.BLACK -> false
        }
        val bg = when (settings.theme) {
            SettingsManager.Theme.LIGHT -> Color.parseColor("#FFFFFF")
            SettingsManager.Theme.SEPIA -> Color.parseColor("#F4ECD8")
            SettingsManager.Theme.GREEN -> Color.parseColor("#E0EBD8")
            SettingsManager.Theme.DARK  -> Color.parseColor("#121212")
            SettingsManager.Theme.BLACK -> Color.BLACK
        }
        rootView.setBackgroundColor(bg)
        headerIndicator.setBackgroundColor(bg)
        footerIndicator.setBackgroundColor(bg)
        val ind = if (onLight) Color.parseColor("#88000000") else Color.parseColor("#AAFFFFFF")
        headerIndicator.setTextColor(ind)
        footerIndicator.setTextColor(ind)
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun setupWebView() {
        webView.settings.apply {
            // JS is required for TTS-mode highlight + auto page snap. We only
            // load locally-built HTML (loadDataWithBaseURL with null base) so
            // there is no untrusted script surface.
            javaScriptEnabled = true
            useWideViewPort = false
            loadWithOverviewMode = false
            defaultTextEncodingName = "UTF-8"
            cacheMode = WebSettings.LOAD_NO_CACHE
        }
        webView.setBackgroundColor(Color.TRANSPARENT)
        webView.isVerticalScrollBarEnabled = false
        webView.webViewClient = object : WebViewClient() {
            override fun onPageFinished(view: WebView?, url: String?) {
                // Give the WebView a tick so getComputedStyle sees the real
                // line-height before we restore scroll position.
                view?.postDelayed({
                    if (pendingScrollY > 0) {
                        webView.evaluateJavascript("lisbSetY($pendingScrollY)", null)
                        pendingScrollY = 0
                    }
                    refreshPageInfo {
                        updateIndicators()
                        firePendingTtsStartIfAny()
                    }
                }, 80)
                updateProgressText()
            }

            override fun shouldOverrideUrlLoading(view: WebView?, request: android.webkit.WebResourceRequest?): Boolean {
                // EPUB TOC pages often have internal links like
                // "chapter1.xhtml" or "#section". Since we load via
                // loadDataWithBaseURL(null,...), these resolve to about:blank
                // or data: scheme URLs that would navigate the WebView to a
                // blank page. Intercept and try to match to a chapter.
                val url = request?.url?.toString() ?: return true
                handleInternalLink(url)
                return true // always consume — never let WebView navigate away
            }

            @Deprecated("Deprecated in Java")
            override fun shouldOverrideUrlLoading(view: WebView?, url: String?): Boolean {
                url?.let { handleInternalLink(it) }
                return true
            }
        }
        // No native scrolling happens (overflow:hidden) so the scroll-changed
        // listener would never fire — indicator refreshes are driven by
        // refreshPageInfo() after every page turn instead.
    }

    /** Called after a TTS-mode chapter finishes loading; if a chunk was
     *  queued (e.g. user just hit "start TTS here"), tell the service now. */
    private fun firePendingTtsStartIfAny() {
        val idx = pendingTtsStartChunk ?: return
        pendingTtsStartChunk = null
        val svc = ttsService ?: return
        ContextCompat.startForegroundService(this, Intent(this, TtsService::class.java))
        svc.setRate(settings.ttsRate)
        webView.evaluateJavascript("lisbSetMinChunk($idx)", null)
        svc.startSpeaking(currentChapter, idx)
    }

    /**
     * Attach a gesture detector directly to the WebView for tap-zone
     * navigation and horizontal swipes, while letting the WebView itself
     * handle long-press → text selection → copy natively. We deliberately
     * return false from setOnTouchListener so the WebView always sees the
     * raw events too (single taps on text are no-ops in WebView, so there
     * is no visible double-action).
     */
    @SuppressLint("ClickableViewAccessibility")
    private fun setupTouchZones() {
        val density = resources.displayMetrics.density
        val minSwipePx = 60f * density
        val gd = GestureDetector(this, object : GestureDetector.SimpleOnGestureListener() {
            override fun onSingleTapUp(e: MotionEvent): Boolean {
                // If the tap landed on an image, let the JS zoom handler take
                // over instead of turning the page. We check via HitTestResult.
                val hit = webView.hitTestResult
                if (hit.type == WebView.HitTestResult.IMAGE_TYPE ||
                    hit.type == WebView.HitTestResult.SRC_IMAGE_ANCHOR_TYPE) {
                    return false // don't turn page; JS click handler shows zoom
                }
                // If zoom overlay is currently shown, dismiss it via JS and
                // suppress page turn. (The overlay's own click handler will
                // hide it, but we need to also suppress the Kotlin action.)
                webView.evaluateJavascript("lisbIsZoomShown()") { raw ->
                    val shown = raw?.trim('"') == "true"
                    if (shown) {
                        webView.evaluateJavascript(
                            "document.getElementById('lisb-img-overlay').click();", null)
                        return@evaluateJavascript
                    }
                    val w = webView.width; val h = webView.height
                    if (w <= 0 || h <= 0) return@evaluateJavascript
                    val x = e.x; val y = e.y
                    when {
                        y < h * 0.20f -> toggleMenu()
                        x < w * 0.33f -> goPreviousPage()
                        x > w * 0.67f -> goNextPage()
                        else -> toggleMenu()
                    }
                }
                return true
            }
            override fun onFling(
                e1: MotionEvent?, e2: MotionEvent, vx: Float, vy: Float
            ): Boolean {
                e1 ?: return false
                val dx = e2.x - e1.x
                val dy = e2.y - e1.y
                if (kotlin.math.abs(dx) >= minSwipePx &&
                    kotlin.math.abs(dy) < kotlin.math.abs(dx) * 0.6f) {
                    if (dx < 0) goNextPage() else goPreviousPage()
                    return true
                }
                return false
            }
        })
        webView.setOnTouchListener { _, ev ->
            gd.onTouchEvent(ev)
            false
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
                if (target != currentChapter) {
                    if (isTtsActive) { ttsService?.doStop(); stopTtsModeAndRestore() }
                    loadChapter(target, scrollY = 0)
                }
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
        val chapter = b.chapters[currentChapter]
        val html = if (isTtsActive) buildTtsHtml(chapter) else buildHtml(chapter)
        webView.loadDataWithBaseURL(null, html, "text/html", "UTF-8", null)
        saveProgress()
    }

    private fun buildHtml(chapter: com.lisb.reader.epub.EpubBook.Chapter): String {
        val theme = settings.theme
        val font = settings.fontFamily
        val size = settings.fontSizePx
        val lh = settings.lineHeight
        val ls = settings.letterSpacing
        val fc = settings.fontColor
        val fontColorRule = if (fc.isNotBlank()) "body,body *{color:$fc !important;}" else ""

        // Some EPUBs have TOC entries that point to chapters with empty
        // body markup (e.g. a wrapper file that only links elsewhere).
        // Without a fallback, the page renders blank and the user can
        // get stuck. Reuse the plain-text extract as a last resort.
        val rawBody = chapter.bodyHtml
        val strippedForCheck = rawBody.replace(Regex("<[^>]+>"), "").trim()
        val effectiveBody = if (strippedForCheck.isEmpty()) {
            val pt = chapter.plainText.trim()
            if (pt.isEmpty()) "<p style=\"text-align:center;color:#888;margin-top:2em;\">本章为空</p>"
            else "<div>" + htmlEscape(pt).replace("\n\n", "</p><p>").replace("\n", "<br>").let { "<p>$it</p>" } + "</div>"
        } else rawBody

        return if (settings.preserveEpubStyle) {
            val overlay = """
                <style id="lisb-theme-overlay">
                  html,body{${theme.css}}
                  html,body{overflow:hidden!important;height:100%!important;margin:0!important;padding:0!important;}
                  body{padding:0!important;}
                  #lisb-wrapper{height:100%;overflow:hidden;padding:0 20px;box-sizing:border-box;}
                  $fontColorRule
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
                $READER_JS
                </head>
                <body><div id="lisb-wrapper"><div id="lisb-content">${effectiveBody}</div></div></body></html>
            """.trimIndent()
        } else {
            val colorCss = if (fc.isNotBlank()) "color:$fc;" else ""
            """
                <!DOCTYPE html>
                <html><head><meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
                <style>
                  html,body{margin:0;padding:0;overflow:hidden;height:100%;${theme.css}}
                  body{font-family:${font.css};font-size:${size}px;line-height:$lh;
                       letter-spacing:${"%.3f".format(ls)}em;
                       padding:0;text-align:justify;word-wrap:break-word;$colorCss}
                  #lisb-wrapper{height:100%;overflow:hidden;padding:0 20px;box-sizing:border-box;}
                  p{margin:0 0 0.9em 0;text-indent:2em;}
                  h1,h2,h3{font-weight:600;margin:1em 0 0.6em;text-indent:0;letter-spacing:0;}
                  img{max-width:100%;height:auto;}
                  a{color:inherit;text-decoration:none;}
                </style>
                $READER_JS
                </head>
                <body><div id="lisb-wrapper"><div id="lisb-content">${effectiveBody}</div></div></body></html>
            """.trimIndent()
        }
    }

    /** Build a stripped-down, plain-text rendering of the chapter where each
     *  TTS chunk is wrapped in a span so we can highlight + auto-scroll to
     *  it. EPUB-original styling is intentionally dropped here — we trade it
     *  for reliable mapping between TTS sentences and on-screen positions. */
    private fun buildTtsHtml(chapter: com.lisb.reader.epub.EpubBook.Chapter): String {
        val theme = settings.theme
        val font = settings.fontFamily
        val size = settings.fontSizePx
        val lh = settings.lineHeight
        val ls = settings.letterSpacing
        val fc = settings.fontColor
        val colorCss = if (fc.isNotBlank()) "color:$fc;" else ""

        val chunks = TtsManager.splitForTts(chapter.plainText)
        val body = StringBuilder()
        body.append("<div class=\"tts-flow\">")
        chunks.forEachIndexed { i, c ->
            body.append("<span id=\"tts-").append(i).append("\" class=\"tts-chunk\">")
            body.append(htmlEscape(c).replace("\n", "<br>"))
            body.append("</span>")
            body.append(" ")
        }
        body.append("</div>")

        val highlightCss = when (theme) {
            SettingsManager.Theme.LIGHT, SettingsManager.Theme.SEPIA, SettingsManager.Theme.GREEN ->
                "background-color:rgba(255,213,79,0.55);color:#1A1A1A;"
            SettingsManager.Theme.DARK, SettingsManager.Theme.BLACK ->
                "background-color:rgba(255,213,79,0.35);color:#FFFFFF;"
        }

        return """
            <!DOCTYPE html>
            <html><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
            <style>
              html,body{margin:0;padding:0;overflow:hidden;height:100%;${theme.css}}
              body{font-family:${font.css};font-size:${size}px;line-height:$lh;
                   letter-spacing:${"%.3f".format(ls)}em;
                   padding:0;text-align:justify;word-wrap:break-word;$colorCss}
              #lisb-wrapper{height:100%;overflow:hidden;padding:0 20px;box-sizing:border-box;}
              .tts-chunk{transition:background-color .15s;border-radius:3px;padding:0 1px;}
              .tts-active{$highlightCss}
              img{max-width:100%;height:auto;}
            </style>
            $READER_JS
            <script>
              window.__lisbCur=null;
              function lisbHighlight(i){
                if(i<(window.__lisbMinChunk||0))return; // stale callback
                if(window.__lisbCur!=null){var p=document.getElementById('tts-'+window.__lisbCur);if(p)p.classList.remove('tts-active');}
                window.__lisbCur=i;
                var el=document.getElementById('tts-'+i);
                if(!el)return;
                el.classList.add('tts-active');
                if(window.__lisbPages==null&&typeof lisbBuildPages==='function')lisbBuildPages();
                var pages=window.__lisbPages||[0];
                var topAbs=el.offsetTop;
                var curIdx=(typeof lisbPageIndex==='function')?lisbPageIndex():0;
                var chunkIdx=0;
                for(var k=pages.length-1;k>=0;k--){if(pages[k]<=topAbs+2){chunkIdx=k;break;}}
                if(chunkIdx!==curIdx){lisbApplyY(pages[chunkIdx]);}
              }
              function lisbFirstChunkAt(y){
                var spans=document.getElementsByClassName('tts-chunk');
                for(var i=0;i<spans.length;i++){
                  var s=spans[i];
                  if(s.offsetTop+s.offsetHeight>y+2)return i;
                }
                return spans.length>0?spans.length-1:0;
              }
            </script>
            </head>
            <body><div id="lisb-wrapper"><div id="lisb-content">${body}</div></div></body></html>
        """.trimIndent()
    }

    private fun htmlEscape(s: String): String =
        s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    // ---- Pagination helpers ----

    /** Parse the "y|page|total" string produced by JS into our cached
     *  state and invoke the callback on the UI thread. */
    private fun applyPageInfo(raw: String?, after: (() -> Unit)? = null) {
        val v = raw?.trim('"') ?: return
        val parts = v.split('|')
        if (parts.size < 3) return
        val y = parts[0].toIntOrNull() ?: return
        val p = parts[1].toIntOrNull() ?: return
        val t = parts[2].toIntOrNull() ?: return
        currentVirtualY = y; currentPage = p; currentTotal = t
        after?.invoke()
    }

    private fun refreshPageInfo(after: (() -> Unit)? = null) {
        webView.evaluateJavascript("lisbPageInfo()") { raw ->
            applyPageInfo(raw, after)
        }
    }

    private fun totalPagesInChapter(): Int = currentTotal
    private fun currentPageInChapter(): Int = currentPage

    private fun goNextPage() {
        // During active TTS playback, page turn is blocked. User must pause
        // first. This avoids the complex state-sync issues (BUG 3 request).
        if (isTtsActive && ttsService?.playing == true) {
            ttsService?.doPause()
            Toast.makeText(this, "已暂停朗读，再次点击可翻页", Toast.LENGTH_SHORT).show()
            return
        }
        webView.evaluateJavascript("lisbScrollByPage(1)") { raw ->
            val v = raw?.trim('"') ?: return@evaluateJavascript
            if (v == "NEXT_CHAPTER") {
                val b = book ?: return@evaluateJavascript
                if (currentChapter < b.chapters.lastIndex) {
                    loadChapter(currentChapter + 1, scrollY = 0)
                } else {
                    Toast.makeText(this, "已是最后一页", Toast.LENGTH_SHORT).show()
                }
            } else {
                applyPageInfo(raw) {
                    saveProgress(); updateProgressText(); updateIndicators()
                }
            }
        }
    }

    private fun goPreviousPage() {
        if (isTtsActive && ttsService?.playing == true) {
            ttsService?.doPause()
            Toast.makeText(this, "已暂停朗读，再次点击可翻页", Toast.LENGTH_SHORT).show()
            return
        }
        webView.evaluateJavascript("lisbScrollByPage(-1)") { raw ->
            val v = raw?.trim('"') ?: return@evaluateJavascript
            if (v == "PREV_CHAPTER") {
                if (currentChapter > 0) {
                    loadChapter(currentChapter - 1, scrollY = Int.MAX_VALUE / 2)
                } else {
                    Toast.makeText(this, "已是第一页", Toast.LENGTH_SHORT).show()
                }
            } else {
                applyPageInfo(raw) {
                    saveProgress(); updateProgressText(); updateIndicators()
                }
            }
        }
    }

    private fun saveProgress() {
        val b = book ?: return
        settings.saveProgress(b.id, currentChapter, currentVirtualY)
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

        // Font color swatches: "默认" + a handful of presets. Selecting one
        // updates the live preview by reloading the chapter on dialog dismiss.
        val colorRow = dialogView.findViewById<android.widget.LinearLayout>(R.id.fontColorRow)
        val colorOptions = listOf(
            "" to "默认",
            "#1A1A1A" to "黑",
            "#3B2A1A" to "深棕",
            "#1F3A5F" to "靛",
            "#2E5E36" to "墨绿",
            "#7A2E2E" to "酒红",
            "#5C5C5C" to "灰"
        )
        colorOptions.forEach { (hex, label) ->
            val sw = android.widget.TextView(this).apply {
                text = label
                textSize = 12f
                setPadding(dp(10), dp(6), dp(10), dp(6))
                val lp = android.widget.LinearLayout.LayoutParams(
                    android.widget.LinearLayout.LayoutParams.WRAP_CONTENT,
                    android.widget.LinearLayout.LayoutParams.WRAP_CONTENT
                )
                lp.marginEnd = dp(8)
                layoutParams = lp
                setBackgroundColor(
                    if (settings.fontColor == hex) Color.parseColor("#33000000") else Color.TRANSPARENT
                )
                setTextColor(if (hex.isBlank()) Color.parseColor("#666666") else Color.parseColor(hex))
                setOnClickListener {
                    settings.fontColor = hex
                    // Refresh swatch highlights
                    for (j in 0 until colorRow.childCount) {
                        colorRow.getChildAt(j).setBackgroundColor(Color.TRANSPARENT)
                    }
                    setBackgroundColor(Color.parseColor("#33000000"))
                }
            }
            colorRow.addView(sw)
        }

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
                if (which != currentChapter && isTtsActive) {
                    ttsService?.doStop(); stopTtsModeAndRestore()
                }
                loadChapter(which, scrollY = 0); d.dismiss()
            }.setNegativeButton("关闭", null).show()
    }

    /** Handle a link click inside the WebView. EPUB internal links often
     *  look like "chapter3.xhtml#sec1" or just "#footnote5". We try to
     *  resolve them to one of the loaded chapters. */
    private fun handleInternalLink(url: String) {
        val b = book ?: return
        // Extract the file component (e.g. "chapter3.xhtml")
        val filePart = url.substringAfterLast('/').substringBefore('#')
        val fragment = if ('#' in url) url.substringAfter('#') else ""

        if (filePart.isBlank() && fragment.isNotBlank()) {
            // Pure in-page anchor like "#footnote". Scroll within current chapter.
            webView.evaluateJavascript("""
                (function(){
                  var el=document.getElementById('$fragment');
                  if(!el)return '-1';
                  if(window.__lisbPages==null&&typeof lisbBuildPages==='function')lisbBuildPages();
                  var pages=window.__lisbPages||[0];
                  var top=el.offsetTop;
                  var idx=0;
                  for(var i=pages.length-1;i>=0;i--){if(pages[i]<=top+2){idx=i;break;}}
                  if(typeof lisbApplyY==='function')lisbApplyY(pages[idx]);
                  return (typeof lisbPageInfo==='function')?lisbPageInfo():'-1';
                })()
            """.trimIndent()) { raw -> applyPageInfo(raw) { updateIndicators() } }
            return
        }

        // Try to match filePart against chapter sourceHref.
        // sourceHref looks like "Text/chapter3.xhtml" or "OEBPS/ch03.html"
        val filePartLower = filePart.lowercase()
        for ((i, ch) in b.chapters.withIndex()) {
            val chFile = ch.sourceHref.substringAfterLast('/').lowercase()
            if (chFile == filePartLower) {
                loadChapter(i, scrollY = 0)
                return
            }
        }
        // Also try partial match (without extension)
        val fileNoExt = filePartLower.substringBefore('.')
        if (fileNoExt.isNotBlank()) {
            for ((i, ch) in b.chapters.withIndex()) {
                val chFile = ch.sourceHref.substringAfterLast('/').lowercase().substringBefore('.')
                if (chFile == fileNoExt) {
                    loadChapter(i, scrollY = 0)
                    return
                }
            }
        }
        // If there's a fragment, search chapter bodyHtml for id="fragment"
        if (fragment.isNotBlank()) {
            for ((i, ch) in b.chapters.withIndex()) {
                if (ch.bodyHtml.contains("id=\"$fragment\"")) {
                    loadChapter(i, scrollY = 0)
                    return
                }
            }
        }
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

        // Menu transparency slider (0.30 .. 1.00 in 0.05 steps -> 0..14)
        val alphaSeek = view.findViewById<SeekBar>(R.id.menuAlphaSeek)
        val alphaLabel = view.findViewById<TextView>(R.id.menuAlphaLabel)
        alphaSeek.max = 14
        alphaSeek.progress = ((settings.menuBarAlpha - 0.3f) / 0.05f).toInt().coerceIn(0, 14)
        alphaLabel.text = "菜单透明度：${(settings.menuBarAlpha * 100).toInt()}%"
        alphaSeek.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                val v = (0.3f + p * 0.05f).coerceIn(0.3f, 1f)
                settings.menuBarAlpha = v
                alphaLabel.text = "菜单透明度：${(v * 100).toInt()}%"
                applyMenuBarAlpha()
            }
            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })

        AlertDialog.Builder(this).setTitle("亮度 / 菜单").setView(view)
            .setPositiveButton("确定", null)
            .setNeutralButton("跳随系统亮度") { _, _ -> settings.brightness = -1f; applyBrightness() }
            .show()
    }

    /** Apply current menuBarAlpha to top/bottom menu backgrounds while
     *  keeping their child text/icons fully opaque. */
    private fun applyMenuBarAlpha() {
        val a = (settings.menuBarAlpha * 255).toInt().coerceIn(0, 255)
        topBar.background = ColorDrawable(Color.parseColor("#202020")).apply { alpha = a }
        bottomBar.background = ColorDrawable(Color.parseColor("#202020")).apply { alpha = a }
    }

    private fun applyBrightness() {
        val lp = window.attributes
        val b = settings.brightness
        lp.screenBrightness = if (b < 0) WindowManager.LayoutParams.BRIGHTNESS_OVERRIDE_NONE else b.coerceIn(0.05f, 1f)
        window.attributes = lp
    }

    private fun reloadCurrentChapter() {
        loadChapter(currentChapter, scrollY = currentVirtualY)
    }

    // ==================== TTS ====================

    private fun toggleTts() {
        val svc = ttsService
        if (svc != null && svc.playing) {
            svc.doPause()
            Toast.makeText(this, "已暂停朗读", Toast.LENGTH_SHORT).show()
            return
        }
        if (isTtsActive && svc != null) {
            // TTS is loaded but paused — offer resume or restart from current page.
            val b = book ?: return
            val currentChunkIdx = currentPageChunkIndex(b.chapters[currentChapter].plainText)
            AlertDialog.Builder(this).setTitle("朗读已暂停")
                .setPositiveButton("继续上次位置") { _, _ -> svc.doResume() }
                .setNegativeButton("从当前页开始") { _, _ ->
                    startSpeakingHere(currentChapter, currentChunkIdx)
                }
                .setNeutralButton("停止朗读") { _, _ -> svc.doStop(); stopTtsModeAndRestore() }
                .show()
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
        // Compute total characters that appear BEFORE the current page.
        // This gives a much more accurate mapping than page-ratio because
        // page sizes vary (images, headings, margins).
        val charsBefore = plainText.length.toFloat() *
                (if (currentTotal <= 1) 0f else (currentPage - 1).toFloat() / currentTotal.toFloat())
        var acc = 0
        for (i in chunks.indices) {
            acc += chunks[i].length
            if (acc >= charsBefore) return i
        }
        return chunks.lastIndex
    }

    private fun startSpeakingHere(chapterIdx: Int, startChunk: Int) {
        val svc = ttsService ?: run {
            webView.postDelayed({ startSpeakingHere(chapterIdx, startChunk) }, 200)
            return
        }
        pushChaptersToServiceIfReady()
        val wasActive = isTtsActive
        isTtsActive = true
        if (chapterIdx != currentChapter) {
            // Loading a new chapter will rebuild it in TTS mode; defer the
            // actual start until onPageFinished fires.
            pendingTtsStartChunk = startChunk
            loadChapter(chapterIdx, scrollY = 0, skipTtsRestart = true)
            return
        }
        if (!wasActive) {
            // First-time switch into TTS mode: reload the current chapter
            // with chunk spans, then start speaking after onPageFinished.
            pendingTtsStartChunk = startChunk
            loadChapter(currentChapter, scrollY = currentVirtualY, skipTtsRestart = true)
            return
        }
        ContextCompat.startForegroundService(this, Intent(this, TtsService::class.java))
        svc.setRate(settings.ttsRate)
        webView.evaluateJavascript("lisbSetMinChunk($startChunk)", null)
        svc.startSpeaking(currentChapter, startChunk)
        Toast.makeText(this, "开始朗读", Toast.LENGTH_SHORT).show()
    }

    /** Service → Activity hook: a new chunk just started being spoken. */
    private fun onTtsChunkStarted(idx: Int) {
        if (!isTtsActive) return
        webView.evaluateJavascript("if(typeof lisbHighlight==='function')lisbHighlight($idx);", null)
    }

    private fun onChapterTtsFinished() {
        val b = book ?: return
        if (currentChapter < b.chapters.lastIndex) {
            // Service has already advanced its own pointer; just sync the UI.
            loadChapter(currentChapter + 1, scrollY = 0, skipTtsRestart = true)
        } else {
            Toast.makeText(this, "朗读完毕", Toast.LENGTH_SHORT).show()
            ttsService?.doStop()
            stopTtsModeAndRestore()
        }
    }

    /** Leave TTS mode and re-render the current chapter with normal styling. */
    private fun stopTtsModeAndRestore() {
        if (!isTtsActive) return
        isTtsActive = false
        loadChapter(currentChapter, scrollY = currentVirtualY, skipTtsRestart = true)
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
        // Going back to the bookshelf must stop ongoing TTS — it would be
        // confusing for the user to hear the previous book still being read.
        if (isFinishing) {
            try { ttsService?.doStop() } catch (_: Throwable) {}
            try { stopService(Intent(this, TtsService::class.java)) } catch (_: Throwable) {}
        }
        if (ttsBound) { try { unbindService(ttsConn) } catch (_: Throwable) {}; ttsBound = false }
    }

    companion object {
        const val EXTRA_BOOK_ID = "book_id"
        private const val REQ_NOTIF = 1101

        /** Script injected into every chapter's HTML so the WebView can do
         *  exact-line-aligned pagination (knows real computed line-height
         *  and snaps page boundaries to it, eliminating clipped half-lines). */
        private const val READER_JS = """
            <script>
              // Scroll-based pagination engine.
              // #lisb-wrapper (overflow:hidden, height:100%) is the scroll
              // container. We set wrapper.scrollTop programmatically to
              // navigate between pages. The wrapper's overflow:hidden clips
              // content at its exact boundary — no CSS transform needed.
              //
              // Pagination: enumerate every rendered text line via
              // Range.getClientRects(), then build a pages[] array by
              // stepping forward to the first line whose bottom exceeds
              // the wrapper's visible height.
              window.__lisbLh=null;
              window.__lisbY=0;
              window.__lisbLines=null;
              window.__lisbPages=null;
              window.__lisbMinChunk=0;
              function lisbC(){return document.getElementById('lisb-content');}
              function lisbW(){return document.getElementById('lisb-wrapper');}
              function lisbInit(){
                try{
                  var s=getComputedStyle(document.body);
                  var lh=s.lineHeight;
                  if(lh==='normal'){lh=parseFloat(s.fontSize)*1.3;}
                  else{lh=parseFloat(lh);}
                  if(!isFinite(lh)||lh<=0){lh=24;}
                  window.__lisbLh=lh;
                  var c=lisbC();
                  if(c){
                    c.style.paddingTop=Math.round(lh*0.5)+'px';
                    c.style.paddingBottom=Math.round(lh*0.5)+'px';
                    var fc=c.firstElementChild;
                    if(fc){fc.style.marginTop='0';fc.style.paddingTop='0';}
                    var lc=c.lastElementChild;
                    if(lc){lc.style.marginBottom='0';lc.style.paddingBottom='0';}
                  }
                  window.__lisbLines=null;
                  window.__lisbPages=null;
                }catch(e){window.__lisbLh=24;}
              }
              function lisbBuildLines(){
                var c=lisbC();
                var w=lisbW();
                if(!c||!w){window.__lisbLines=[];return;}
                // Scroll to top for measurement so getClientRects returns
                // absolute offsets from the wrapper's top edge.
                var savedScroll=w.scrollTop;
                w.scrollTop=0;
                var arr=[];
                try{
                  var walker=document.createTreeWalker(c,NodeFilter.SHOW_TEXT,null);
                  var n;
                  while(n=walker.nextNode()){
                    var v=n.nodeValue;
                    if(!v||!v.replace(/\s+/g,'').length)continue;
                    var r=document.createRange();
                    r.selectNodeContents(n);
                    var rects=r.getClientRects();
                    for(var i=0;i<rects.length;i++){
                      var rc=rects[i];
                      if(rc.width<1||rc.height<1)continue;
                      arr.push({t:Math.round(rc.top),b:Math.ceil(rc.bottom)});
                    }
                  }
                  var imgs=c.querySelectorAll('img');
                  for(var i=0;i<imgs.length;i++){
                    var rc=imgs[i].getBoundingClientRect();
                    if(rc.width<1||rc.height<1)continue;
                    arr.push({t:Math.round(rc.top),b:Math.ceil(rc.bottom)});
                  }
                }catch(e){}
                w.scrollTop=savedScroll;
                arr.sort(function(a,b){return a.t-b.t;});
                var out=[];
                for(var i=0;i<arr.length;i++){
                  if(out.length===0||arr[i].t-out[out.length-1].t>2){
                    out.push(arr[i]);
                  } else {
                    if(arr[i].b>out[out.length-1].b)out[out.length-1].b=arr[i].b;
                  }
                }
                window.__lisbLines=out;
              }
              function lisbBuildPages(){
                if(window.__lisbLh==null)lisbInit();
                if(window.__lisbLines==null)lisbBuildLines();
                var lines=window.__lisbLines||[];
                // Use wrapper.clientHeight as the exact visible area.
                // This is the same value that overflow:hidden clips at,
                // so there is zero discrepancy.
                var w=lisbW();
                var vh=w?w.clientHeight:window.innerHeight;
                var maxY=lisbMaxY();
                var pages=[0];
                if(maxY<=0){window.__lisbPages=pages;return;}
                var py=0,guard=0;
                while(guard++<99999){
                  var next=-1;
                  for(var i=0;i<lines.length;i++){
                    if(lines[i].t<=py)continue;
                    if(lines[i].b>py+vh){
                      next=lines[i].t;
                      break;
                    }
                  }
                  if(next<0||next<=py+1)break;
                  if(next>maxY){
                    if(pages[pages.length-1]<maxY)pages.push(maxY);
                    break;
                  }
                  pages.push(next);
                  py=next;
                }
                if(pages[pages.length-1]<maxY && maxY-pages[pages.length-1]>10){
                  pages.push(maxY);
                }
                window.__lisbPages=pages;
              }
              function lisbMaxY(){
                var w=lisbW();
                if(!w)return 0;
                return Math.max(0,w.scrollHeight-w.clientHeight);
              }
              function lisbApplyY(y){
                var maxY=lisbMaxY();
                if(y<0)y=0; if(y>maxY)y=maxY;
                window.__lisbY=y;
                var w=lisbW();
                if(w){
                  w.scrollTop=y;
                  // Cover any partially-visible line at the bottom that
                  // belongs to the next page. The mask is a fixed-position
                  // div matching the body background.
                  var mask=document.getElementById('lisb-page-mask');
                  if(!mask){
                    mask=document.createElement('div');
                    mask.id='lisb-page-mask';
                    mask.style.cssText='position:fixed;bottom:0;left:0;width:100%;pointer-events:none;z-index:98;';
                    document.body.appendChild(mask);
                  }
                  var pages=window.__lisbPages||[0];
                  var idx=lisbPageIndex();
                  if(idx<pages.length-1){
                    var nextY=pages[idx+1];
                    var gap=w.clientHeight-(nextY-y);
                    if(gap>0&&gap<w.clientHeight*0.4){
                      mask.style.height=gap+'px';
                      mask.style.backgroundColor=getComputedStyle(document.body).backgroundColor;
                      mask.style.display='block';
                    }else{
                      mask.style.display='none';
                    }
                  }else{
                    mask.style.display='none';
                  }
                }
              }
              function lisbSetY(y){
                if(window.__lisbPages==null)lisbBuildPages();
                lisbApplyY(y);
              }
              function lisbPageIndex(){
                if(window.__lisbPages==null)lisbBuildPages();
                var pages=window.__lisbPages;
                var y=window.__lisbY;
                var idx=0;
                for(var i=pages.length-1;i>=0;i--){
                  if(pages[i]<=y+2){idx=i;break;}
                }
                return idx;
              }
              function lisbPageInfo(){
                if(window.__lisbPages==null)lisbBuildPages();
                var pages=window.__lisbPages;
                var idx=lisbPageIndex();
                return window.__lisbY+'|'+(idx+1)+'|'+pages.length;
              }
              function lisbScrollByPage(dir){
                if(window.__lisbPages==null)lisbBuildPages();
                var pages=window.__lisbPages;
                var cur=lisbPageIndex();
                if(dir<0&&cur<=0)return 'PREV_CHAPTER';
                if(dir>0&&cur>=pages.length-1)return 'NEXT_CHAPTER';
                var ni=cur+dir;
                if(ni<0)ni=0; if(ni>pages.length-1)ni=pages.length-1;
                lisbApplyY(pages[ni]);
                return lisbPageInfo();
              }
              function lisbSetMinChunk(i){window.__lisbMinChunk=i|0;}
              function lisbInitAll(){
                lisbInit();
                lisbBuildLines();
                lisbBuildPages();
                lisbSetupImgZoom();
              }
              // --- Image click-to-zoom overlay with pinch-zoom ---
              window.__lisbZoomShown=false;
              function lisbIsZoomShown(){return window.__lisbZoomShown;}
              function lisbSetupImgZoom(){
                var overlay=document.getElementById('lisb-img-overlay');
                if(!overlay){
                  overlay=document.createElement('div');
                  overlay.id='lisb-img-overlay';
                  overlay.style.cssText='display:none;position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.92);z-index:999999;align-items:center;justify-content:center;touch-action:none;';
                  overlay.innerHTML='<img id=\"lisb-img-zoom\" style=\"max-width:95%;max-height:95%;object-fit:contain;transform-origin:center center;\"/>';
                  document.body.appendChild(overlay);
                  var scale=1,startDist=0,startScale=1;
                  function getDist(e){var t=e.touches;return Math.hypot(t[1].pageX-t[0].pageX,t[1].pageY-t[0].pageY);}
                  overlay.addEventListener('touchstart',function(e){
                    if(e.touches.length===2){startDist=getDist(e);startScale=scale;e.preventDefault();}
                  },{passive:false});
                  overlay.addEventListener('touchmove',function(e){
                    if(e.touches.length===2&&startDist>0){
                      var d=getDist(e);
                      scale=Math.max(1,Math.min(5,startScale*(d/startDist)));
                      var img=document.getElementById('lisb-img-zoom');
                      if(img)img.style.transform='scale('+scale+')';
                      e.preventDefault();
                    }
                  },{passive:false});
                  overlay.addEventListener('touchend',function(e){
                    if(e.touches.length<2)startDist=0;
                  });
                  overlay.addEventListener('click',function(){
                    scale=1;
                    var img=document.getElementById('lisb-img-zoom');
                    if(img)img.style.transform='scale(1)';
                    overlay.style.display='none';
                    window.__lisbZoomShown=false;
                  });
                }
                var imgs=document.querySelectorAll('#lisb-content img');
                for(var i=0;i<imgs.length;i++){
                  imgs[i].style.cursor='pointer';
                  imgs[i].addEventListener('click',function(e){
                    e.stopPropagation();
                    var ov=document.getElementById('lisb-img-overlay');
                    var z=document.getElementById('lisb-img-zoom');
                    z.src=this.src;
                    z.style.transform='scale(1)';
                    ov.style.display='flex';
                    window.__lisbZoomShown=true;
                  });
                }
              }
              window.addEventListener('load',function(){
                lisbInitAll();
                setTimeout(lisbInitAll,50);
              });
            </script>
        """
    }
}
