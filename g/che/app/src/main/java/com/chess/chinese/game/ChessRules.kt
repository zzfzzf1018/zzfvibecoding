package com.chess.chinese.game

/**
 * 象棋规则引擎 - 走法生成和合法性校验
 */
class ChessRules {

    /**
     * 生成指定颜色所有合法走法
     */
    fun generateAllMoves(board: ChessBoard, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                val piece = board.getPiece(r, c)
                if (piece != null && piece.color == color) {
                    moves.addAll(generateMovesForPiece(board, r, c))
                }
            }
        }
        return moves.filter { isMoveLegal(board, it, color) }
    }

    /**
     * 生成某个位置棋子的所有候选走法（不含将军检查）
     */
    fun generateMovesForPiece(board: ChessBoard, row: Int, col: Int): List<Move> {
        val piece = board.getPiece(row, col) ?: return emptyList()
        return when (piece.type) {
            PieceType.KING -> generateKingMoves(board, row, col, piece.color)
            PieceType.ADVISOR -> generateAdvisorMoves(board, row, col, piece.color)
            PieceType.ELEPHANT -> generateElephantMoves(board, row, col, piece.color)
            PieceType.HORSE -> generateHorseMoves(board, row, col, piece.color)
            PieceType.ROOK -> generateRookMoves(board, row, col, piece.color)
            PieceType.CANNON -> generateCannonMoves(board, row, col, piece.color)
            PieceType.PAWN -> generatePawnMoves(board, row, col, piece.color)
        }
    }

    /**
     * 判断走法是否合法（走完后己方将帅不被将军）
     */
    fun isMoveLegal(board: ChessBoard, move: Move, color: PieceColor): Boolean {
        // 执行走法
        val captured = board.movePiece(move)
        // 检查走完后己方是否被将军
        val inCheck = isKingInCheck(board, color)
        // 检查将帅是否对面
        val kingsFacing = areKingsFacing(board)
        // 撤销走法
        board.undoMove(Move(move.fromRow, move.fromCol, move.toRow, move.toCol, captured))
        return !inCheck && !kingsFacing
    }

    /**
     * 检查将帅是否面对面
     */
    fun areKingsFacing(board: ChessBoard): Boolean {
        val redKing = board.findKing(PieceColor.RED) ?: return false
        val blackKing = board.findKing(PieceColor.BLACK) ?: return false
        if (redKing.second != blackKing.second) return false
        val col = redKing.second
        val minRow = minOf(redKing.first, blackKing.first)
        val maxRow = maxOf(redKing.first, blackKing.first)
        for (r in (minRow + 1) until maxRow) {
            if (board.getPiece(r, col) != null) return false
        }
        return true
    }

    /**
     * 检查指定颜色的将/帅是否正在被将军
     */
    fun isKingInCheck(board: ChessBoard, color: PieceColor): Boolean {
        val kingPos = board.findKing(color) ?: return true
        val opponentColor = if (color == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
        // 检查对方所有棋子是否能攻击到将/帅
        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                val piece = board.getPiece(r, c)
                if (piece != null && piece.color == opponentColor) {
                    val moves = generateMovesForPiece(board, r, c)
                    if (moves.any { it.toRow == kingPos.first && it.toCol == kingPos.second }) {
                        return true
                    }
                }
            }
        }
        return false
    }

    /**
     * 检查是否将杀（无合法走法）
     */
    fun isCheckmate(board: ChessBoard, color: PieceColor): Boolean {
        return generateAllMoves(board, color).isEmpty()
    }

    // --- 各棋子走法生成 ---

    private fun generateKingMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        val directions = arrayOf(intArrayOf(-1, 0), intArrayOf(1, 0), intArrayOf(0, -1), intArrayOf(0, 1))

        // 九宫格范围
        val rowRange = if (color == PieceColor.RED) 7..9 else 0..2
        val colRange = 3..5

        for (dir in directions) {
            val nr = row + dir[0]
            val nc = col + dir[1]
            if (nr in rowRange && nc in colRange) {
                val target = board.getPiece(nr, nc)
                if (target == null || target.color != color) {
                    moves.add(Move(row, col, nr, nc, target))
                }
            }
        }
        return moves
    }

    private fun generateAdvisorMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        val directions = arrayOf(intArrayOf(-1, -1), intArrayOf(-1, 1), intArrayOf(1, -1), intArrayOf(1, 1))

        val rowRange = if (color == PieceColor.RED) 7..9 else 0..2
        val colRange = 3..5

        for (dir in directions) {
            val nr = row + dir[0]
            val nc = col + dir[1]
            if (nr in rowRange && nc in colRange) {
                val target = board.getPiece(nr, nc)
                if (target == null || target.color != color) {
                    moves.add(Move(row, col, nr, nc, target))
                }
            }
        }
        return moves
    }

    private fun generateElephantMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        val directions = arrayOf(intArrayOf(-2, -2), intArrayOf(-2, 2), intArrayOf(2, -2), intArrayOf(2, 2))
        val blocks = arrayOf(intArrayOf(-1, -1), intArrayOf(-1, 1), intArrayOf(1, -1), intArrayOf(1, 1))

        // 象不能过河
        val rowRange = if (color == PieceColor.RED) 5..9 else 0..4

        for (i in directions.indices) {
            val nr = row + directions[i][0]
            val nc = col + directions[i][1]
            val br = row + blocks[i][0]
            val bc = col + blocks[i][1]
            if (nr in rowRange && nc in 0..8) {
                // 检查象眼是否被堵
                if (board.getPiece(br, bc) == null) {
                    val target = board.getPiece(nr, nc)
                    if (target == null || target.color != color) {
                        moves.add(Move(row, col, nr, nc, target))
                    }
                }
            }
        }
        return moves
    }

    private fun generateHorseMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        // 马的8个方向及对应蹩马腿位置
        val targets = arrayOf(
            intArrayOf(-2, -1), intArrayOf(-2, 1),
            intArrayOf(-1, -2), intArrayOf(-1, 2),
            intArrayOf(1, -2), intArrayOf(1, 2),
            intArrayOf(2, -1), intArrayOf(2, 1)
        )
        val blocks = arrayOf(
            intArrayOf(-1, 0), intArrayOf(-1, 0),
            intArrayOf(0, -1), intArrayOf(0, 1),
            intArrayOf(0, -1), intArrayOf(0, 1),
            intArrayOf(1, 0), intArrayOf(1, 0)
        )

        for (i in targets.indices) {
            val nr = row + targets[i][0]
            val nc = col + targets[i][1]
            val br = row + blocks[i][0]
            val bc = col + blocks[i][1]
            if (nr in 0..9 && nc in 0..8) {
                // 检查蹩马腿
                if (board.getPiece(br, bc) == null) {
                    val target = board.getPiece(nr, nc)
                    if (target == null || target.color != color) {
                        moves.add(Move(row, col, nr, nc, target))
                    }
                }
            }
        }
        return moves
    }

    private fun generateRookMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        val directions = arrayOf(intArrayOf(-1, 0), intArrayOf(1, 0), intArrayOf(0, -1), intArrayOf(0, 1))

        for (dir in directions) {
            var nr = row + dir[0]
            var nc = col + dir[1]
            while (nr in 0..9 && nc in 0..8) {
                val target = board.getPiece(nr, nc)
                if (target == null) {
                    moves.add(Move(row, col, nr, nc))
                } else {
                    if (target.color != color) {
                        moves.add(Move(row, col, nr, nc, target))
                    }
                    break
                }
                nr += dir[0]
                nc += dir[1]
            }
        }
        return moves
    }

    private fun generateCannonMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()
        val directions = arrayOf(intArrayOf(-1, 0), intArrayOf(1, 0), intArrayOf(0, -1), intArrayOf(0, 1))

        for (dir in directions) {
            var nr = row + dir[0]
            var nc = col + dir[1]
            var jumped = false
            while (nr in 0..9 && nc in 0..8) {
                val target = board.getPiece(nr, nc)
                if (!jumped) {
                    if (target == null) {
                        moves.add(Move(row, col, nr, nc))
                    } else {
                        jumped = true
                    }
                } else {
                    if (target != null) {
                        if (target.color != color) {
                            moves.add(Move(row, col, nr, nc, target))
                        }
                        break
                    }
                }
                nr += dir[0]
                nc += dir[1]
            }
        }
        return moves
    }

    private fun generatePawnMoves(board: ChessBoard, row: Int, col: Int, color: PieceColor): List<Move> {
        val moves = mutableListOf<Move>()

        if (color == PieceColor.RED) {
            // 红兵：向上走
            val nr = row - 1
            if (nr >= 0) {
                val target = board.getPiece(nr, col)
                if (target == null || target.color != color) {
                    moves.add(Move(row, col, nr, col, target))
                }
            }
            // 过河后可以左右走
            if (row <= 4) {
                for (dc in intArrayOf(-1, 1)) {
                    val nc = col + dc
                    if (nc in 0..8) {
                        val target = board.getPiece(row, nc)
                        if (target == null || target.color != color) {
                            moves.add(Move(row, col, row, nc, target))
                        }
                    }
                }
            }
        } else {
            // 黑卒：向下走
            val nr = row + 1
            if (nr <= 9) {
                val target = board.getPiece(nr, col)
                if (target == null || target.color != color) {
                    moves.add(Move(row, col, nr, col, target))
                }
            }
            // 过河后可以左右走
            if (row >= 5) {
                for (dc in intArrayOf(-1, 1)) {
                    val nc = col + dc
                    if (nc in 0..8) {
                        val target = board.getPiece(row, nc)
                        if (target == null || target.color != color) {
                            moves.add(Move(row, col, row, nc, target))
                        }
                    }
                }
            }
        }
        return moves
    }
}
