package com.lisb.reader.ui

import android.annotation.SuppressLint
import android.content.Context
import android.util.AttributeSet
import android.view.MotionEvent
import android.view.View
import android.view.ViewConfiguration
import kotlin.math.abs

/**
 * Transparent overlay above the WebView. Recognizes single taps (forwarded
 * to [onTap] with zone info) and horizontal swipes ([onSwipeLeft] for
 * next-page, [onSwipeRight] for prev-page). Long presses / vertical drags
 * are ignored so they don't fight WebView gestures.
 */
class TouchOverlay @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    var onTap: ((x: Float, y: Float, w: Int, h: Int) -> Unit)? = null
    var onSwipeLeft: (() -> Unit)? = null   // finger swipes from right to left
    var onSwipeRight: (() -> Unit)? = null  // finger swipes from left to right

    private var downX = 0f
    private var downY = 0f
    private var downTime = 0L

    private val density = resources.displayMetrics.density
    private val tapSlop = ViewConfiguration.get(context).scaledTouchSlop.toFloat()
    private val swipeMinDistPx = 60f * density       // need at least 60dp horizontal travel
    private val swipeMaxVerticalRatio = 0.6f         // |dy| must be < this * |dx|
    private val tapMaxDurationMs = 500L

    @SuppressLint("ClickableViewAccessibility")
    override fun onTouchEvent(event: MotionEvent): Boolean {
        when (event.actionMasked) {
            MotionEvent.ACTION_DOWN -> {
                downX = event.x; downY = event.y
                downTime = System.currentTimeMillis()
                return true
            }
            MotionEvent.ACTION_UP -> {
                val dx = event.x - downX
                val dy = event.y - downY
                val adx = abs(dx); val ady = abs(dy)
                val elapsed = System.currentTimeMillis() - downTime

                // Tap?
                if (adx < tapSlop && ady < tapSlop && elapsed < tapMaxDurationMs) {
                    onTap?.invoke(event.x, event.y, width, height)
                    performClick()
                    return true
                }

                // Horizontal swipe?
                if (adx >= swipeMinDistPx && ady < adx * swipeMaxVerticalRatio) {
                    if (dx < 0) onSwipeLeft?.invoke() else onSwipeRight?.invoke()
                    return true
                }
                return true
            }
            MotionEvent.ACTION_CANCEL -> return true
        }
        return false
    }

    override fun performClick(): Boolean {
        super.performClick()
        return true
    }
}
