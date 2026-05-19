package com.lisb.reader.epub

import android.content.Context
import android.net.Uri
import nl.siegmann.epublib.domain.Book
import nl.siegmann.epublib.epub.EpubReader
import org.jsoup.Jsoup
import java.io.File
import java.io.FileOutputStream
import java.security.MessageDigest

/**
 * Loads an EPUB file, extracts plain-text chapters keyed by stable IDs.
 *
 * For rendering we strip down each chapter to body HTML, then inject our
 * own CSS at runtime in the WebView.
 */
class EpubBook private constructor(
    val id: String,
    val title: String,
    val author: String,
    val coverPngBase64: String?,
    val chapters: List<Chapter>
) {
    data class Chapter(
        val index: Int,
        val title: String,
        /** Clean body HTML (no <html><head>); safe to wrap in our template. */
        val bodyHtml: String,
        /** Plain text used by TTS / progress estimation. */
        val plainText: String
    )

    companion object {
        /** Imports a picked EPUB file from a content Uri into app storage and parses it. */
        fun importAndOpen(context: Context, uri: Uri): EpubBook {
            val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
                ?: error("Cannot read picked file")
            val id = sha1(bytes).take(16)
            val booksDir = File(context.filesDir, "books").apply { mkdirs() }
            val file = File(booksDir, "$id.epub")
            if (!file.exists()) {
                FileOutputStream(file).use { it.write(bytes) }
            }
            return open(context, file, id)
        }

        fun open(context: Context, file: File, id: String): EpubBook {
            val book: Book = file.inputStream().use { EpubReader().readEpub(it) }
            val title = book.title?.takeIf { it.isNotBlank() } ?: file.nameWithoutExtension
            val author = book.metadata.authors.joinToString(", ") { "${it.firstname} ${it.lastname}".trim() }
                .ifBlank { "未知作者" }

            val spine = book.spine.spineReferences
            val chapters = spine.mapIndexedNotNull { index, ref ->
                val res = ref.resource ?: return@mapIndexedNotNull null
                val raw = try { String(res.data, Charsets.UTF_8) } catch (_: Throwable) { return@mapIndexedNotNull null }
                val doc = Jsoup.parse(raw)
                // Remove scripts for safety
                doc.select("script, iframe, object, embed").remove()
                val body = doc.body() ?: return@mapIndexedNotNull null
                val plain = body.text()
                if (plain.isBlank()) return@mapIndexedNotNull null
                val chapterTitle = doc.selectFirst("h1, h2, h3")?.text()?.take(40)
                    ?: book.tableOfContents.tocReferences.getOrNull(index)?.title
                    ?: "第 ${index + 1} 章"
                Chapter(index, chapterTitle, body.html(), plain)
            }
            return EpubBook(id, title, author, null, chapters)
        }

        private fun sha1(bytes: ByteArray): String {
            val md = MessageDigest.getInstance("SHA-1")
            return md.digest(bytes).joinToString("") { "%02x".format(it) }
        }
    }
}
