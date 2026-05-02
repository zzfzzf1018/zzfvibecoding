package com.chess.chinese.ui

import android.animation.ValueAnimator
import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.RectF
import android.util.AttributeSet
import android.view.MotionEvent
import android.view.View
import android.view.animation.DecelerateInterpolator
import com.chess.chinese.game.*

/**
 * 象棋棋盘视图 - 支持动画、翻转、主题
 */
class ChessBoardView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0
) : View(context, attrs, defStyleAttr) {

    var gameManager: GameManager? = null
        set(value) {
            field = value
            value?.onBoardChanged = { post { invalidate() } }
            invalidate()
        }

    // 是否翻转棋盘
    var isFlipped: Boolean = false
        set(value) {
            field = value
            invalidate()
        }

    // 当前主题
    var theme: BoardTheme = BoardTheme.CLASSIC
        set(value) {
            field = value
            updateThemeColors()
            invalidate()
        }

    // 提示走法
    var hintMove: Move? = null
        set(value) {
            field = value
            invalidate()
        }

    // 动画相关
    private var animating = false
    private var animFromRow = 0
    private var animFromCol = 0
    private var animToRow = 0
    private var animToCol = 0
    private var animProgress = 1f
    private var animPiece: Piece? = null

    // 绘制参数
    private var cellSize = 0f
    private var boardLeft = 0f
    private var boardTop = 0f
    private var pieceRadius = 0f

    // 画笔
    private val boardPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        strokeWidth = 2f
        style = Paint.Style.STROKE
    }

    private val boardBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }

    private val pieceBorderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        strokeWidth = 3f
        style = Paint.Style.STROKE
    }

    private val pieceTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textAlign = Paint.Align.CENTER
        isFakeBoldText = true
    }

    private val blackTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textAlign = Paint.Align.CENTER
        isFakeBoldText = true
    }

    private val selectedPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }

    private val validMovePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }

    private val lastMovePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#44FF6600")
        style = Paint.Style.FILL
    }

    private val riverTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textAlign = Paint.Align.CENTER
        isFakeBoldText = true
    }

    private val pieceBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }

    private val hintPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#660088FF")
        style = Paint.Style.STROKE
        strokeWidth = 5f
    }

    init {
        updateThemeColors()
    }

    private fun updateThemeColors() {
        boardPaint.color = Color.parseColor(theme.boardLineColor)
        boardBgPaint.color = Color.parseColor(theme.boardBgColor)
        pieceBgPaint.color = Color.parseColor(theme.pieceBgColor)
        selectedPaint.color = Color.parseColor(theme.selectedColor)
        validMovePaint.color = Color.parseColor(theme.validMoveColor)
        riverTextPaint.color = Color.parseColor(theme.boardLineColor)
    }

    /**
     * 播放棋子移动动画
     */
    fun animateMove(fromRow: Int, fromCol: Int, toRow: Int, toCol: Int, piece: Piece, onComplete: () -> Unit) {
        animFromRow = fromRow
        animFromCol = fromCol
        animToRow = toRow
        animToCol = toCol
        animPiece = piece
        animating = true

        val animator = ValueAnimator.ofFloat(0f, 1f)
        animator.duration = 250
        animator.interpolator = DecelerateInterpolator()
        animator.addUpdateListener { anim ->
            animProgress = anim.animatedValue as Float
            invalidate()
        }
        animator.addListener(object : android.animation.AnimatorListenerAdapter() {
            override fun onAnimationEnd(animation: android.animation.Animator) {
                animating = false
                animPiece = null
                onComplete()
                invalidate()
            }
        })
        animator.start()
    }

    override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
        val width = MeasureSpec.getSize(widthMeasureSpec)
        val height = (width * 10f / 9f * 1.05f).toInt()
        setMeasuredDimension(width, height)
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)
        calculateDimensions()
    }

    private fun calculateDimensions() {
        val padding = width * 0.04f
        cellSize = (width - padding * 2) / 8f
        boardLeft = padding
        boardTop = (height - cellSize * 9) / 2f
        pieceRadius = cellSize * 0.42f
        pieceTextPaint.textSize = pieceRadius * 1.2f
        blackTextPaint.textSize = pieceRadius * 1.2f
        riverTextPaint.textSize = cellSize * 0.5f
    }

    // 坐标变换：逻辑坐标 -> 屏幕坐标
    private fun toScreenX(col: Int): Float {
        val c = if (isFlipped) 8 - col else col
        return boardLeft + c * cellSize
    }

    private fun toScreenY(row: Int): Float {
        val r = if (isFlipped) 9 - row else row
        return boardTop + r * cellSize
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        drawBoard(canvas)
        drawHighlights(canvas)
        drawPieces(canvas)
        drawAnimation(canvas)
        drawHint(canvas)
    }

    private fun drawBoard(canvas: Canvas) {
        val bgRect = RectF(
            boardLeft - cellSize * 0.3f,
            boardTop - cellSize * 0.3f,
            boardLeft + cellSize * 8 + cellSize * 0.3f,
            boardTop + cellSize * 9 + cellSize * 0.3f
        )
        canvas.drawRoundRect(bgRect, 10f, 10f, boardBgPaint)

        boardPaint.strokeWidth = 4f
        canvas.drawRect(
            boardLeft, boardTop,
            boardLeft + cellSize * 8, boardTop + cellSize * 9,
            boardPaint
        )
        boardPaint.strokeWidth = 2f

        for (c in 0..8) {
            val x = boardLeft + c * cellSize
            if (c == 0 || c == 8) {
                canvas.drawLine(x, boardTop, x, boardTop + cellSize * 9, boardPaint)
            } else {
                canvas.drawLine(x, boardTop, x, boardTop + cellSize * 4, boardPaint)
                canvas.drawLine(x, boardTop + cellSize * 5, x, boardTop + cellSize * 9, boardPaint)
            }
        }

        for (r in 0..9) {
            val y = boardTop + r * cellSize
            canvas.drawLine(boardLeft, y, boardLeft + cellSize * 8, y, boardPaint)
        }

        // 九宫格斜线
        canvas.drawLine(
            boardLeft + 3 * cellSize, boardTop,
            boardLeft + 5 * cellSize, boardTop + 2 * cellSize, boardPaint
        )
        canvas.drawLine(
            boardLeft + 5 * cellSize, boardTop,
            boardLeft + 3 * cellSize, boardTop + 2 * cellSize, boardPaint
        )
        canvas.drawLine(
            boardLeft + 3 * cellSize, boardTop + 7 * cellSize,
            boardLeft + 5 * cellSize, boardTop + 9 * cellSize, boardPaint
        )
        canvas.drawLine(
            boardLeft + 5 * cellSize, boardTop + 7 * cellSize,
            boardLeft + 3 * cellSize, boardTop + 9 * cellSize, boardPaint
        )

        // 楚河汉界
        val riverY = boardTop + cellSize * 4.5f
        if (isFlipped) {
            canvas.drawText("汉 界", boardLeft + cellSize * 2, riverY + riverTextPaint.textSize / 3, riverTextPaint)
            canvas.drawText("楚 河", boardLeft + cellSize * 6, riverY + riverTextPaint.textSize / 3, riverTextPaint)
        } else {
            canvas.drawText("楚 河", boardLeft + cellSize * 2, riverY + riverTextPaint.textSize / 3, riverTextPaint)
            canvas.drawText("汉 界", boardLeft + cellSize * 6, riverY + riverTextPaint.textSize / 3, riverTextPaint)
        }
    }

    private fun drawHighlights(canvas: Canvas) {
        val gm = gameManager ?: return

        gm.lastMove?.let { move ->
            val fromX = toScreenX(move.fromCol)
            val fromY = toScreenY(move.fromRow)
            canvas.drawCircle(fromX, fromY, pieceRadius, lastMovePaint)

            val toX = toScreenX(move.toCol)
            val toY = toScreenY(move.toRow)
            canvas.drawCircle(toX, toY, pieceRadius, lastMovePaint)
        }

        if (gm.selectedRow >= 0 && gm.selectedCol >= 0) {
            val x = toScreenX(gm.selectedCol)
            val y = toScreenY(gm.selectedRow)
            canvas.drawCircle(x, y, pieceRadius + 4, selectedPaint)

            val validTargets = gm.getValidMoveTargets()
            for ((tr, tc) in validTargets) {
                val tx = toScreenX(tc)
                val ty = toScreenY(tr)
                canvas.drawCircle(tx, ty, pieceRadius * 0.35f, validMovePaint)
            }
        }
    }

    private fun drawPieces(canvas: Canvas) {
        val gm = gameManager ?: return
        val board = gm.board

        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                // 动画中的棋子目标位置不绘制（由drawAnimation绘制）
                if (animating && r == animToRow && c == animToCol) continue

                val piece = board.getPiece(r, c) ?: continue
                val x = toScreenX(c)
                val y = toScreenY(r)
                drawSinglePiece(canvas, piece, x, y)
            }
        }
    }

    private fun drawAnimation(canvas: Canvas) {
        if (!animating || animPiece == null) return
        val fromX = toScreenX(animFromCol)
        val fromY = toScreenY(animFromRow)
        val toX = toScreenX(animToCol)
        val toY = toScreenY(animToRow)

        val curX = fromX + (toX - fromX) * animProgress
        val curY = fromY + (toY - fromY) * animProgress
        drawSinglePiece(canvas, animPiece!!, curX, curY)
    }

    private fun drawHint(canvas: Canvas) {
        val hint = hintMove ?: return
        val fromX = toScreenX(hint.fromCol)
        val fromY = toScreenY(hint.fromRow)
        val toX = toScreenX(hint.toCol)
        val toY = toScreenY(hint.toRow)

        canvas.drawCircle(fromX, fromY, pieceRadius + 6, hintPaint)
        canvas.drawCircle(toX, toY, pieceRadius + 6, hintPaint)
        canvas.drawLine(fromX, fromY, toX, toY, hintPaint)
    }

    private fun drawSinglePiece(canvas: Canvas, piece: Piece, x: Float, y: Float) {
        canvas.drawCircle(x, y, pieceRadius, pieceBgPaint)

        pieceBorderPaint.color = if (piece.color == PieceColor.RED)
            Color.parseColor(theme.redPieceColor) else Color.parseColor(theme.blackPieceColor)
        canvas.drawCircle(x, y, pieceRadius, pieceBorderPaint)
        canvas.drawCircle(x, y, pieceRadius - 4, pieceBorderPaint)

        val textPaint = if (piece.color == PieceColor.RED) {
            pieceTextPaint.apply { color = Color.parseColor(theme.redPieceColor) }
        } else {
            blackTextPaint.apply { color = Color.parseColor(theme.blackPieceColor) }
        }
        val textY = y - (textPaint.descent() + textPaint.ascent()) / 2
        canvas.drawText(piece.getDisplayName(), x, textY, textPaint)
    }

    override fun onTouchEvent(event: MotionEvent): Boolean {
        if (event.action == MotionEvent.ACTION_DOWN) {
            if (animating) return true

            val touchCol = ((event.x - boardLeft + cellSize / 2) / cellSize).toInt()
            val touchRow = ((event.y - boardTop + cellSize / 2) / cellSize).toInt()

            if (touchRow in 0..9 && touchCol in 0..8) {
                // 逆变换：屏幕坐标 -> 逻辑坐标
                val logicRow = if (isFlipped) 9 - touchRow else touchRow
                val logicCol = if (isFlipped) 8 - touchCol else touchCol
                hintMove = null  // 清除提示
                gameManager?.onCellClicked(logicRow, logicCol)
            }
            return true
        }
        return super.onTouchEvent(event)
    }
}
