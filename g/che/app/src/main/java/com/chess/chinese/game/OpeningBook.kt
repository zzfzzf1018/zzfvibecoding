package com.chess.chinese.game

/**
 * AI开局库 - 预置常见开局走法
 */
class OpeningBook {

    data class BookEntry(
        val moves: List<Pair<Pair<Int, Int>, Pair<Int, Int>>>,  // (from, to) 对列表
        val name: String
    )

    private val openings = listOf(
        // 中炮开局
        BookEntry(
            listOf(
                Pair(Pair(9, 4), Pair(9, 4)), // 不动（占位）
                Pair(Pair(7, 1), Pair(7, 4))  // 炮二平五
            ),
            "中炮"
        )
    )

    // 红方开局走法库（row, col) -> (row, col)
    private val redOpeningMoves = listOf(
        // 中炮开局
        listOf(
            Move(7, 1, 7, 4),   // 炮二平五
            Move(9, 1, 7, 2),   // 马二进三
            Move(9, 7, 7, 6),   // 马八进七
            Move(6, 2, 5, 2),   // 兵三进一
            Move(6, 6, 5, 6)    // 兵七进一
        ),
        // 飞相开局
        listOf(
            Move(9, 2, 7, 4),   // 相三进五
            Move(9, 1, 7, 2),   // 马二进三
            Move(9, 7, 7, 6),   // 马八进七
            Move(7, 7, 7, 4),   // 炮八平五
            Move(6, 4, 5, 4)    // 兵五进一
        ),
        // 仙人指路
        listOf(
            Move(6, 6, 5, 6),   // 兵七进一
            Move(9, 7, 7, 6),   // 马八进七
            Move(9, 1, 7, 2),   // 马二进三
            Move(7, 1, 7, 4),   // 炮二平五
            Move(6, 2, 5, 2)    // 兵三进一
        ),
        // 起马局
        listOf(
            Move(9, 1, 7, 2),   // 马二进三
            Move(9, 7, 7, 6),   // 马八进七
            Move(7, 1, 7, 4),   // 炮二平五
            Move(6, 4, 5, 4),   // 兵五进一
            Move(9, 0, 9, 1)    // 车一进一
        )
    )

    // 黑方应对走法库
    private val blackResponseMoves = listOf(
        // 屏风马
        listOf(
            Move(0, 1, 2, 2),   // 马2进3
            Move(0, 7, 2, 6),   // 马8进7
            Move(3, 0, 4, 0),   // 卒1进1
            Move(2, 1, 2, 4),   // 炮2平5
            Move(3, 4, 4, 4)    // 卒5进1
        ),
        // 反宫马
        listOf(
            Move(0, 7, 2, 6),   // 马8进7
            Move(2, 7, 2, 4),   // 炮8平5
            Move(0, 1, 2, 2),   // 马2进3
            Move(3, 6, 4, 6),   // 卒7进1
            Move(0, 0, 0, 1)    // 车1平2
        ),
        // 顺炮
        listOf(
            Move(2, 7, 2, 4),   // 炮8平5
            Move(0, 7, 2, 6),   // 马8进7
            Move(0, 1, 2, 2),   // 马2进3
            Move(3, 2, 4, 2),   // 卒3进1
            Move(0, 8, 1, 8)    // 车9进1
        )
    )

    /**
     * 获取开局走法，如果当前局面在开局库中
     * @param moveCount 当前已走步数
     * @param color 当前走棋方
     * @return 开局走法，如果没有匹配返回null
     */
    fun getOpeningMove(moveCount: Int, color: PieceColor): Move? {
        if (moveCount >= 5) return null // 超过5步不再使用开局库

        val bookMoves = if (color == PieceColor.RED) redOpeningMoves else blackResponseMoves
        val index = moveCount / 2  // 该方第几步

        if (index >= 5) return null

        // 随机选择一个开局变化
        val variation = bookMoves.random()
        return variation.getOrNull(index)
    }
}
