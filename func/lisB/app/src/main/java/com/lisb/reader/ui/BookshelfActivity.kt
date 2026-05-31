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
import androidx.recyclerview.widget.ItemTouchHelper
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

        // Toolbar is kept in layout for compatibility but hidden; the new header
        // is a styled TextView block. Window title still uses app_name.
        title = getString(R.string.app_name)

        val rv = findViewById<RecyclerView>(R.id.shelfList)
        val empty = findViewById<View>(R.id.emptyHint)
        adapter = ShelfAdapter(
            onClick = { entry -> openBook(entry.id) },
            onLongClick = { entry -> confirmDelete(entry) }
        )
        rv.layoutManager = LinearLayoutManager(this)
        rv.adapter = adapter
        attachSwipeToDelete(rv)

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
        try {
            contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
        } catch (_: Throwable) {}
        // Cheap format gate before we even hit the EPUB parser.
        if (!looksLikeEpub(uri)) {
            Toast.makeText(this, R.string.import_unsupported, Toast.LENGTH_LONG).show()
            return
        }
        lifecycleScope.launch {
            val book = try {
                withContext(Dispatchers.IO) { EpubBook.importAndOpen(this@BookshelfActivity, uri) }
            } catch (t: Throwable) {
                Toast.makeText(this@BookshelfActivity, getString(R.string.import_unsupported), Toast.LENGTH_LONG).show()
                return@launch
            }
            if (book.chapters.isEmpty()) {
                // Cached file from importAndOpen is unusable: clean it up so it
                // doesn't show up later if the user reopens the same Uri.
                File(filesDir, "books/${book.id}.epub").delete()
                Toast.makeText(this@BookshelfActivity, R.string.import_empty, Toast.LENGTH_LONG).show()
                return@launch
            }
            settings.addToShelf(SettingsManager.ShelfEntry(book.id, book.title, book.author, System.currentTimeMillis()))
            refreshShelf(findViewById(R.id.emptyHint), findViewById(R.id.shelfList))
            openBook(book.id)
        }
    }

    /**
     * Returns true only if the picked URI looks like an EPUB: name ends with
     * .epub OR the resolver reports the official MIME. Cheap and avoids the
     * cost / noise of running the parser on, say, a JPG the user picked by
     * mistake.
     */
    private fun looksLikeEpub(uri: Uri): Boolean {
        val mime = contentResolver.getType(uri)?.lowercase()
        if (mime == "application/epub+zip") return true
        val name = queryDisplayName(uri)?.lowercase().orEmpty()
        return name.endsWith(".epub")
    }

    private fun queryDisplayName(uri: Uri): String? {
        return try {
            contentResolver.query(uri, arrayOf(android.provider.OpenableColumns.DISPLAY_NAME), null, null, null)
                ?.use { c -> if (c.moveToFirst()) c.getString(0) else null }
        } catch (_: Throwable) { null }
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

    /**
     * Swipe right on a book row to surface a delete confirmation. We use a
     * confirm dialog rather than instant-delete because the action is
     * destructive and not easily undone (the cached EPUB is removed too).
     */
    private fun attachSwipeToDelete(rv: RecyclerView) {
        val cb = object : ItemTouchHelper.SimpleCallback(0, ItemTouchHelper.RIGHT or ItemTouchHelper.LEFT) {
            override fun onMove(
                recyclerView: RecyclerView,
                viewHolder: RecyclerView.ViewHolder,
                target: RecyclerView.ViewHolder
            ): Boolean = false

            override fun onSwiped(viewHolder: RecyclerView.ViewHolder, direction: Int) {
                val pos = viewHolder.bindingAdapterPosition
                if (pos == RecyclerView.NO_POSITION) return
                val entry = adapter.getItem(pos) ?: run {
                    adapter.notifyItemChanged(pos); return
                }
                AlertDialog.Builder(this@BookshelfActivity)
                    .setTitle("删除《${entry.title}》？")
                    .setMessage("将同时删除本地缓存与阅读进度。")
                    .setOnCancelListener { adapter.notifyItemChanged(pos) }
                    .setNegativeButton("取消") { _, _ -> adapter.notifyItemChanged(pos) }
                    .setPositiveButton("删除") { _, _ ->
                        settings.removeFromShelf(entry.id)
                        File(filesDir, "books/${entry.id}.epub").delete()
                        refreshShelf(findViewById(R.id.emptyHint), findViewById(R.id.shelfList))
                    }.show()
            }
        }
        ItemTouchHelper(cb).attachToRecyclerView(rv)
    }

    private class ShelfAdapter(
        val onClick: (SettingsManager.ShelfEntry) -> Unit,
        val onLongClick: (SettingsManager.ShelfEntry) -> Unit
    ) : RecyclerView.Adapter<ShelfAdapter.VH>() {

        private val items = mutableListOf<SettingsManager.ShelfEntry>()

        fun submit(list: List<SettingsManager.ShelfEntry>) {
            items.clear(); items.addAll(list); notifyDataSetChanged()
        }

        fun getItem(position: Int): SettingsManager.ShelfEntry? =
            items.getOrNull(position)

        class VH(v: View) : RecyclerView.ViewHolder(v) {
            val title: TextView = v.findViewById(R.id.itemTitle)
            val author: TextView = v.findViewById(R.id.itemAuthor)
            val badge: TextView = v.findViewById(R.id.itemBadge)
        }

        override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): VH {
            val v = LayoutInflater.from(parent.context).inflate(R.layout.item_book, parent, false)
            return VH(v)
        }

        override fun onBindViewHolder(holder: VH, position: Int) {
            val e = items[position]
            holder.title.text = e.title
            holder.author.text = e.author
            holder.badge.text = e.title.firstOrNull()?.toString()?.uppercase() ?: "?"
            holder.itemView.setOnClickListener { onClick(e) }
            holder.itemView.setOnLongClickListener { onLongClick(e); true }
        }

        override fun getItemCount() = items.size
    }
}
