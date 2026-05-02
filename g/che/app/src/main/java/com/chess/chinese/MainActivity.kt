package com.chess.chinese

import android.content.Intent
import android.os.Bundle
import android.widget.*
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import com.chess.chinese.game.*

class MainActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        val btnPvP = findViewById<Button>(R.id.btn_pvp)
        val btnPvE = findViewById<Button>(R.id.btn_pve)
        val btnPuzzle = findViewById<Button>(R.id.btn_puzzle)
        val btnLoad = findViewById<Button>(R.id.btn_load)
        val rgDifficulty = findViewById<RadioGroup>(R.id.rg_difficulty)
        val cbAutoFlip = findViewById<CheckBox>(R.id.cb_auto_flip)

        btnPvP.setOnClickListener {
            val intent = Intent(this, GameActivity::class.java)
            intent.putExtra("game_mode", GameMode.PVP.name)
            intent.putExtra("auto_flip", cbAutoFlip.isChecked)
            startActivity(intent)
        }

        btnPvE.setOnClickListener {
            val difficulty = when (rgDifficulty.checkedRadioButtonId) {
                R.id.rb_easy -> AIDifficulty.EASY
                R.id.rb_hard -> AIDifficulty.HARD
                else -> AIDifficulty.MEDIUM
            }
            val intent = Intent(this, GameActivity::class.java)
            intent.putExtra("game_mode", GameMode.PVE.name)
            intent.putExtra("difficulty", difficulty.name)
            startActivity(intent)
        }

        btnPuzzle.setOnClickListener {
            showPuzzleDialog()
        }

        btnLoad.setOnClickListener {
            showLoadDialog()
        }
    }

    private fun showPuzzleDialog() {
        val puzzles = PuzzleManager.getPuzzles()
        val names = puzzles.map { "${it.name} - ${it.description}" }.toTypedArray()
        AlertDialog.Builder(this)
            .setTitle("选择残局")
            .setItems(names) { _, which ->
                val intent = Intent(this, GameActivity::class.java)
                intent.putExtra("game_mode", GameMode.PUZZLE.name)
                intent.putExtra("puzzle_index", which)
                intent.putExtra("difficulty", AIDifficulty.HARD.name)
                startActivity(intent)
            }
            .setNegativeButton("取消", null)
            .show()
    }

    private fun showLoadDialog() {
        val saveManager = SaveManager(this)
        val saves = saveManager.getSaveList()
        if (saves.isEmpty()) {
            Toast.makeText(this, "没有存档记录", Toast.LENGTH_SHORT).show()
            return
        }
        val names = saves.map { it.name }.toTypedArray()
        AlertDialog.Builder(this)
            .setTitle("选择存档")
            .setItems(names) { _, which ->
                val save = saves[which]
                val intent = Intent(this, GameActivity::class.java)
                intent.putExtra("game_mode", save.gameMode)
                intent.putExtra("difficulty", save.difficulty)
                intent.putExtra("load_save_id", save.id)
                startActivity(intent)
            }
            .setNegativeButton("取消", null)
            .show()
    }
}
