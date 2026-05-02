package com.chess.chinese.game

/**
 * 游戏模式
 */
enum class GameMode {
    PVP,  // 双人对战
    PVE   // 人机对战
}

/**
 * 游戏状态
 */
enum class GameState {
    PLAYING,
    RED_WIN,
    BLACK_WIN,
    DRAW
}

/**
 * 游戏管理器
 */
class GameManager(
    val gameMode: GameMode,
    val difficulty: AIDifficulty = AIDifficulty.MEDIUM,
    val playerColor: PieceColor = PieceColor.RED
) {
    val board = ChessBoard()
    val rules = ChessRules()
    private var ai: ChessAI? = null

    var currentTurn: PieceColor = PieceColor.RED
        private set
    var gameState: GameState = GameState.PLAYING
        private set
    var selectedRow: Int = -1
        private set
    var selectedCol: Int = -1
        private set
    var lastMove: Move? = null
        private set

    private val moveHistory = mutableListOf<Move>()

    // 回调
    var onBoardChanged: (() -> Unit)? = null
    var onGameOver: ((GameState) -> Unit)? = null
    var onAIThinking: ((Boolean) -> Unit)? = null

    init {
        if (gameMode == GameMode.PVE) {
            ai = ChessAI(difficulty)
        }
    }

    /**
     * 处理玩家点击
     */
    fun onCellClicked(row: Int, col: Int) {
        if (gameState != GameState.PLAYING) return
        if (gameMode == GameMode.PVE && currentTurn != playerColor) return

        val piece = board.getPiece(row, col)

        if (selectedRow >= 0 && selectedCol >= 0) {
            // 已选中棋子，尝试移动
            val selectedPiece = board.getPiece(selectedRow, selectedCol)
            if (selectedPiece != null && piece != null && piece.color == selectedPiece.color) {
                // 重新选择己方棋子
                selectedRow = row
                selectedCol = col
                onBoardChanged?.invoke()
                return
            }

            // 尝试走子
            val move = Move(selectedRow, selectedCol, row, col, piece)
            if (tryMove(move)) {
                selectedRow = -1
                selectedCol = -1
                onBoardChanged?.invoke()
                checkGameState()

                // AI回合
                if (gameMode == GameMode.PVE && gameState == GameState.PLAYING && currentTurn != playerColor) {
                    makeAIMove()
                }
            } else {
                // 非法走法，取消选中
                selectedRow = -1
                selectedCol = -1
                onBoardChanged?.invoke()
            }
        } else {
            // 选择棋子
            if (piece != null && piece.color == currentTurn) {
                selectedRow = row
                selectedCol = col
                onBoardChanged?.invoke()
            }
        }
    }

    /**
     * 尝试执行走法
     */
    private fun tryMove(move: Move): Boolean {
        val piece = board.getPiece(move.fromRow, move.fromCol) ?: return false
        if (piece.color != currentTurn) return false

        // 检查是否是合法走法
        val legalMoves = rules.generateMovesForPiece(board, move.fromRow, move.fromCol)
        val matchingMove = legalMoves.find { it.toRow == move.toRow && it.toCol == move.toCol }
            ?: return false

        // 检查走完后是否会被将军
        if (!rules.isMoveLegal(board, matchingMove, currentTurn)) return false

        // 执行走法
        val captured = board.movePiece(matchingMove)
        val recordedMove = Move(move.fromRow, move.fromCol, move.toRow, move.toCol, captured)
        moveHistory.add(recordedMove)
        lastMove = recordedMove

        // 切换回合
        currentTurn = if (currentTurn == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
        return true
    }

    /**
     * AI走棋
     */
    private fun makeAIMove() {
        onAIThinking?.invoke(true)
        Thread {
            // 使用棋盘副本进行AI搜索，避免与UI线程产生竞争条件
            val boardCopy = board.clone()
            val aiMove = ai?.getBestMove(boardCopy, currentTurn)
            if (aiMove != null) {
                val captured = board.movePiece(aiMove)
                val recordedMove = Move(aiMove.fromRow, aiMove.fromCol, aiMove.toRow, aiMove.toCol, captured)
                moveHistory.add(recordedMove)
                lastMove = recordedMove
                currentTurn = if (currentTurn == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
            }
            onAIThinking?.invoke(false)
            onBoardChanged?.invoke()
            checkGameState()
        }.start()
    }

    /**
     * 检查游戏状态
     */
    private fun checkGameState() {
        if (rules.isCheckmate(board, currentTurn)) {
            gameState = if (currentTurn == PieceColor.RED) GameState.BLACK_WIN else GameState.RED_WIN
            onGameOver?.invoke(gameState)
        }
    }

    /**
     * 悔棋
     */
    fun undoMove(): Boolean {
        if (moveHistory.isEmpty()) return false
        if (gameState != GameState.PLAYING) return false

        // 人机模式需要撤销两步
        val stepsToUndo = if (gameMode == GameMode.PVE && moveHistory.size >= 2) 2 else 1

        repeat(stepsToUndo) {
            if (moveHistory.isNotEmpty()) {
                val lastMoveRecord = moveHistory.removeAt(moveHistory.size - 1)
                board.undoMove(lastMoveRecord)
                currentTurn = if (currentTurn == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
            }
        }

        lastMove = moveHistory.lastOrNull()
        selectedRow = -1
        selectedCol = -1
        onBoardChanged?.invoke()
        return true
    }

    /**
     * 重新开始
     */
    fun restart() {
        board.initBoard()
        currentTurn = PieceColor.RED
        gameState = GameState.PLAYING
        selectedRow = -1
        selectedCol = -1
        lastMove = null
        moveHistory.clear()
        onBoardChanged?.invoke()
    }

    /**
     * 获取选中棋子的合法走法目标位置
     */
    fun getValidMoveTargets(): List<Pair<Int, Int>> {
        if (selectedRow < 0 || selectedCol < 0) return emptyList()
        val moves = rules.generateMovesForPiece(board, selectedRow, selectedCol)
        return moves.filter { rules.isMoveLegal(board, it, currentTurn) }
            .map { Pair(it.toRow, it.toCol) }
    }
}
