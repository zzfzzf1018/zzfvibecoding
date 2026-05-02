package com.chess.chinese

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.os.Bundle
import android.view.View
import android.widget.*
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import com.chess.chinese.game.*
import com.chess.chinese.ui.ChessBoardView

class GameActivity : AppCompatActivity() {

    private lateinit var gameManager: GameManager
    private lateinit var boardView: ChessBoardView
    private lateinit var tvStatus: TextView
    private lateinit var tvThinking: TextView
    private lateinit var tvNotation: TextView
    private lateinit var scrollNotation: ScrollView
    private lateinit var btnUndo: Button
    private lateinit var btnRestart: Button
    private lateinit var btnBack: Button
    private lateinit var btnHint: Button
    private lateinit var btnSave: Button
    private lateinit var btnMore: Button

    private lateinit var soundManager: SoundManager
    private lateinit var saveManager: SaveManager
    private var autoFlip = false  // 双人对战自动翻转

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_game)

        soundManager = SoundManager(this)
        saveManager = SaveManager(this)

        boardView = findViewById(R.id.chess_board_view)
        tvStatus = findViewById(R.id.tv_status)
        tvThinking = findViewById(R.id.tv_thinking)
        tvNotation = findViewById(R.id.tv_notation)
        scrollNotation = findViewById(R.id.scroll_notation)
        btnUndo = findViewById(R.id.btn_undo)
        btnRestart = findViewById(R.id.btn_restart)
        btnBack = findViewById(R.id.btn_back)
        btnHint = findViewById(R.id.btn_hint)
        btnSave = findViewById(R.id.btn_save)
        btnMore = findViewById(R.id.btn_more)

        val gameMode = GameMode.valueOf(intent.getStringExtra("game_mode") ?: GameMode.PVP.name)
        val difficulty = AIDifficulty.valueOf(intent.getStringExtra("difficulty") ?: AIDifficulty.MEDIUM.name)
        autoFlip = intent.getBooleanExtra("auto_flip", false)

        // 残局模式
        val puzzleIndex = intent.getIntExtra("puzzle_index", -1)

        gameManager = GameManager(gameMode, difficulty)

        if (puzzleIndex >= 0) {
            val puzzles = PuzzleManager.getPuzzles()
            if (puzzleIndex < puzzles.size) {
                val puzzle = puzzles[puzzleIndex]
                val pm = PuzzleManager()
                pm.loadPuzzle(puzzle, gameManager.board)
                tvStatus.text = "残局: ${puzzle.name}"
            }
        }

        // 加载存档
        val loadSaveId = intent.getLongExtra("load_save_id", -1L)
        if (loadSaveId >= 0) {
            val saves = saveManager.getSaveList()
            val save = saves.find { it.id == loadSaveId }
            if (save != null) {
                saveManager.loadGame(save, gameManager)
            }
        }

        boardView.gameManager = gameManager

        // 应用主题
        val themeIndex = getSharedPreferences("settings", MODE_PRIVATE).getInt("theme_index", 0)
        boardView.theme = BoardTheme.getAllThemes().getOrElse(themeIndex) { BoardTheme.CLASSIC }

        setupCallbacks()
        setupButtons()
        updateStatus()

        // 人机模式隐藏提示按钮（可以用来请求AI建议）
        btnHint.visibility = if (gameMode == GameMode.PVP) View.VISIBLE else View.VISIBLE
    }

    private fun setupCallbacks() {
        gameManager.onBoardChanged = {
            runOnUiThread {
                // 双人对战自动翻转
                if (autoFlip && gameManager.gameMode == GameMode.PVP) {
                    boardView.isFlipped = gameManager.currentTurn == PieceColor.BLACK
                }
                boardView.invalidate()
                updateStatus()
            }
        }

        gameManager.onGameOver = { state ->
            runOnUiThread {
                soundManager.playGameOver()
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
                tvThinking.visibility = if (thinking) View.VISIBLE else View.INVISIBLE
                btnUndo.isEnabled = !thinking
                btnHint.isEnabled = !thinking
            }
        }

        gameManager.onMoveEvent = { eventType, _ ->
            runOnUiThread {
                when (eventType) {
                    MoveEventType.MOVE -> soundManager.playMove()
                    MoveEventType.CAPTURE -> soundManager.playCapture()
                    MoveEventType.CHECK -> soundManager.playCheck()
                    MoveEventType.CHECKMATE -> soundManager.playCheck()
                }
            }
        }

        gameManager.onNotationAdded = { notation ->
            runOnUiThread {
                val records = gameManager.notationRecorder.getRecords()
                val sb = StringBuilder()
                for (i in records.indices) {
                    val num = i / 2 + 1
                    if (i % 2 == 0) sb.append("$num. ${records[i]}  ")
                    else sb.appendLine(records[i])
                }
                tvNotation.text = sb.toString()
                scrollNotation.post { scrollNotation.fullScroll(View.FOCUS_DOWN) }
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
                    tvNotation.text = ""
                }
                .setNegativeButton("取消", null)
                .show()
        }

        btnBack.setOnClickListener {
            finish()
        }

        btnHint.setOnClickListener {
            btnHint.isEnabled = false
            Thread {
                val hint = gameManager.getHint()
                runOnUiThread {
                    btnHint.isEnabled = true
                    if (hint != null) {
                        boardView.hintMove = hint
                        Toast.makeText(this, "AI建议已显示（蓝色标记）", Toast.LENGTH_SHORT).show()
                    } else {
                        Toast.makeText(this, "无法获取提示", Toast.LENGTH_SHORT).show()
                    }
                }
            }.start()
        }

        btnSave.setOnClickListener {
            saveManager.saveGame(gameManager)
            Toast.makeText(this, "对局已保存", Toast.LENGTH_SHORT).show()
        }

        btnMore.setOnClickListener {
            showMoreMenu()
        }
    }

    private fun showMoreMenu() {
        val items = arrayOf("导出棋谱", "切换主题", "翻转棋盘", "音效开关")
        AlertDialog.Builder(this)
            .setTitle("更多选项")
            .setItems(items) { _, which ->
                when (which) {
                    0 -> exportNotation()
                    1 -> showThemeDialog()
                    2 -> {
                        boardView.isFlipped = !boardView.isFlipped
                        Toast.makeText(this,
                            if (boardView.isFlipped) "已翻转棋盘" else "已恢复棋盘方向",
                            Toast.LENGTH_SHORT).show()
                    }
                    3 -> {
                        // 简单切换（实际可存储到SharedPreferences）
                        Toast.makeText(this, "音效已切换", Toast.LENGTH_SHORT).show()
                    }
                }
            }
            .show()
    }

    private fun exportNotation() {
        val notation = gameManager.notationRecorder.exportNotation()
        if (notation.isBlank()) {
            Toast.makeText(this, "暂无棋谱记录", Toast.LENGTH_SHORT).show()
            return
        }
        val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        clipboard.setPrimaryClip(ClipData.newPlainText("棋谱", notation))
        Toast.makeText(this, "棋谱已复制到剪贴板", Toast.LENGTH_SHORT).show()
    }

    private fun showThemeDialog() {
        val themes = BoardTheme.getAllThemes()
        val names = themes.map { it.name }.toTypedArray()
        AlertDialog.Builder(this)
            .setTitle("选择主题")
            .setItems(names) { _, which ->
                boardView.theme = themes[which]
                getSharedPreferences("settings", MODE_PRIVATE)
                    .edit().putInt("theme_index", which).apply()
            }
            .show()
    }

    private fun updateStatus() {
        val modeText = when (gameManager.gameMode) {
            GameMode.PVE -> "人机对战 (${gameManager.difficulty.displayName})"
            GameMode.PUZZLE -> "残局模式"
            else -> "双人对战"
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
                tvNotation.text = ""
            }
            .setNegativeButton("返回主菜单") { _, _ ->
                finish()
            }
            .setNeutralButton("查看棋谱") { _, _ ->
                exportNotation()
            }
            .setCancelable(false)
            .show()
    }

    override fun onDestroy() {
        super.onDestroy()
        soundManager.release()
    }
}
