package com.chess.chinese.game

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject

/**
 * 存档管理器 - 保存和读取对局进度
 */
class SaveManager(private val context: Context) {

    companion object {
        private const val PREFS_NAME = "chess_saves"
        private const val KEY_SAVE_LIST = "save_list"
        private const val MAX_SAVES = 10
    }

    data class SaveData(
        val id: Long,
        val name: String,
        val timestamp: Long,
        val gameMode: String,
        val difficulty: String,
        val currentTurn: String,
        val boardState: String,
        val moveHistory: String
    )

    /**
     * 保存当前对局
     */
    fun saveGame(gameManager: GameManager, name: String = ""): Boolean {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val id = System.currentTimeMillis()
        val saveName = if (name.isEmpty()) {
            val modeStr = if (gameManager.gameMode == GameMode.PVE) "人机" else "双人"
            "$modeStr-${android.text.format.DateFormat.format("MM/dd HH:mm", id)}"
        } else name

        val boardJson = encodeBoardState(gameManager.board)
        val movesJson = encodeMoveHistory(gameManager.getMoveHistory())

        val saveJson = JSONObject().apply {
            put("id", id)
            put("name", saveName)
            put("timestamp", id)
            put("gameMode", gameManager.gameMode.name)
            put("difficulty", gameManager.difficulty.name)
            put("currentTurn", gameManager.currentTurn.name)
            put("boardState", boardJson)
            put("moveHistory", movesJson)
        }

        // 读取已有存档列表
        val savesStr = prefs.getString(KEY_SAVE_LIST, "[]") ?: "[]"
        val savesArray = JSONArray(savesStr)

        // 最多保存MAX_SAVES个
        while (savesArray.length() >= MAX_SAVES) {
            savesArray.remove(0)
        }
        savesArray.put(saveJson)

        prefs.edit().putString(KEY_SAVE_LIST, savesArray.toString()).apply()
        return true
    }

    /**
     * 获取所有存档
     */
    fun getSaveList(): List<SaveData> {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val savesStr = prefs.getString(KEY_SAVE_LIST, "[]") ?: "[]"
        val savesArray = JSONArray(savesStr)
        val list = mutableListOf<SaveData>()

        for (i in 0 until savesArray.length()) {
            val obj = savesArray.getJSONObject(i)
            list.add(SaveData(
                id = obj.getLong("id"),
                name = obj.getString("name"),
                timestamp = obj.getLong("timestamp"),
                gameMode = obj.getString("gameMode"),
                difficulty = obj.getString("difficulty"),
                currentTurn = obj.getString("currentTurn"),
                boardState = obj.getString("boardState"),
                moveHistory = obj.getString("moveHistory")
            ))
        }
        return list.reversed() // 最新的在前
    }

    /**
     * 加载存档到GameManager
     */
    fun loadGame(saveData: SaveData, gameManager: GameManager) {
        // 还原棋盘
        decodeBoardState(saveData.boardState, gameManager.board)
        // 还原走法历史
        val moves = decodeMoveHistory(saveData.moveHistory)
        gameManager.loadState(
            PieceColor.valueOf(saveData.currentTurn),
            moves
        )
    }

    /**
     * 删除存档
     */
    fun deleteSave(id: Long) {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val savesStr = prefs.getString(KEY_SAVE_LIST, "[]") ?: "[]"
        val savesArray = JSONArray(savesStr)
        val newArray = JSONArray()

        for (i in 0 until savesArray.length()) {
            val obj = savesArray.getJSONObject(i)
            if (obj.getLong("id") != id) {
                newArray.put(obj)
            }
        }
        prefs.edit().putString(KEY_SAVE_LIST, newArray.toString()).apply()
    }

    private fun encodeBoardState(board: ChessBoard): String {
        val json = JSONArray()
        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                val piece = board.getPiece(r, c)
                if (piece != null) {
                    json.put(JSONObject().apply {
                        put("r", r)
                        put("c", c)
                        put("t", piece.type.name)
                        put("cl", piece.color.name)
                    })
                }
            }
        }
        return json.toString()
    }

    private fun decodeBoardState(json: String, board: ChessBoard) {
        for (r in 0 until ChessBoard.ROWS) {
            for (c in 0 until ChessBoard.COLS) {
                board.board[r][c] = null
            }
        }
        val arr = JSONArray(json)
        for (i in 0 until arr.length()) {
            val obj = arr.getJSONObject(i)
            board.board[obj.getInt("r")][obj.getInt("c")] = Piece(
                PieceType.valueOf(obj.getString("t")),
                PieceColor.valueOf(obj.getString("cl"))
            )
        }
    }

    private fun encodeMoveHistory(moves: List<Move>): String {
        val json = JSONArray()
        for (move in moves) {
            json.put(JSONObject().apply {
                put("fr", move.fromRow)
                put("fc", move.fromCol)
                put("tr", move.toRow)
                put("tc", move.toCol)
                if (move.capturedPiece != null) {
                    put("ct", move.capturedPiece.type.name)
                    put("cc", move.capturedPiece.color.name)
                }
            })
        }
        return json.toString()
    }

    private fun decodeMoveHistory(json: String): List<Move> {
        val arr = JSONArray(json)
        val moves = mutableListOf<Move>()
        for (i in 0 until arr.length()) {
            val obj = arr.getJSONObject(i)
            val captured = if (obj.has("ct")) {
                Piece(PieceType.valueOf(obj.getString("ct")), PieceColor.valueOf(obj.getString("cc")))
            } else null
            moves.add(Move(obj.getInt("fr"), obj.getInt("fc"), obj.getInt("tr"), obj.getInt("tc"), captured))
        }
        return moves
    }
}
