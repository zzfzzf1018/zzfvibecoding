package com.chess.chinese.game

/**
 * 走法
 */
data class Move(
    val fromRow: Int,
    val fromCol: Int,
    val toRow: Int,
    val toCol: Int,
    val capturedPiece: Piece? = null
)
