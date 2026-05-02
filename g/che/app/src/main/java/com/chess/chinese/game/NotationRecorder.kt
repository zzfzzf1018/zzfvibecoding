package com.chess.chinese.game

/**
 * 棋谱记录器 - 生成中文棋谱记法
 */
class NotationRecorder {

    private val records = mutableListOf<String>()

    /**
     * 记录一步棋
     */
    fun recordMove(board: ChessBoard, move: Move, piece: Piece): String {
        val notation = generateNotation(board, move, piece)
        records.add(notation)
        return notation
    }

    fun getRecords(): List<String> = records.toList()

    fun clear() {
        records.clear()
    }

    fun removeLast() {
        if (records.isNotEmpty()) records.removeAt(records.size - 1)
    }

    /**
     * 导出棋谱文本
     */
    fun exportNotation(): String {
        val sb = StringBuilder()
        sb.appendLine("=== 中国象棋对局棋谱 ===")
        sb.appendLine()
        for (i in records.indices) {
            val roundNum = i / 2 + 1
            if (i % 2 == 0) {
                sb.append("$roundNum. ${records[i]}")
            } else {
                sb.appendLine("  ${records[i]}")
            }
        }
        if (records.size % 2 == 1) sb.appendLine()
        return sb.toString()
    }

    private fun generateNotation(board: ChessBoard, move: Move, piece: Piece): String {
        val isRed = piece.color == PieceColor.RED
        val pieceName = piece.getDisplayName()

        // 列号表示（红方从右到左为一到九，黑方从右到左为1到9）
        val fromColName = getColName(move.fromCol, isRed)
        val toColName = getColName(move.toCol, isRed)

        // 方向
        val rowDiff = move.toRow - move.fromRow
        val direction = if (isRed) {
            when {
                rowDiff < 0 -> "进"
                rowDiff > 0 -> "退"
                else -> "平"
            }
        } else {
            when {
                rowDiff > 0 -> "进"
                rowDiff < 0 -> "退"
                else -> "平"
            }
        }

        // 步数或目标列
        val distance = if (direction == "平") {
            toColName
        } else {
            when (piece.type) {
                PieceType.HORSE, PieceType.ELEPHANT, PieceType.ADVISOR -> toColName
                else -> getDistanceName(Math.abs(rowDiff), isRed)
            }
        }

        return "$pieceName$fromColName$direction$distance"
    }

    private fun getColName(col: Int, isRed: Boolean): String {
        val redNumbers = arrayOf("九", "八", "七", "六", "五", "四", "三", "二", "一")
        val blackNumbers = arrayOf("9", "8", "7", "6", "5", "4", "3", "2", "1")
        return if (isRed) redNumbers[col] else blackNumbers[col]
    }

    private fun getDistanceName(distance: Int, isRed: Boolean): String {
        val redNumbers = arrayOf("", "一", "二", "三", "四", "五", "六", "七", "八", "九")
        val blackNumbers = arrayOf("", "1", "2", "3", "4", "5", "6", "7", "8", "9")
        return if (isRed) redNumbers.getOrElse(distance) { distance.toString() }
        else blackNumbers.getOrElse(distance) { distance.toString() }
    }
}
