package com.chess.chinese

import android.Manifest
import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.View
import android.view.ViewGroup
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import com.chess.chinese.game.BluetoothGameService
import com.chess.chinese.game.GameMode
import com.chess.chinese.game.PieceColor

/**
 * 蓝牙对战 - 设备发现和连接界面
 */
@SuppressLint("MissingPermission")
class BluetoothActivity : AppCompatActivity() {

    companion object {
        private const val REQUEST_ENABLE_BT = 1
        private const val REQUEST_PERMISSIONS = 2
        // 全局蓝牙服务实例（Activity间共享）
        var bluetoothService: BluetoothGameService? = null
    }

    private lateinit var tvStatus: TextView
    private lateinit var btnCreateRoom: Button
    private lateinit var btnScan: Button
    private lateinit var btnBack: Button
    private lateinit var progressBar: ProgressBar
    private lateinit var lvPairedDevices: ListView
    private lateinit var lvDiscoveredDevices: ListView
    private lateinit var tvPairedLabel: TextView
    private lateinit var tvDiscoveredLabel: TextView

    private val pairedDevices = mutableListOf<BluetoothDevice>()
    private val discoveredDevices = mutableListOf<BluetoothDevice>()
    private lateinit var pairedAdapter: DeviceListAdapter
    private lateinit var discoveredAdapter: DeviceListAdapter

    private var isHost = false  // 是否是房主（服务端）

    private val receiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context, intent: Intent) {
            when (intent.action) {
                BluetoothDevice.ACTION_FOUND -> {
                    val device = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE, BluetoothDevice::class.java)
                    } else {
                        @Suppress("DEPRECATION")
                        intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE)
                    }
                    device?.let {
                        if (!discoveredDevices.any { d -> d.address == it.address }
                            && !pairedDevices.any { d -> d.address == it.address }) {
                            discoveredDevices.add(it)
                            discoveredAdapter.notifyDataSetChanged()
                        }
                    }
                }
                BluetoothAdapter.ACTION_DISCOVERY_FINISHED -> {
                    progressBar.visibility = View.GONE
                    btnScan.isEnabled = true
                    if (discoveredDevices.isEmpty()) {
                        tvStatus.text = "未发现新设备，请确保对方设备可被发现"
                    }
                }
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_bluetooth)

        tvStatus = findViewById(R.id.tv_bt_status)
        btnCreateRoom = findViewById(R.id.btn_create_room)
        btnScan = findViewById(R.id.btn_scan)
        btnBack = findViewById(R.id.btn_bt_back)
        progressBar = findViewById(R.id.progress_bt)
        lvPairedDevices = findViewById(R.id.lv_paired)
        lvDiscoveredDevices = findViewById(R.id.lv_discovered)
        tvPairedLabel = findViewById(R.id.tv_paired_label)
        tvDiscoveredLabel = findViewById(R.id.tv_discovered_label)

        pairedAdapter = DeviceListAdapter(pairedDevices)
        discoveredAdapter = DeviceListAdapter(discoveredDevices)
        lvPairedDevices.adapter = pairedAdapter
        lvDiscoveredDevices.adapter = discoveredAdapter

        checkPermissions()
        setupBluetooth()
        setupButtons()

        val filter = IntentFilter().apply {
            addAction(BluetoothDevice.ACTION_FOUND)
            addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED)
        }
        registerReceiver(receiver, filter)
    }

    private fun checkPermissions() {
        val permissions = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.BLUETOOTH_CONNECT) != PackageManager.PERMISSION_GRANTED) {
                permissions.add(Manifest.permission.BLUETOOTH_CONNECT)
            }
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.BLUETOOTH_SCAN) != PackageManager.PERMISSION_GRANTED) {
                permissions.add(Manifest.permission.BLUETOOTH_SCAN)
            }
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.BLUETOOTH_ADVERTISE) != PackageManager.PERMISSION_GRANTED) {
                permissions.add(Manifest.permission.BLUETOOTH_ADVERTISE)
            }
        } else {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
                permissions.add(Manifest.permission.ACCESS_FINE_LOCATION)
            }
        }
        if (permissions.isNotEmpty()) {
            ActivityCompat.requestPermissions(this, permissions.toTypedArray(), REQUEST_PERMISSIONS)
        }
    }

    private fun setupBluetooth() {
        val service = BluetoothGameService(this)
        bluetoothService = service

        if (service.bluetoothAdapter == null) {
            tvStatus.text = "此设备不支持蓝牙"
            btnCreateRoom.isEnabled = false
            btnScan.isEnabled = false
            return
        }

        if (!service.isBluetoothEnabled) {
            val enableBtIntent = Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE)
            startActivityForResult(enableBtIntent, REQUEST_ENABLE_BT)
        } else {
            loadPairedDevices()
        }

        service.onStateChanged = { state ->
            runOnUiThread {
                when (state) {
                    BluetoothGameService.STATE_LISTENING -> tvStatus.text = "等待对方连接..."
                    BluetoothGameService.STATE_CONNECTING -> tvStatus.text = "正在连接..."
                    BluetoothGameService.STATE_CONNECTED -> {
                        // 已连接，进入游戏
                    }
                    BluetoothGameService.STATE_NONE -> tvStatus.text = "未连接"
                }
            }
        }

        service.onDeviceConnected = { deviceName ->
            runOnUiThread {
                tvStatus.text = "已连接: $deviceName"
                Toast.makeText(this, "连接成功！", Toast.LENGTH_SHORT).show()
                // 延迟一下启动游戏
                tvStatus.postDelayed({ startBluetoothGame() }, 500)
            }
        }

        service.onError = { error ->
            runOnUiThread {
                tvStatus.text = "错误: $error"
                progressBar.visibility = View.GONE
                Toast.makeText(this, error, Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun loadPairedDevices() {
        val service = bluetoothService ?: return
        pairedDevices.clear()
        pairedDevices.addAll(service.pairedDevices)
        pairedAdapter.notifyDataSetChanged()
        tvPairedLabel.visibility = if (pairedDevices.isEmpty()) View.GONE else View.VISIBLE
        lvPairedDevices.visibility = if (pairedDevices.isEmpty()) View.GONE else View.VISIBLE
    }

    private fun setupButtons() {
        btnCreateRoom.setOnClickListener {
            isHost = true
            tvStatus.text = "等待对方连接..."
            progressBar.visibility = View.VISIBLE

            // 使设备可被发现
            val discoverableIntent = Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE)
            discoverableIntent.putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, 120)
            startActivity(discoverableIntent)

            // 启动服务端
            bluetoothService?.startServer()
        }

        btnScan.setOnClickListener {
            isHost = false
            startDiscovery()
        }

        btnBack.setOnClickListener {
            bluetoothService?.stop()
            finish()
        }

        lvPairedDevices.setOnItemClickListener { _, _, position, _ ->
            isHost = false
            val device = pairedDevices[position]
            connectToDevice(device)
        }

        lvDiscoveredDevices.setOnItemClickListener { _, _, position, _ ->
            isHost = false
            val device = discoveredDevices[position]
            connectToDevice(device)
        }
    }

    private fun startDiscovery() {
        discoveredDevices.clear()
        discoveredAdapter.notifyDataSetChanged()
        progressBar.visibility = View.VISIBLE
        btnScan.isEnabled = false
        tvStatus.text = "正在搜索附近设备..."
        tvDiscoveredLabel.visibility = View.VISIBLE
        lvDiscoveredDevices.visibility = View.VISIBLE

        bluetoothService?.bluetoothAdapter?.let { adapter ->
            if (adapter.isDiscovering) {
                adapter.cancelDiscovery()
            }
            adapter.startDiscovery()
        }
    }

    private fun connectToDevice(device: BluetoothDevice) {
        bluetoothService?.bluetoothAdapter?.cancelDiscovery()
        tvStatus.text = "正在连接 ${device.name ?: device.address}..."
        progressBar.visibility = View.VISIBLE
        bluetoothService?.connectToDevice(device)
    }

    private fun startBluetoothGame() {
        val intent = Intent(this, GameActivity::class.java)
        intent.putExtra("game_mode", GameMode.BLUETOOTH.name)
        // 房主执红先行
        intent.putExtra("bt_is_host", isHost)
        intent.putExtra("player_color", if (isHost) PieceColor.RED.name else PieceColor.BLACK.name)
        startActivity(intent)
        finish()
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == REQUEST_ENABLE_BT) {
            if (resultCode == RESULT_OK) {
                loadPairedDevices()
                tvStatus.text = "蓝牙已开启，请创建房间或搜索设备"
            } else {
                tvStatus.text = "需要开启蓝牙才能使用此功能"
                btnCreateRoom.isEnabled = false
                btnScan.isEnabled = false
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        try {
            unregisterReceiver(receiver)
        } catch (_: Exception) {}
        bluetoothService?.bluetoothAdapter?.cancelDiscovery()
    }

    /**
     * 简单的设备列表适配器
     */
    private inner class DeviceListAdapter(
        private val devices: List<BluetoothDevice>
    ) : BaseAdapter() {
        override fun getCount() = devices.size
        override fun getItem(position: Int) = devices[position]
        override fun getItemId(position: Int) = position.toLong()

        override fun getView(position: Int, convertView: View?, parent: ViewGroup?): View {
            val tv = (convertView as? TextView) ?: TextView(this@BluetoothActivity).apply {
                setPadding(32, 24, 32, 24)
                textSize = 16f
                setTextColor(0xFF333333.toInt())
            }
            val device = devices[position]
            val name = device.name ?: "未知设备"
            val address = device.address
            tv.text = "$name\n$address"
            return tv
        }
    }
}
