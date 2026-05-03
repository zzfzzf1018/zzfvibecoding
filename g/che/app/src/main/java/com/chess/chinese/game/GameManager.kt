package com.chess.chinese.game

/**
 * 走棋事件类型（用于音效和动画）
 */
enum class MoveEventType {
    MOVE,       // 普通走子
    CAPTURE,    // 吃子
    CHECK,      // 将军
    CHECKMATE   // 将杀
}

/**
 * 游戏模式
 */
enum class GameMode {
    PVP,        // 双人对战
    PVE,        // 人机对战
    PUZZLE,     // 残局模式
    BLUETOOTH   // 蓝牙对战
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
    val notationRecorder = NotationRecorder()
    private var ai: ChessAI? = null
    private val openingBook = OpeningBook()

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
    private var moveCount = 0

    // 回调
    var onBoardChanged: (() -> Unit)? = null
    var onGameOver: ((GameState) -> Unit)? = null
    var onAIThinking: ((Boolean) -> Unit)? = null
    var onMoveEvent: ((MoveEventType, Move) -> Unit)? = null
    var onNotationAdded: ((String) -> Unit)? = null
    var onLocalMove: ((Move) -> Unit)? = null  // 蓝牙模式：本地走棋后通知发送

    init {
        if (gameMode == GameMode.PVE || gameMode == GameMode.PUZZLE) {
            ai = ChessAI(difficulty)
        }
    }

    fun getMoveHistory(): List<Move> = moveHistory.toList()

    fun getMoveCount(): Int = moveCount

    /**
     * 加载状态（用于读档）
     */
    fun loadState(turn: PieceColor, moves: List<Move>) {
        currentTurn = turn
        moveHistory.clear()
        moveHistory.addAll(moves)
        moveCount = moves.size
        lastMove = moves.lastOrNull()
        gameState = GameState.PLAYING
        selectedRow = -1
        selectedCol = -1
        onBoardChanged?.invoke()
    }

    /**
     * 处理玩家点击
     */
    fun onCellClicked(row: Int, col: Int) {
        if (gameState != GameState.PLAYING) return
        if (gameMode == GameMode.PVE && currentTurn != playerColor) return
        if (gameMode == GameMode.PUZZLE && currentTurn != playerColor) return
        if (gameMode == GameMode.BLUETOOTH && currentTurn != playerColor) return

        val piece = board.getPiece(row, col)

        if (selectedRow >= 0 && selectedCol >= 0) {
            val selectedPiece = board.getPiece(selectedRow, selectedCol)
            if (selectedPiece != null && piece != null && piece.color == selectedPiece.color) {
                selectedRow = row
                selectedCol = col
                onBoardChanged?.invoke()
                return
            }

            val move = Move(selectedRow, selectedCol, row, col, piece)
            if (tryMove(move)) {
                // 蓝牙模式：走棋成功后通知远端
                if (gameMode == GameMode.BLUETOOTH) {
                    onLocalMove?.invoke(Move(move.fromRow, move.fromCol, row, col, null))
                }
                selectedRow = -1
                selectedCol = -1
                onBoardChanged?.invoke()
                checkGameState()

                if ((gameMode == GameMode.PVE || gameMode == GameMode.PUZZLE) &&
                    gameState == GameState.PLAYING && currentTurn != playerColor) {
                    makeAIMove()
                }
            } else {
                selectedRow = -1
                selectedCol = -1
                onBoardChanged?.invoke()
            }
        } else {
            if (piece != null && piece.color == currentTurn) {
                selectedRow = row
                selectedCol = col
                onBoardChanged?.invoke()
            }
        }
    }

    /**
     * 应用远端走棋（蓝牙对战收到对方走法）
     */
    fun applyRemoteMove(fromRow: Int, fromCol: Int, toRow: Int, toCol: Int): Boolean {
        if (gameState != GameState.PLAYING) return false
        if (gameMode != GameMode.BLUETOOTH) return false
        if (currentTurn == playerColor) return false  // 不是对方回合

        val captured = board.getPiece(toRow, toCol)
        val move = Move(fromRow, fromCol, toRow, toCol, captured)
        return tryMove(move).also {
            if (it) {
                selectedRow = -1
                selectedCol = -1
                onBoardChanged?.invoke()
                checkGameState()
            }
        }
    }

    /**
     * 获取AI提示（玩家可用）
     */
    fun getHint(): Move? {
        if (gameState != GameState.PLAYING) return null
        val hintAI = ChessAI(AIDifficulty.HARD)
        val boardCopy = board.clone()
        return hintAI.getBestMove(boardCopy, currentTurn)
    }

    /**
     * 尝试执行走法
     */
    private fun tryMove(move: Move): Boolean {
        val piece = board.getPiece(move.fromRow, move.fromCol) ?: return false
        if (piece.color != currentTurn) return false

        val legalMoves = rules.generateMovesForPiece(board, move.fromRow, move.fromCol)
        val matchingMove = legalMoves.find { it.toRow == move.toRow && it.toCol == move.toCol }
            ?: return false

        if (!rules.isMoveLegal(board, matchingMove, currentTurn)) return false

        // 记录棋谱
        val notation = notationRecorder.recordMove(board, matchingMove, piece)
        onNotationAdded?.invoke(notation)

        // 执行走法
        val captured = board.movePiece(matchingMove)
        val recordedMove = Move(move.fromRow, move.fromCol, move.toRow, move.toCol, captured)
        moveHistory.add(recordedMove)
        lastMove = recordedMove
        moveCount++

        // 判断事件类型
        val opponentColor = if (currentTurn == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
        val eventType = when {
            rules.isCheckmate(board, opponentColor) -> MoveEventType.CHECKMATE
            rules.isKingInCheck(board, opponentColor) -> MoveEventType.CHECK
            captured != null -> MoveEventType.CAPTURE
            else -> MoveEventType.MOVE
        }
        onMoveEvent?.invoke(eventType, recordedMove)

        currentTurn = opponentColor
        return true
    }

    /**
     * AI走棋
     */
    private fun makeAIMove() {
        onAIThinking?.invoke(true)
        Thread {
            // 先尝试开局库
            var aiMove: Move? = null
            if (gameMode == GameMode.PVE) {
                aiMove = openingBook.getOpeningMove(moveCount, currentTurn)
                if (aiMove != null) {
                    val piece = board.getPiece(aiMove.fromRow, aiMove.fromCol)
                    if (piece == null || piece.color != currentTurn) {
                        aiMove = null
                    } else if (!rules.isMoveLegal(board, aiMove, currentTurn)) {
                        aiMove = null
                    }
                }
            }

            if (aiMove == null) {
                val boardCopy = board.clone()
                aiMove = ai?.getBestMove(boardCopy, currentTurn)
            }

            if (aiMove != null) {
                val piece = board.getPiece(aiMove.fromRow, aiMove.fromCol)
                if (piece != null) {
                    val notation = notationRecorder.recordMove(board, aiMove, piece)
                    onNotationAdded?.invoke(notation)
                }

                val captured = board.movePiece(aiMove)
                val recordedMove = Move(aiMove.fromRow, aiMove.fromCol, aiMove.toRow, aiMove.toCol, captured)
                moveHistory.add(recordedMove)
                lastMove = recordedMove
                moveCount++

                val opponentColor = if (currentTurn == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
                val eventType = when {
                    rules.isCheckmate(board, opponentColor) -> MoveEventType.CHECKMATE
                    rules.isKingInCheck(board, opponentColor) -> MoveEventType.CHECK
                    captured != null -> MoveEventType.CAPTURE
                    else -> MoveEventType.MOVE
                }
                onMoveEvent?.invoke(eventType, recordedMove)

                currentTurn = opponentColor
            }
            onAIThinking?.invoke(false)
            onBoardChanged?.invoke()
            checkGameState()
        }.start()
    }

    private fun checkGameState() {
        if (rules.isCheckmate(board, currentTurn)) {
            gameState = if (currentTurn == PieceColor.RED) GameState.BLACK_WIN else GameState.RED_WIN
            onGameOver?.invoke(gameState)
        }
    }

    fun undoMove(): Boolean {
        if (moveHistory.isEmpty()) return false
        if (gameState != GameState.PLAYING) return false

        val stepsToUndo = if ((gameMode == GameMode.PVE || gameMode == GameMode.PUZZLE)
            && moveHistory.size >= 2) 2 else 1

        repeat(stepsToUndo) {
            if (moveHistory.isNotEmpty()) {
                val lastMoveRecord = moveHistory.removeAt(moveHistory.size - 1)
                board.undoMove(lastMoveRecord)
                currentTurn = if (currentTurn == PieceColor.RED) PieceColor.BLACK else PieceColor.RED
                moveCount--
                notationRecorder.removeLast()
            }
        }

        lastMove = moveHistory.lastOrNull()
        selectedRow = -1
        selectedCol = -1
        onBoardChanged?.invoke()
        return true
    }

    fun restart() {
        board.initBoard()
        currentTurn = PieceColor.RED
        gameState = GameState.PLAYING
        selectedRow = -1
        selectedCol = -1
        lastMove = null
        moveHistory.clear()
        moveCount = 0
        notationRecorder.clear()
        onBoardChanged?.invoke()
    }

    fun getValidMoveTargets(): List<Pair<Int, Int>> {
        if (selectedRow < 0 || selectedCol < 0) return emptyList()
        val moves = rules.generateMovesForPiece(board, selectedRow, selectedCol)
        return moves.filter { rules.isMoveLegal(board, it, currentTurn) }
            .map { Pair(it.toRow, it.toCol) }
    }
}
