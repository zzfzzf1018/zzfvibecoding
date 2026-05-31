package com.lisb.reader.tts

import android.content.Context
import android.os.Bundle
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import java.util.Locale

/**
 * Wrapper around [TextToSpeech] that:
 *  - splits a chapter's plain text into ~180-char sentence-aligned chunks
 *  - speaks from an arbitrary starting chunk index
 *  - supports pause / resume by stop()ing and re-enqueueing remaining chunks
 *  - reports chunk-by-chunk progress so listeners can persist it
 */
class TtsManager(context: Context) {

    private var tts: TextToSpeech? = null
    private var ready = false
    private var pendingRate: Float = 1f

    /** Latest chunks loaded via [speak]. */
    var chunks: List<String> = emptyList()
        private set
    /** Index of the chunk that should play next if [resume] is called. */
    var nextIndex: Int = 0
        private set
    var idPrefix: String = ""
        private set

    val chunkCount: Int get() = chunks.size

    // Callbacks
    var onChunkStart: ((index: Int) -> Unit)? = null
    var onChunkDone:  ((index: Int) -> Unit)? = null
    var onAllFinished: (() -> Unit)? = null
    var onReady: (() -> Unit)? = null

    init {
        tts = TextToSpeech(context.applicationContext) { status ->
            if (status == TextToSpeech.SUCCESS) {
                ready = true
                tts?.language = Locale.CHINA
                tts?.setSpeechRate(pendingRate)
                tts?.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
                    override fun onStart(utteranceId: String) {
                        parseIndex(utteranceId)?.let { idx -> onChunkStart?.invoke(idx) }
                    }
                    override fun onDone(utteranceId: String) {
                        val idx = parseIndex(utteranceId) ?: return
                        nextIndex = idx + 1
                        onChunkDone?.invoke(idx)
                        if (nextIndex >= chunks.size) onAllFinished?.invoke()
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

    /**
     * Split [text] into chunks and start speaking from [startIndex].
     * Pass the same [idPrefix] for a given chapter so resumes / re-speaks line up.
     */
    fun speak(text: String, idPrefix: String, startIndex: Int = 0) {
        val t = tts ?: return
        if (!ready) return
        this.idPrefix = idPrefix
        chunks = splitForTts(text)
        if (chunks.isEmpty()) { onAllFinished?.invoke(); return }
        val start = startIndex.coerceIn(0, chunks.lastIndex)
        nextIndex = start
        t.stop()
        enqueueFromNextIndex()
    }

    /** Stop speaking without losing position, so [resume] can continue. */
    fun pause() {
        // Remember next chunk to play (already maintained by onDone). If we pause
        // mid-utterance, that utterance won't fire onDone, so nextIndex still
        // points at it — re-speaking that chunk on resume is fine.
        tts?.stop()
    }

    fun resume() {
        if (!ready || chunks.isEmpty()) return
        if (nextIndex >= chunks.size) { onAllFinished?.invoke(); return }
        enqueueFromNextIndex()
    }

    fun stop() {
        tts?.stop()
        chunks = emptyList()
        nextIndex = 0
    }

    fun shutdown() {
        tts?.stop(); tts?.shutdown(); tts = null
    }

    private fun enqueueFromNextIndex() {
        val t = tts ?: return
        for (i in nextIndex until chunks.size) {
            val params = Bundle()
            t.speak(chunks[i], TextToSpeech.QUEUE_ADD, params, "$idPrefix#$i")
        }
    }

    private fun parseIndex(utteranceId: String): Int? {
        val hash = utteranceId.lastIndexOf('#')
        if (hash < 0) return null
        return utteranceId.substring(hash + 1).toIntOrNull()
    }

    companion object {
        /** Split into ~maxLen char chunks aligned to sentence boundaries. */
        fun splitForTts(text: String, maxLen: Int = 180): List<String> {
            val clean = text.trim()
            if (clean.isEmpty()) return emptyList()
            if (clean.length <= maxLen) return listOf(clean)
            val out = mutableListOf<String>()
            val buf = StringBuilder()
            for (ch in clean) {
                buf.append(ch)
                val isBreak = ch == '。' || ch == '！' || ch == '？' || ch == '\n' ||
                              ch == '.' || ch == '!' || ch == '?'
                if (buf.length >= maxLen && isBreak) {
                    out.add(buf.toString().trim())
                    buf.clear()
                }
            }
            if (buf.isNotBlank()) out.add(buf.toString().trim())
            return out
        }
    }
}
