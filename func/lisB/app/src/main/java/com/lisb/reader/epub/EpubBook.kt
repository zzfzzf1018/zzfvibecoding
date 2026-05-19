package com.lisb.reader.epub

import android.content.Context
import android.net.Uri
import android.util.Base64
import nl.siegmann.epublib.domain.Book
import nl.siegmann.epublib.domain.Resource
import nl.siegmann.epublib.epub.EpubReader
import org.jsoup.Jsoup
import org.jsoup.nodes.Document
import java.io.File
import java.io.FileOutputStream
import java.security.MessageDigest

/**
 * Loads an EPUB file and extracts each spine chapter into two forms:
 *  - [Chapter.bodyHtml] / [Chapter.headHtml]: the EPUB's original markup, with
 *    linked stylesheets and image resources INLINED so the chapter renders
 *    faithfully inside a single WebView document (no base URL → external
 *    relative refs would otherwise 404). This preserves the publisher's
 *    typography when the user enables "preserve original style".
 *  - [Chapter.plainText]: clean text for TTS.
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
        /** Original <head> contents (style + meta), with linked CSS inlined as <style>. */
        val headHtml: String,
        /** Original <body> inner HTML, with <img src> rewritten to data: URIs. */
        val bodyHtml: String,
        /** Plain text used by TTS / progress estimation. */
        val plainText: String
    )

    companion object {
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
            val author = book.metadata.authors.joinToString(", ") {
                "${it.firstname} ${it.lastname}".trim()
            }.ifBlank { "未知作者" }

            val spine = book.spine.spineReferences
            val chapters = spine.mapIndexedNotNull { index, ref ->
                val res = ref.resource ?: return@mapIndexedNotNull null
                val raw = try { String(res.data, Charsets.UTF_8) }
                catch (_: Throwable) { return@mapIndexedNotNull null }
                val doc = Jsoup.parse(raw)
                // Strip active content for safety
                doc.select("script, iframe, object, embed").remove()

                val body = doc.body() ?: return@mapIndexedNotNull null
                val plain = body.text()
                if (plain.isBlank()) return@mapIndexedNotNull null

                inlineStylesheets(doc, book, res)
                inlineImages(doc, book, res)

                val chapterTitle = doc.selectFirst("h1, h2, h3")?.text()?.take(40)
                    ?: book.tableOfContents.tocReferences.getOrNull(index)?.title
                    ?: "第 ${index + 1} 章"

                val headHtml = doc.head()?.html().orEmpty()
                Chapter(index, chapterTitle, headHtml, body.html(), plain)
            }
            return EpubBook(id, title, author, null, chapters)
        }

        /** Replace <link rel="stylesheet" href="..."> with inline <style>...</style>. */
        private fun inlineStylesheets(doc: Document, book: Book, chapterRes: Resource) {
            val links = doc.select("link[rel=stylesheet][href], link[href][type=text/css]")
            for (link in links) {
                val href = link.attr("href")
                if (href.isBlank()) continue
                val css = readResourceText(book, chapterRes.href, href) ?: continue
                val style = doc.createElement("style").apply {
                    attr("type", "text/css")
                    appendText("\n$css\n")
                }
                link.replaceWith(style)
            }
        }

        /** Inline <img src> and SVG <image href> as data: URIs so they render without a base URL. */
        private fun inlineImages(doc: Document, book: Book, chapterRes: Resource) {
            for (img in doc.select("img[src]")) {
                val src = img.attr("src")
                if (src.isBlank() || src.startsWith("data:")) continue
                val bytes = readResourceBytes(book, chapterRes.href, src) ?: continue
                img.attr("src", toDataUri(guessImageMime(src), bytes))
            }
            for (img in doc.select("image[href], image[xlink|href]")) {
                val src = img.attr("href").ifBlank { img.attr("xlink:href") }
                if (src.isBlank() || src.startsWith("data:")) continue
                val bytes = readResourceBytes(book, chapterRes.href, src) ?: continue
                val dataUri = toDataUri(guessImageMime(src), bytes)
                if (img.hasAttr("href")) img.attr("href", dataUri)
                if (img.hasAttr("xlink:href")) img.attr("xlink:href", dataUri)
            }
        }

        private fun toDataUri(mime: String, bytes: ByteArray): String {
            val b64 = Base64.encodeToString(bytes, Base64.NO_WRAP)
            return "data:$mime;base64,$b64"
        }

        private fun guessImageMime(href: String): String {
            val lower = href.substringBefore('?').lowercase()
            return when {
                lower.endsWith(".png") -> "image/png"
                lower.endsWith(".jpg") || lower.endsWith(".jpeg") -> "image/jpeg"
                lower.endsWith(".gif") -> "image/gif"
                lower.endsWith(".webp") -> "image/webp"
                lower.endsWith(".svg") -> "image/svg+xml"
                else -> "image/*"
            }
        }

        private fun readResourceText(book: Book, baseHref: String, relHref: String): String? {
            val bytes = readResourceBytes(book, baseHref, relHref) ?: return null
            return try { String(bytes, Charsets.UTF_8) } catch (_: Throwable) { null }
        }

        /** Resolve a relative href against the chapter href and look it up in the EPUB. */
        private fun readResourceBytes(book: Book, baseHref: String, relHref: String): ByteArray? {
            if (relHref.startsWith("http://") || relHref.startsWith("https://")) return null
            val resolved = resolveHref(baseHref, relHref) ?: return null
            val candidates = listOf(
                resolved,
                resolved.removePrefix("/"),
                java.net.URLDecoder.decode(resolved, "UTF-8"),
                java.net.URLDecoder.decode(resolved.removePrefix("/"), "UTF-8")
            ).distinct()
            for (c in candidates) {
                val r = book.resources.getByHref(c) ?: continue
                return r.data
            }
            // Last resort: match by basename only
            val name = resolved.substringAfterLast('/')
            for (r in book.resources.all) {
                if (r.href?.substringAfterLast('/') == name) return r.data
            }
            return null
        }

        private fun resolveHref(base: String, rel: String): String? {
            if (rel.isBlank()) return null
            val cleanRel = rel.substringBefore('#')
            if (cleanRel.startsWith("/")) return cleanRel.removePrefix("/")
            val baseDir = base.substringBeforeLast('/', missingDelimiterValue = "")
            val joined = if (baseDir.isEmpty()) cleanRel else "$baseDir/$cleanRel"
            val parts = mutableListOf<String>()
            for (seg in joined.split('/')) {
                when (seg) {
                    "", "." -> {}
                    ".." -> if (parts.isNotEmpty()) parts.removeAt(parts.lastIndex)
                    else -> parts.add(seg)
                }
            }
            return parts.joinToString("/")
        }

        private fun sha1(bytes: ByteArray): String {
            val md = MessageDigest.getInstance("SHA-1")
            return md.digest(bytes).joinToString("") { "%02x".format(it) }
        }
    }
}
