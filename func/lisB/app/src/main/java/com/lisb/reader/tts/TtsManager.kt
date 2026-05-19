package com.lisb.reader.tts

import android.content.Context
import android.os.Bundle
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import java.util.Locale

/**
 * Thin wrapper around [TextToSpeech] that speaks a queue of utterances and
 * fires [onChunkFinished] when each finishes (used to auto turn the page).
 */
class TtsManager(context: Context) {

    private var tts: TextToSpeech? = null
    private var ready = false
    private var pendingRate: Float = 1f
    var onChunkFinished: ((utteranceId: String) -> Unit)? = null
    var onAllFinished: (() -> Unit)? = null
    var onReady: (() -> Unit)? = null

    private var totalQueued = 0
    private var completed = 0

    init {
        tts = TextToSpeech(context.applicationContext) { status ->
            if (status == TextToSpeech.SUCCESS) {
                ready = true
                tts?.language = Locale.CHINA
                tts?.setSpeechRate(pendingRate)
                tts?.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
                    override fun onStart(utteranceId: String) {}
                    override fun onDone(utteranceId: String) {
                        completed++
                        onChunkFinished?.invoke(utteranceId)
                        if (completed >= totalQueued) onAllFinished?.invoke()
                    }
                    @Deprecated("Deprecated in Java")
                    override fun onError(utteranceId: String) {}
                })
                onReady?.invoke()
            }
        }
    }

    val isReady get() = ready
    val isSpeaking: Boolean get() = tts?.isSpeaking == true

    fun setRate(rate: Float) {
        pendingRate = rate
        if (ready) tts?.setSpeechRate(rate)
    }

    /** Splits text into ~200-char chunks and enqueues. */
    fun speak(text: String, idPrefix: String) {
        val t = tts ?: return
        if (!ready) return
        t.stop()
        totalQueued = 0; completed = 0
        val chunks = splitForTts(text)
        chunks.forEachIndexed { i, c ->
            val params = Bundle()
            t.speak(c, TextToSpeech.QUEUE_ADD, params, "$idPrefix#$i")
            totalQueued++
        }
    }

    fun stop() { tts?.stop() }

    fun shutdown() {
        tts?.stop(); tts?.shutdown(); tts = null
    }

    private fun splitForTts(text: String, maxLen: Int = 180): List<String> {
        if (text.length <= maxLen) return listOf(text)
        val out = mutableListOf<String>()
        val buf = StringBuilder()
        for (ch in text) {
            buf.append(ch)
            val isBreak = ch == '。' || ch == '！' || ch == '？' || ch == '.' || ch == '!' || ch == '?' || ch == '\n'
            if (buf.length >= maxLen && isBreak) {
                out.add(buf.toString().trim()); buf.clear()
            }
        }
        if (buf.isNotBlank()) out.add(buf.toString().trim())
        return out
    }
}
