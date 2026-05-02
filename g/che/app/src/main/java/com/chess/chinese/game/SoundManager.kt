package com.chess.chinese.game

import android.content.Context
import android.media.AudioAttributes
import android.media.SoundPool

/**
 * 音效管理器
 */
class SoundManager(context: Context) {

    private val soundPool: SoundPool
    private var soundMove: Int = 0
    private var soundCapture: Int = 0
    private var soundCheck: Int = 0
    private var soundSelect: Int = 0
    private var soundGameOver: Int = 0
    private var enabled: Boolean = true

    init {
        val attrs = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()
        soundPool = SoundPool.Builder()
            .setMaxStreams(4)
            .setAudioAttributes(attrs)
            .build()

        // 使用系统内置音效生成简单音效
        // 由于没有实际音频文件，我们使用ToneGenerator替代
    }

    fun setEnabled(enabled: Boolean) {
        this.enabled = enabled
    }

    fun playMove() {
        if (!enabled) return
        playTone(800, 80)
    }

    fun playCapture() {
        if (!enabled) return
        playTone(600, 120)
    }

    fun playCheck() {
        if (!enabled) return
        playTone(1000, 150)
        playTone(1200, 150)
    }

    fun playSelect() {
        if (!enabled) return
        playTone(1000, 50)
    }

    fun playGameOver() {
        if (!enabled) return
        playTone(400, 200)
    }

    private fun playTone(frequencyHz: Int, durationMs: Int) {
        try {
            val toneGen = android.media.ToneGenerator(
                android.media.AudioManager.STREAM_MUSIC, 80
            )
            val tone = when {
                frequencyHz >= 1000 -> android.media.ToneGenerator.TONE_PROP_BEEP
                frequencyHz >= 700 -> android.media.ToneGenerator.TONE_PROP_ACK
                else -> android.media.ToneGenerator.TONE_PROP_NACK
            }
            toneGen.startTone(tone, durationMs)
            Thread {
                Thread.sleep(durationMs.toLong() + 50)
                toneGen.release()
            }.start()
        } catch (_: Exception) {}
    }

    fun release() {
        soundPool.release()
    }
}
