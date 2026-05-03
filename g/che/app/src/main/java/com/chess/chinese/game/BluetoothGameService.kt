package com.chess.chinese.game

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothManager
import android.bluetooth.BluetoothServerSocket
import android.bluetooth.BluetoothSocket
import android.content.Context
import android.os.Handler
import android.os.Looper
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.util.UUID

/**
 * 蓝牙对战服务 - 管理蓝牙连接和数据传输
 */
@SuppressLint("MissingPermission")
class BluetoothGameService(private val context: Context) {

    companion object {
        val SERVICE_UUID: UUID = UUID.fromString("fa87c0d0-afac-11eb-8529-0242ac130003")
        const val SERVICE_NAME = "ChineseChessGame"

        // 消息类型
        const val MSG_MOVE = "MOVE"       // 走棋: MOVE:fromRow,fromCol,toRow,toCol
        const val MSG_UNDO_REQ = "UNDO_REQ"   // 请求悔棋
        const val MSG_UNDO_OK = "UNDO_OK"     // 同意悔棋
        const val MSG_UNDO_NO = "UNDO_NO"     // 拒绝悔棋
        const val MSG_RESTART_REQ = "RESTART_REQ"
        const val MSG_RESTART_OK = "RESTART_OK"
        const val MSG_RESTART_NO = "RESTART_NO"
        const val MSG_RESIGN = "RESIGN"   // 认输

        // 连接状态
        const val STATE_NONE = 0
        const val STATE_LISTENING = 1
        const val STATE_CONNECTING = 2
        const val STATE_CONNECTED = 3
    }

    private val bluetoothManager = context.getSystemService(Context.BLUETOOTH_SERVICE) as BluetoothManager
    val bluetoothAdapter: BluetoothAdapter? = bluetoothManager.adapter

    private var acceptThread: AcceptThread? = null
    private var connectThread: ConnectThread? = null
    private var connectedThread: ConnectedThread? = null

    var state: Int = STATE_NONE
        private set

    private val handler = Handler(Looper.getMainLooper())

    // 回调
    var onStateChanged: ((Int) -> Unit)? = null
    var onMessageReceived: ((String) -> Unit)? = null
    var onError: ((String) -> Unit)? = null
    var onDeviceConnected: ((String) -> Unit)? = null

    val isBluetoothEnabled: Boolean
        get() = bluetoothAdapter?.isEnabled == true

    val pairedDevices: Set<BluetoothDevice>
        get() = bluetoothAdapter?.bondedDevices ?: emptySet()

    private fun setState(newState: Int) {
        state = newState
        handler.post { onStateChanged?.invoke(newState) }
    }

    /**
     * 作为服务端等待连接
     */
    @Synchronized
    fun startServer() {
        cancelAllThreads()
        acceptThread = AcceptThread()
        acceptThread?.start()
        setState(STATE_LISTENING)
    }

    /**
     * 连接到指定设备（作为客户端）
     */
    @Synchronized
    fun connectToDevice(device: BluetoothDevice) {
        if (state == STATE_CONNECTING) {
            connectThread?.cancel()
            connectThread = null
        }
        if (connectedThread != null) {
            connectedThread?.cancel()
            connectedThread = null
        }
        connectThread = ConnectThread(device)
        connectThread?.start()
        setState(STATE_CONNECTING)
    }

    /**
     * 连接建立后管理通信
     */
    @Synchronized
    private fun onConnected(socket: BluetoothSocket, deviceName: String) {
        cancelAllThreads()
        connectedThread = ConnectedThread(socket)
        connectedThread?.start()
        setState(STATE_CONNECTED)
        handler.post { onDeviceConnected?.invoke(deviceName) }
    }

    /**
     * 发送消息
     */
    fun sendMessage(message: String) {
        val thread: ConnectedThread?
        synchronized(this) {
            if (state != STATE_CONNECTED) return
            thread = connectedThread
        }
        thread?.write(message)
    }

    /**
     * 发送走棋指令
     */
    fun sendMove(fromRow: Int, fromCol: Int, toRow: Int, toCol: Int) {
        sendMessage("$MSG_MOVE:$fromRow,$fromCol,$toRow,$toCol")
    }

    /**
     * 停止所有线程
     */
    @Synchronized
    fun stop() {
        cancelAllThreads()
        setState(STATE_NONE)
    }

    private fun cancelAllThreads() {
        acceptThread?.cancel()
        acceptThread = null
        connectThread?.cancel()
        connectThread = null
        connectedThread?.cancel()
        connectedThread = null
    }

    private fun connectionFailed() {
        setState(STATE_NONE)
        handler.post { onError?.invoke("连接失败") }
    }

    private fun connectionLost() {
        setState(STATE_NONE)
        handler.post { onError?.invoke("连接已断开") }
    }

    /**
     * 服务端接受连接的线程
     */
    private inner class AcceptThread : Thread() {
        private val serverSocket: BluetoothServerSocket? = try {
            bluetoothAdapter?.listenUsingRfcommWithServiceRecord(SERVICE_NAME, SERVICE_UUID)
        } catch (e: IOException) {
            null
        }

        override fun run() {
            name = "AcceptThread"
            var socket: BluetoothSocket?
            while (this@BluetoothGameService.state != STATE_CONNECTED) {
                socket = try {
                    serverSocket?.accept()
                } catch (e: IOException) {
                    break
                }
                if (socket != null) {
                    synchronized(this@BluetoothGameService) {
                        when (this@BluetoothGameService.state) {
                            STATE_LISTENING, STATE_CONNECTING -> {
                                val device = socket.remoteDevice
                                onConnected(socket, device.name ?: "未知设备")
                            }
                            STATE_NONE, STATE_CONNECTED -> {
                                try { socket.close() } catch (_: IOException) {}
                            }
                            else -> {
                                try { socket.close() } catch (_: IOException) {}
                            }
                        }
                    }
                }
            }
        }

        fun cancel() {
            try { serverSocket?.close() } catch (_: IOException) {}
        }
    }

    /**
     * 客户端连接的线程
     */
    private inner class ConnectThread(private val device: BluetoothDevice) : Thread() {
        private val socket: BluetoothSocket? = try {
            device.createRfcommSocketToServiceRecord(SERVICE_UUID)
        } catch (e: IOException) {
            null
        }

        override fun run() {
            name = "ConnectThread"
            bluetoothAdapter?.cancelDiscovery()

            try {
                socket?.connect()
            } catch (e: IOException) {
                try { socket?.close() } catch (_: IOException) {}
                connectionFailed()
                return
            }

            synchronized(this@BluetoothGameService) {
                connectThread = null
            }

            if (socket != null) {
                onConnected(socket, device.name ?: "未知设备")
            }
        }

        fun cancel() {
            try { socket?.close() } catch (_: IOException) {}
        }
    }

    /**
     * 已连接后的通信线程
     */
    private inner class ConnectedThread(private val socket: BluetoothSocket) : Thread() {
        private val inputStream: InputStream? = try { socket.inputStream } catch (_: IOException) { null }
        private val outputStream: OutputStream? = try { socket.outputStream } catch (_: IOException) { null }

        override fun run() {
            name = "ConnectedThread"
            val buffer = ByteArray(1024)
            val messageBuffer = StringBuilder()

            while (true) {
                try {
                    val bytes = inputStream?.read(buffer) ?: break
                    if (bytes > 0) {
                        val received = String(buffer, 0, bytes)
                        messageBuffer.append(received)

                        // 按换行符分割消息
                        while (messageBuffer.contains("\n")) {
                            val newlineIdx = messageBuffer.indexOf("\n")
                            val message = messageBuffer.substring(0, newlineIdx).trim()
                            messageBuffer.delete(0, newlineIdx + 1)
                            if (message.isNotEmpty()) {
                                handler.post { onMessageReceived?.invoke(message) }
                            }
                        }
                    }
                } catch (e: IOException) {
                    connectionLost()
                    break
                }
            }
        }

        fun write(message: String) {
            try {
                outputStream?.write("$message\n".toByteArray())
                outputStream?.flush()
            } catch (e: IOException) {
                connectionLost()
            }
        }

        fun cancel() {
            try { socket.close() } catch (_: IOException) {}
        }
    }
}
