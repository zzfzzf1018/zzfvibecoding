package com.chess.chinese.game

/**
 * 残局模式 - 经典残局集合
 */
class PuzzleManager {

    data class Puzzle(
        val name: String,
        val description: String,
        val pieces: List<PuzzlePiece>,
        val playerColor: PieceColor = PieceColor.RED  // 玩家执哪方
    )

    data class PuzzlePiece(
        val row: Int,
        val col: Int,
        val type: PieceType,
        val color: PieceColor
    )

    companion object {
        fun getPuzzles(): List<Puzzle> = listOf(
            // 七星聚会
            Puzzle(
                name = "七星聚会",
                description = "红先胜，经典七子残局",
                pieces = listOf(
                    // 红方
                    PuzzlePiece(7, 0, PieceType.ROOK, PieceColor.RED),
                    PuzzlePiece(9, 4, PieceType.KING, PieceColor.RED),
                    PuzzlePiece(5, 4, PieceType.PAWN, PieceColor.RED),
                    PuzzlePiece(4, 3, PieceType.PAWN, PieceColor.RED),
                    PuzzlePiece(0, 0, PieceType.ROOK, PieceColor.RED),
                    PuzzlePiece(2, 2, PieceType.CANNON, PieceColor.RED),
                    PuzzlePiece(1, 6, PieceType.HORSE, PieceColor.RED),
                    // 黑方
                    PuzzlePiece(0, 4, PieceType.KING, PieceColor.BLACK),
                    PuzzlePiece(0, 3, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(0, 5, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(3, 4, PieceType.PAWN, PieceColor.BLACK),
                    PuzzlePiece(8, 4, PieceType.ROOK, PieceColor.BLACK),
                    PuzzlePiece(5, 0, PieceType.ROOK, PieceColor.BLACK),
                    PuzzlePiece(2, 4, PieceType.CANNON, PieceColor.BLACK)
                )
            ),
            // 蚯蚓降龙
            Puzzle(
                name = "蚯蚓降龙",
                description = "红先胜，车马兵巧胜",
                pieces = listOf(
                    // 红方
                    PuzzlePiece(9, 4, PieceType.KING, PieceColor.RED),
                    PuzzlePiece(3, 0, PieceType.ROOK, PieceColor.RED),
                    PuzzlePiece(4, 2, PieceType.HORSE, PieceColor.RED),
                    PuzzlePiece(3, 4, PieceType.PAWN, PieceColor.RED),
                    // 黑方
                    PuzzlePiece(0, 4, PieceType.KING, PieceColor.BLACK),
                    PuzzlePiece(1, 4, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(0, 3, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(1, 0, PieceType.ROOK, PieceColor.BLACK),
                    PuzzlePiece(2, 6, PieceType.CANNON, PieceColor.BLACK)
                )
            ),
            // 野马操田
            Puzzle(
                name = "野马操田",
                description = "红先胜，马炮配合",
                pieces = listOf(
                    // 红方
                    PuzzlePiece(9, 4, PieceType.KING, PieceColor.RED),
                    PuzzlePiece(3, 3, PieceType.HORSE, PieceColor.RED),
                    PuzzlePiece(4, 4, PieceType.CANNON, PieceColor.RED),
                    PuzzlePiece(5, 5, PieceType.PAWN, PieceColor.RED),
                    // 黑方
                    PuzzlePiece(0, 4, PieceType.KING, PieceColor.BLACK),
                    PuzzlePiece(1, 4, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(0, 5, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(1, 3, PieceType.ELEPHANT, PieceColor.BLACK),
                    PuzzlePiece(0, 2, PieceType.ELEPHANT, PieceColor.BLACK)
                )
            ),
            // 大刀剜心
            Puzzle(
                name = "大刀剜心",
                description = "红先胜，车炮绝杀",
                pieces = listOf(
                    // 红方
                    PuzzlePiece(9, 4, PieceType.KING, PieceColor.RED),
                    PuzzlePiece(2, 0, PieceType.ROOK, PieceColor.RED),
                    PuzzlePiece(5, 4, PieceType.CANNON, PieceColor.RED),
                    PuzzlePiece(4, 6, PieceType.PAWN, PieceColor.RED),
                    // 黑方
                    PuzzlePiece(0, 4, PieceType.KING, PieceColor.BLACK),
                    PuzzlePiece(0, 3, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(1, 5, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(0, 2, PieceType.ELEPHANT, PieceColor.BLACK),
                    PuzzlePiece(2, 6, PieceType.ELEPHANT, PieceColor.BLACK),
                    PuzzlePiece(3, 8, PieceType.ROOK, PieceColor.BLACK)
                )
            ),
            // 千里独行
            Puzzle(
                name = "千里独行",
                description = "红先胜，单车破士象全",
                pieces = listOf(
                    // 红方
                    PuzzlePiece(9, 4, PieceType.KING, PieceColor.RED),
                    PuzzlePiece(4, 4, PieceType.ROOK, PieceColor.RED),
                    // 黑方
                    PuzzlePiece(0, 4, PieceType.KING, PieceColor.BLACK),
                    PuzzlePiece(0, 3, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(0, 5, PieceType.ADVISOR, PieceColor.BLACK),
                    PuzzlePiece(0, 2, PieceType.ELEPHANT, PieceColor.BLACK),
                    PuzzlePiece(0, 6, PieceType.ELEPHANT, PieceColor.BLACK)
                )
            )
        )
    }

    /**
     * 将残局加载到棋盘
     */
    fun loadPuzzle(puzzle: Puzzle, board: ChessBoard) {
        // 清空棋盘
        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                board.board[r][c] = null
            }
        }
        // 放置棋子
        for (p in puzzle.pieces) {
            board.board[p.row][p.col] = Piece(p.type, p.color)
        }
    }
}
