package com.lisb.reader.ui

import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.lisb.reader.R
import com.lisb.reader.data.SettingsManager
import com.lisb.reader.epub.EpubBook
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import androidx.lifecycle.lifecycleScope

class BookshelfActivity : AppCompatActivity() {

    private lateinit var settings: SettingsManager
    private lateinit var adapter: ShelfAdapter

    private val pickEpub = registerForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) importEpub(uri)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_bookshelf)
        settings = SettingsManager.get(this)

        setSupportActionBar(findViewById(R.id.action_bar))
        title = getString(R.string.app_name)

        val rv = findViewById<RecyclerView>(R.id.shelfList)
        val empty = findViewById<View>(R.id.emptyHint)
        adapter = ShelfAdapter(
            onClick = { entry -> openBook(entry.id) },
            onLongClick = { entry -> confirmDelete(entry) }
        )
        rv.layoutManager = LinearLayoutManager(this)
        rv.adapter = adapter

        findViewById<View>(R.id.fabImport).setOnClickListener {
            pickEpub.launch(arrayOf("application/epub+zip", "application/octet-stream", "*/*"))
        }

        refreshShelf(empty, rv)
    }

    override fun onResume() {
        super.onResume()
        refreshShelf(findViewById(R.id.emptyHint), findViewById(R.id.shelfList))
    }

    private fun refreshShelf(empty: View, rv: View) {
        val list = settings.getShelf().sortedByDescending { it.addedAt }
        adapter.submit(list)
        empty.visibility = if (list.isEmpty()) View.VISIBLE else View.GONE
        rv.visibility = if (list.isEmpty()) View.GONE else View.VISIBLE
    }

    private fun importEpub(uri: Uri) {
        // Persist read permission so we can re-open if needed (not strictly required since we copy)
        try {
            contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
        } catch (_: Throwable) {}
        lifecycleScope.launch {
            val book = try {
                withContext(Dispatchers.IO) { EpubBook.importAndOpen(this@BookshelfActivity, uri) }
            } catch (t: Throwable) {
                Toast.makeText(this@BookshelfActivity, "导入失败：${t.message}", Toast.LENGTH_LONG).show()
                return@launch
            }
            settings.addToShelf(SettingsManager.ShelfEntry(book.id, book.title, book.author, System.currentTimeMillis()))
            refreshShelf(findViewById(R.id.emptyHint), findViewById(R.id.shelfList))
            openBook(book.id)
        }
    }

    private fun openBook(id: String) {
        val intent = Intent(this, ReaderActivity::class.java).putExtra(ReaderActivity.EXTRA_BOOK_ID, id)
        startActivity(intent)
    }

    private fun confirmDelete(entry: SettingsManager.ShelfEntry) {
        AlertDialog.Builder(this)
            .setTitle("删除《${entry.title}》？")
            .setMessage("将同时删除本地缓存与阅读进度。")
            .setNegativeButton("取消", null)
            .setPositiveButton("删除") { _, _ ->
                settings.removeFromShelf(entry.id)
                File(filesDir, "books/${entry.id}.epub").delete()
                refreshShelf(findViewById(R.id.emptyHint), findViewById(R.id.shelfList))
            }.show()
    }

    private class ShelfAdapter(
        val onClick: (SettingsManager.ShelfEntry) -> Unit,
        val onLongClick: (SettingsManager.ShelfEntry) -> Unit
    ) : RecyclerView.Adapter<ShelfAdapter.VH>() {

        private val items = mutableListOf<SettingsManager.ShelfEntry>()

        fun submit(list: List<SettingsManager.ShelfEntry>) {
            items.clear(); items.addAll(list); notifyDataSetChanged()
        }

        class VH(v: View) : RecyclerView.ViewHolder(v) {
            val title: TextView = v.findViewById(R.id.itemTitle)
            val author: TextView = v.findViewById(R.id.itemAuthor)
        }

        override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): VH {
            val v = LayoutInflater.from(parent.context).inflate(R.layout.item_book, parent, false)
            return VH(v)
        }

        override fun onBindViewHolder(holder: VH, position: Int) {
            val e = items[position]
            holder.title.text = e.title
            holder.author.text = e.author
            holder.itemView.setOnClickListener { onClick(e) }
            holder.itemView.setOnLongClickListener { onLongClick(e); true }
        }

        override fun getItemCount() = items.size
    }
}
