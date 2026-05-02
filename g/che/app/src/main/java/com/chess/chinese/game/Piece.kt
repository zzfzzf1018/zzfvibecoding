package com.chess.chinese.game

/**
 * 棋子类型
 */
enum class PieceType {
    KING,      // 将/帅
    ADVISOR,   // 士/仕
    ELEPHANT,  // 象/相
    HORSE,     // 马
    ROOK,      // 车
    CANNON,    // 炮
    PAWN       // 兵/卒
}

/**
 * 棋子颜色（阵营）
 */
enum class PieceColor {
    RED,   // 红方
    BLACK  // 黑方
}

/**
 * 棋子
 */
data class Piece(
    val type: PieceType,
    val color: PieceColor
) {
    fun getDisplayName(): String {
        return when (color) {
            PieceColor.RED -> when (type) {
                PieceType.KING -> "帅"
                PieceType.ADVISOR -> "仕"
                PieceType.ELEPHANT -> "相"
                PieceType.HORSE -> "马"
                PieceType.ROOK -> "车"
                PieceType.CANNON -> "炮"
                PieceType.PAWN -> "兵"
            }
            PieceColor.BLACK -> when (type) {
                PieceType.KING -> "将"
                PieceType.ADVISOR -> "士"
                PieceType.ELEPHANT -> "象"
                PieceType.HORSE -> "馬"
                PieceType.ROOK -> "車"
                PieceType.CANNON -> "砲"
                PieceType.PAWN -> "卒"
            }
        }
    }
}
