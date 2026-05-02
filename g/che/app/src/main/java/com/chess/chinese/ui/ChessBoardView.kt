package com.chess.chinese.ui

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.RectF
import android.util.AttributeSet
import android.view.MotionEvent
import android.view.View
import com.chess.chinese.game.ChessBoard
import com.chess.chinese.game.GameManager
import com.chess.chinese.game.PieceColor

/**
 * 象棋棋盘视图
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

    // 绘制参数
    private var cellSize = 0f
    private var boardLeft = 0f
    private var boardTop = 0f
    private var pieceRadius = 0f

    // 画笔
    private val boardPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#8B4513")
        strokeWidth = 2f
        style = Paint.Style.STROKE
    }

    private val boardBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#F5DEB3")
        style = Paint.Style.FILL
    }

    private val redPiecePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#CC0000")
        style = Paint.Style.FILL
    }

    private val blackPiecePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#1A1A1A")
        style = Paint.Style.FILL
    }

    private val pieceBorderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#8B4513")
        strokeWidth = 3f
        style = Paint.Style.STROKE
    }

    private val pieceTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textAlign = Paint.Align.CENTER
        isFakeBoldText = true
    }

    private val blackTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#1A1A1A")
        textAlign = Paint.Align.CENTER
        isFakeBoldText = true
    }

    private val selectedPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#4400FF00")
        style = Paint.Style.FILL
    }

    private val validMovePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#6600AA00")
        style = Paint.Style.FILL
    }

    private val lastMovePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#44FF6600")
        style = Paint.Style.FILL
    }

    private val riverTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#8B4513")
        textAlign = Paint.Align.CENTER
        isFakeBoldText = true
    }

    private val pieceBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#FFEEDD")
        style = Paint.Style.FILL
    }

    override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
        val width = MeasureSpec.getSize(widthMeasureSpec)
        // 棋盘高宽比约 10:9
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

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        drawBoard(canvas)
        drawHighlights(canvas)
        drawPieces(canvas)
    }

    private fun drawBoard(canvas: Canvas) {
        // 背景
        val bgRect = RectF(
            boardLeft - cellSize * 0.3f,
            boardTop - cellSize * 0.3f,
            boardLeft + cellSize * 8 + cellSize * 0.3f,
            boardTop + cellSize * 9 + cellSize * 0.3f
        )
        canvas.drawRoundRect(bgRect, 10f, 10f, boardBgPaint)

        // 外框
        boardPaint.strokeWidth = 4f
        canvas.drawRect(
            boardLeft, boardTop,
            boardLeft + cellSize * 8, boardTop + cellSize * 9,
            boardPaint
        )
        boardPaint.strokeWidth = 2f

        // 竖线
        for (c in 0..8) {
            val x = boardLeft + c * cellSize
            if (c == 0 || c == 8) {
                canvas.drawLine(x, boardTop, x, boardTop + cellSize * 9, boardPaint)
            } else {
                // 上半部分
                canvas.drawLine(x, boardTop, x, boardTop + cellSize * 4, boardPaint)
                // 下半部分
                canvas.drawLine(x, boardTop + cellSize * 5, x, boardTop + cellSize * 9, boardPaint)
            }
        }

        // 横线
        for (r in 0..9) {
            val y = boardTop + r * cellSize
            canvas.drawLine(boardLeft, y, boardLeft + cellSize * 8, y, boardPaint)
        }

        // 九宫格斜线
        // 上方九宫
        canvas.drawLine(
            boardLeft + 3 * cellSize, boardTop,
            boardLeft + 5 * cellSize, boardTop + 2 * cellSize, boardPaint
        )
        canvas.drawLine(
            boardLeft + 5 * cellSize, boardTop,
            boardLeft + 3 * cellSize, boardTop + 2 * cellSize, boardPaint
        )
        // 下方九宫
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
        canvas.drawText("楚 河", boardLeft + cellSize * 2, riverY + riverTextPaint.textSize / 3, riverTextPaint)
        canvas.drawText("汉 界", boardLeft + cellSize * 6, riverY + riverTextPaint.textSize / 3, riverTextPaint)
    }

    private fun drawHighlights(canvas: Canvas) {
        val gm = gameManager ?: return

        // 上一步走法高亮
        gm.lastMove?.let { move ->
            val fromX = boardLeft + move.fromCol * cellSize
            val fromY = boardTop + move.fromRow * cellSize
            canvas.drawCircle(fromX, fromY, pieceRadius, lastMovePaint)

            val toX = boardLeft + move.toCol * cellSize
            val toY = boardTop + move.toRow * cellSize
            canvas.drawCircle(toX, toY, pieceRadius, lastMovePaint)
        }

        // 选中高亮
        if (gm.selectedRow >= 0 && gm.selectedCol >= 0) {
            val x = boardLeft + gm.selectedCol * cellSize
            val y = boardTop + gm.selectedRow * cellSize
            canvas.drawCircle(x, y, pieceRadius + 4, selectedPaint)

            // 合法走法提示
            val validTargets = gm.getValidMoveTargets()
            for ((tr, tc) in validTargets) {
                val tx = boardLeft + tc * cellSize
                val ty = boardTop + tr * cellSize
                canvas.drawCircle(tx, ty, pieceRadius * 0.35f, validMovePaint)
            }
        }
    }

    private fun drawPieces(canvas: Canvas) {
        val gm = gameManager ?: return
        val board = gm.board

        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                val piece = board.getPiece(r, c) ?: continue
                val x = boardLeft + c * cellSize
                val y = boardTop + r * cellSize

                // 棋子底色
                canvas.drawCircle(x, y, pieceRadius, pieceBgPaint)

                // 棋子边框
                pieceBorderPaint.color = if (piece.color == PieceColor.RED)
                    Color.parseColor("#CC0000") else Color.parseColor("#1A1A1A")
                canvas.drawCircle(x, y, pieceRadius, pieceBorderPaint)
                canvas.drawCircle(x, y, pieceRadius - 4, pieceBorderPaint)

                // 棋子文字
                val textPaint = if (piece.color == PieceColor.RED) {
                    pieceTextPaint.apply { color = Color.parseColor("#CC0000") }
                } else {
                    blackTextPaint
                }
                val textY = y - (textPaint.descent() + textPaint.ascent()) / 2
                canvas.drawText(piece.getDisplayName(), x, textY, textPaint)
            }
        }
    }

    override fun onTouchEvent(event: MotionEvent): Boolean {
        if (event.action == MotionEvent.ACTION_DOWN) {
            val col = ((event.x - boardLeft + cellSize / 2) / cellSize).toInt()
            val row = ((event.y - boardTop + cellSize / 2) / cellSize).toInt()

            if (row in 0..9 && col in 0..8) {
                gameManager?.onCellClicked(row, col)
            }
            return true
        }
        return super.onTouchEvent(event)
    }
}
