package com.chess.chinese.game

/**
 * 象棋棋盘 - 9列10行
 * 行: 0-9 (0为黑方底线，9为红方底线)
 * 列: 0-8
 */
class ChessBoard {
    // 棋盘 10行 x 9列
    val board = Array<Array<Piece?>>(ROWS) { arrayOfNulls(COLS) }

    companion object {
        const val ROWS = 10
        const val COLS = 9
    }

    init {
        initBoard()
    }

    fun initBoard() {
        // 清空棋盘
        for (r in 0 until ROWS) {
            for (c in 0 until COLS) {
                board[r][c] = null
            }
        }

        // 黑方棋子 (上方, row 0-4)
        board[0][0] = Piece(PieceType.ROOK, PieceColor.BLACK)
        board[0][1] = Piece(PieceType.HORSE, PieceColor.BLACK)
        board[0][2] = Piece(PieceType.ELEPHANT, PieceColor.BLACK)
        board[0][3] = Piece(PieceType.ADVISOR, PieceColor.BLACK)
        board[0][4] = Piece(PieceType.KING, PieceColor.BLACK)
        board[0][5] = Piece(PieceType.ADVISOR, PieceColor.BLACK)
        board[0][6] = Piece(PieceType.ELEPHANT, PieceColor.BLACK)
        board[0][7] = Piece(PieceType.HORSE, PieceColor.BLACK)
        board[0][8] = Piece(PieceType.ROOK, PieceColor.BLACK)
        board[2][1] = Piece(PieceType.CANNON, PieceColor.BLACK)
        board[2][7] = Piece(PieceType.CANNON, PieceColor.BLACK)
        board[3][0] = Piece(PieceType.PAWN, PieceColor.BLACK)
        board[3][2] = Piece(PieceType.PAWN, PieceColor.BLACK)
        board[3][4] = Piece(PieceType.PAWN, PieceColor.BLACK)
        board[3][6] = Piece(PieceType.PAWN, PieceColor.BLACK)
        board[3][8] = Piece(PieceType.PAWN, PieceColor.BLACK)

        // 红方棋子 (下方, row 5-9)
        board[9][0] = Piece(PieceType.ROOK, PieceColor.RED)
        board[9][1] = Piece(PieceType.HORSE, PieceColor.RED)
        board[9][2] = Piece(PieceType.ELEPHANT, PieceColor.RED)
        board[9][3] = Piece(PieceType.ADVISOR, PieceColor.RED)
        board[9][4] = Piece(PieceType.KING, PieceColor.RED)
        board[9][5] = Piece(PieceType.ADVISOR, PieceColor.RED)
        board[9][6] = Piece(PieceType.ELEPHANT, PieceColor.RED)
        board[9][7] = Piece(PieceType.HORSE, PieceColor.RED)
        board[9][8] = Piece(PieceType.ROOK, PieceColor.RED)
        board[7][1] = Piece(PieceType.CANNON, PieceColor.RED)
        board[7][7] = Piece(PieceType.CANNON, PieceColor.RED)
        board[6][0] = Piece(PieceType.PAWN, PieceColor.RED)
        board[6][2] = Piece(PieceType.PAWN, PieceColor.RED)
        board[6][4] = Piece(PieceType.PAWN, PieceColor.RED)
        board[6][6] = Piece(PieceType.PAWN, PieceColor.RED)
        board[6][8] = Piece(PieceType.PAWN, PieceColor.RED)
    }

    fun getPiece(row: Int, col: Int): Piece? {
        if (row < 0 || row >= ROWS || col < 0 || col >= COLS) return null
        return board[row][col]
    }

    fun setPiece(row: Int, col: Int, piece: Piece?) {
        if (row in 0 until ROWS && col in 0 until COLS) {
            board[row][col] = piece
        }
    }

    fun movePiece(move: Move): Piece? {
        val piece = board[move.fromRow][move.fromCol]
        val captured = board[move.toRow][move.toCol]
        board[move.toRow][move.toCol] = piece
        board[move.fromRow][move.fromCol] = null
        return captured
    }

    fun undoMove(move: Move) {
        val piece = board[move.toRow][move.toCol]
        board[move.fromRow][move.fromCol] = piece
        board[move.toRow][move.toCol] = move.capturedPiece
    }

    fun clone(): ChessBoard {
        val newBoard = ChessBoard()
        for (r in 0 until ROWS) {
            for (c in 0 until COLS) {
                newBoard.board[r][c] = board[r][c]
            }
        }
        return newBoard
    }

    /**
     * 找到指定颜色的将/帅位置
     */
    fun findKing(color: PieceColor): Pair<Int, Int>? {
        for (r in 0 until ROWS) {
            for (c in 0 until COLS) {
                val piece = board[r][c]
                if (piece != null && piece.type == PieceType.KING && piece.color == color) {
                    return Pair(r, c)
                }
            }
        }
        return null
    }
}
