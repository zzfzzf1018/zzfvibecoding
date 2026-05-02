package com.chess.chinese.game

/**
 * AI难度级别
 */
enum class AIDifficulty(val depth: Int, val displayName: String) {
    EASY(2, "简单"),
    MEDIUM(3, "中等"),
    HARD(4, "困难")
}

/**
 * 象棋AI引擎 - 使用Minimax + Alpha-Beta剪枝
 */
class ChessAI(private val difficulty: AIDifficulty) {

    private val rules = ChessRules()

    // 棋子基础价值
    private val pieceValues = mapOf(
        PieceType.KING to 10000,
        PieceType.ROOK to 900,
        PieceType.CANNON to 450,
        PieceType.HORSE to 400,
        PieceType.ELEPHANT to 200,
        PieceType.ADVISOR to 200,
        PieceType.PAWN to 100
    )

    // 棋子位置价值表 (对于黑方，从上到下)
    private val kingPositionBlack = arrayOf(
        intArrayOf(0, 0, 0, 1, 5, 1, 0, 0, 0),
        intArrayOf(0, 0, 0, -8, -8, -8, 0, 0, 0),
        intArrayOf(0, 0, 0, -9, -9, -9, 0, 0, 0)
    )

    private val advisorPositionBlack = arrayOf(
        intArrayOf(0, 0, 0, 3, 0, 3, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 3, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 0, 0, 0, 0, 0)
    )

    private val pawnPositionBlack = arrayOf(
        intArrayOf(0, 0, 0, 0, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, -2, 0, 4, 0, -2, 0, 0),
        intArrayOf(2, 0, 8, 0, 8, 0, 8, 0, 2),
        intArrayOf(0, 12, 16, 20, 20, 20, 16, 12, 0),
        intArrayOf(10, 20, 30, 34, 40, 34, 30, 20, 10),
        intArrayOf(20, 30, 50, 65, 70, 65, 50, 30, 20),
        intArrayOf(20, 30, 50, 55, 60, 55, 50, 30, 20),
        intArrayOf(0, 0, 0, 0, 0, 0, 0, 0, 0)
    )

    /**
     * 获取AI最佳走法
     */
    fun getBestMove(board: ChessBoard, aiColor: PieceColor): Move? {
        val moves = rules.generateAllMoves(board, aiColor)
        if (moves.isEmpty()) return null

        var bestMove: Move? = null
        var bestScore = Int.MIN_VALUE

        // 走法排序优化（吃子优先）
        val sortedMoves = moves.sortedByDescending { move ->
            if (move.capturedPiece != null) pieceValues[move.capturedPiece.type] ?: 0 else 0
        }

        for (move in sortedMoves) {
            val captured = board.movePiece(move)
            val score = -alphaBeta(
                board,
                difficulty.depth - 1,
                Int.MIN_VALUE + 1,
                Int.MAX_VALUE - 1,
                if (aiColor == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
            )
            board.undoMove(Move(move.fromRow, move.fromCol, move.toRow, move.toCol, captured))

            if (score > bestScore) {
                bestScore = score
                bestMove = move
            }
        }

        return bestMove
    }

    /**
     * Alpha-Beta剪枝搜索
     */
    private fun alphaBeta(board: ChessBoard, depth: Int, alpha: Int, beta: Int, color: PieceColor): Int {
        if (depth == 0) {
            return evaluate(board, color)
        }

        val moves = rules.generateAllMoves(board, color)
        if (moves.isEmpty()) {
            // 无合法走法，被将杀
            return -9999 - depth
        }

        var currentAlpha = alpha
        val sortedMoves = moves.sortedByDescending { move ->
            if (move.capturedPiece != null) pieceValues[move.capturedPiece.type] ?: 0 else 0
        }

        for (move in sortedMoves) {
            val captured = board.movePiece(move)
            val score = -alphaBeta(
                board,
                depth - 1,
                -beta,
                -currentAlpha,
                if (color == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
            )
            board.undoMove(Move(move.fromRow, move.fromCol, move.toRow, move.toCol, captured))

            if (score >= beta) {
                return beta
            }
            if (score > currentAlpha) {
                currentAlpha = score
            }
        }
        return currentAlpha
    }

    /**
     * 局面评估函数
     */
    private fun evaluate(board: ChessBoard, color: PieceColor): Int {
        var score = 0
        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                val piece = board.getPiece(r, c) ?: continue
                val baseValue = pieceValues[piece.type] ?: 0
                val posValue = getPositionValue(piece, r, c)
                val value = baseValue + posValue

                if (piece.color == color) {
                    score += value
                } else {
                    score -= value
                }
            }
        }

        // 机动性评估（简单版）
        val myMoves = rules.generateAllMoves(board, color).size
        val opColor = if (color == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
        val opMoves = rules.generateAllMoves(board, opColor).size
        score += (myMoves - opMoves) * 3

        return score
    }

    /**
     * 获取位置价值
     */
    private fun getPositionValue(piece: Piece, row: Int, col: Int): Int {
        return when (piece.type) {
            PieceType.PAWN -> {
                if (piece.color == PieceColor.BLACK) {
                    pawnPositionBlack.getOrNull(row)?.getOrNull(col) ?: 0
                } else {
                    pawnPositionBlack.getOrNull(9 - row)?.getOrNull(8 - col) ?: 0
                }
            }
            PieceType.KING -> {
                if (piece.color == PieceColor.BLACK) {
                    kingPositionBlack.getOrNull(row)?.getOrNull(col) ?: 0
                } else {
                    kingPositionBlack.getOrNull(9 - row)?.getOrNull(8 - col) ?: 0
                }
            }
            PieceType.ADVISOR -> {
                if (piece.color == PieceColor.BLACK) {
                    advisorPositionBlack.getOrNull(row)?.getOrNull(col) ?: 0
                } else {
                    advisorPositionBlack.getOrNull(9 - row)?.getOrNull(8 - col) ?: 0
                }
            }
            PieceType.ROOK -> {
                // 车在中路和对方阵地价值高
                val centerBonus = if (col in 3..5) 10 else 0
                val advanceBonus = if (piece.color == PieceColor.RED) {
                    if (row <= 4) 20 else 0
                } else {
                    if (row >= 5) 20 else 0
                }
                centerBonus + advanceBonus
            }
            PieceType.HORSE -> {
                // 马在中心价值高
                val centerBonus = when {
                    col in 2..6 && row in 2..7 -> 15
                    col in 3..5 && row in 3..6 -> 25
                    else -> 0
                }
                centerBonus
            }
            PieceType.CANNON -> {
                // 炮在初始位置附近价值较高
                val posBonus = if (col in 1..7) 5 else 0
                posBonus
            }
            PieceType.ELEPHANT -> 0
        }
    }
}
