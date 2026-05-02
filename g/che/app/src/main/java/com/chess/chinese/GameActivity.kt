package com.chess.chinese

import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import com.chess.chinese.game.*
import com.chess.chinese.ui.ChessBoardView

class GameActivity : AppCompatActivity() {

    private lateinit var gameManager: GameManager
    private lateinit var boardView: ChessBoardView
    private lateinit var tvStatus: TextView
    private lateinit var tvThinking: TextView
    private lateinit var btnUndo: Button
    private lateinit var btnRestart: Button
    private lateinit var btnBack: Button

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_game)

        boardView = findViewById(R.id.chess_board_view)
        tvStatus = findViewById(R.id.tv_status)
        tvThinking = findViewById(R.id.tv_thinking)
        btnUndo = findViewById(R.id.btn_undo)
        btnRestart = findViewById(R.id.btn_restart)
        btnBack = findViewById(R.id.btn_back)

        val gameMode = GameMode.valueOf(intent.getStringExtra("game_mode") ?: GameMode.PVP.name)
        val difficulty = AIDifficulty.valueOf(intent.getStringExtra("difficulty") ?: AIDifficulty.MEDIUM.name)

        gameManager = GameManager(gameMode, difficulty)
        boardView.gameManager = gameManager

        setupCallbacks()
        setupButtons()
        updateStatus()
    }

    private fun setupCallbacks() {
        gameManager.onBoardChanged = {
            runOnUiThread {
                boardView.invalidate()
                updateStatus()
            }
        }

        gameManager.onGameOver = { state ->
            runOnUiThread {
                val message = when (state) {
                    GameState.RED_WIN -> "红方胜利！"
                    GameState.BLACK_WIN -> "黑方胜利！"
                    GameState.DRAW -> "平局！"
                    else -> ""
                }
                showGameOverDialog(message)
            }
        }

        gameManager.onAIThinking = { thinking ->
            runOnUiThread {
                tvThinking.visibility = if (thinking) View.VISIBLE else View.GONE
                btnUndo.isEnabled = !thinking
            }
        }
    }

    private fun setupButtons() {
        btnUndo.setOnClickListener {
            if (!gameManager.undoMove()) {
                Toast.makeText(this, "无法悔棋", Toast.LENGTH_SHORT).show()
            }
        }

        btnRestart.setOnClickListener {
            AlertDialog.Builder(this)
                .setTitle("重新开始")
                .setMessage("确定要重新开始吗？")
                .setPositiveButton("确定") { _, _ ->
                    gameManager.restart()
                }
                .setNegativeButton("取消", null)
                .show()
        }

        btnBack.setOnClickListener {
            finish()
        }
    }

    private fun updateStatus() {
        val modeText = if (gameManager.gameMode == GameMode.PVE) {
            "人机对战 (${gameManager.difficulty.displayName})"
        } else {
            "双人对战"
        }
        val turnText = if (gameManager.currentTurn == PieceColor.RED) "红方走棋" else "黑方走棋"
        tvStatus.text = "$modeText | $turnText"
    }

    private fun showGameOverDialog(message: String) {
        AlertDialog.Builder(this)
            .setTitle("游戏结束")
            .setMessage(message)
            .setPositiveButton("再来一局") { _, _ ->
                gameManager.restart()
            }
            .setNegativeButton("返回主菜单") { _, _ ->
                finish()
            }
            .setCancelable(false)
            .show()
    }
}
