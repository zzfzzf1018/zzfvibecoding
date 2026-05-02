package com.chess.chinese

import android.content.Intent
import android.os.Bundle
import android.widget.Button
import android.widget.RadioButton
import android.widget.RadioGroup
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import com.chess.chinese.game.AIDifficulty
import com.chess.chinese.game.GameMode

class MainActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        val btnPvP = findViewById<Button>(R.id.btn_pvp)
        val btnPvE = findViewById<Button>(R.id.btn_pve)
        val rgDifficulty = findViewById<RadioGroup>(R.id.rg_difficulty)
        val tvTitle = findViewById<TextView>(R.id.tv_title)

        btnPvP.setOnClickListener {
            val intent = Intent(this, GameActivity::class.java)
            intent.putExtra("game_mode", GameMode.PVP.name)
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
    }
}
