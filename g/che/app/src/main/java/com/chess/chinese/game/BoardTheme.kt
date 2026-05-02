package com.chess.chinese.game

/**
 * 主题/皮肤管理
 */
data class BoardTheme(
    val name: String,
    val boardBgColor: String,
    val boardLineColor: String,
    val redPieceColor: String,
    val blackPieceColor: String,
    val pieceBgColor: String,
    val selectedColor: String,
    val validMoveColor: String
) {
    companion object {
        val CLASSIC = BoardTheme(
            name = "经典",
            boardBgColor = "#F5DEB3",
            boardLineColor = "#8B4513",
            redPieceColor = "#CC0000",
            blackPieceColor = "#1A1A1A",
            pieceBgColor = "#FFEEDD",
            selectedColor = "#4400FF00",
            validMoveColor = "#6600AA00"
        )

        val DARK = BoardTheme(
            name = "暗夜",
            boardBgColor = "#2C2C2C",
            boardLineColor = "#888888",
            redPieceColor = "#FF4444",
            blackPieceColor = "#CCCCCC",
            pieceBgColor = "#3C3C3C",
            selectedColor = "#4400FFFF",
            validMoveColor = "#6600CCFF"
        )

        val JADE = BoardTheme(
            name = "翡翠",
            boardBgColor = "#E8F5E9",
            boardLineColor = "#2E7D32",
            redPieceColor = "#C62828",
            blackPieceColor = "#1B5E20",
            pieceBgColor = "#F1F8E9",
            selectedColor = "#44FFEB3B",
            validMoveColor = "#6676FF03"
        )

        val WOOD = BoardTheme(
            name = "檀木",
            boardBgColor = "#D7CCC8",
            boardLineColor = "#4E342E",
            redPieceColor = "#B71C1C",
            blackPieceColor = "#212121",
            pieceBgColor = "#EFEBE9",
            selectedColor = "#44FF9800",
            validMoveColor = "#66FF6D00"
        )

        val BLUE = BoardTheme(
            name = "蓝天",
            boardBgColor = "#E3F2FD",
            boardLineColor = "#1565C0",
            redPieceColor = "#D32F2F",
            blackPieceColor = "#0D47A1",
            pieceBgColor = "#E8EAF6",
            selectedColor = "#4400E676",
            validMoveColor = "#6600C853"
        )

        fun getAllThemes(): List<BoardTheme> = listOf(CLASSIC, DARK, JADE, WOOD, BLUE)
    }
}
