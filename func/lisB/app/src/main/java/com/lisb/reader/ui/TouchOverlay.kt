package com.lisb.reader.ui

import android.annotation.SuppressLint
import android.content.Context
import android.util.AttributeSet
import android.view.MotionEvent
import android.view.View

/**
 * Transparent overlay that turns single taps into page/menu actions.
 * Long presses and drags fall through so WebView can still receive them
 * (e.g. selection, scroll).
 */
class TouchOverlay @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    var onTap: ((x: Float, y: Float, w: Int, h: Int) -> Unit)? = null

    private var downX = 0f
    private var downY = 0f
    private var downTime = 0L

    @SuppressLint("ClickableViewAccessibility")
    override fun onTouchEvent(event: MotionEvent): Boolean {
        when (event.actionMasked) {
            MotionEvent.ACTION_DOWN -> {
                downX = event.x
                downY = event.y
                downTime = System.currentTimeMillis()
                return true
            }
            MotionEvent.ACTION_UP -> {
                val dx = event.x - downX
                val dy = event.y - downY
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

    override fun performClick(): Boolean {
        super.performClick()
        return true
    }
}
