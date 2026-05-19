package com.listeenb.reader.epub;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class EpubBook {
    private final String title;
    private final String author;
    private final byte[] coverImage;
    private final List<EpubChapter> chapters;

    public EpubBook(String title, String author, byte[] coverImage, List<EpubChapter> chapters) {
        this.title = title == null || title.trim().isEmpty() ? "未命名书籍" : title.trim();
        this.author = author == null || author.trim().isEmpty() ? "未知作者" : author.trim();
        this.coverImage = coverImage == null ? null : coverImage.clone();
        this.chapters = Collections.unmodifiableList(new ArrayList<>(chapters));
    }

    public String getTitle() {
        return title;
    }

    public String getAuthor() {
        return author;
    }

    public byte[] getCoverImage() {
        return coverImage == null ? null : coverImage.clone();
    }

    public List<EpubChapter> getChapters() {
        return chapters;
    }

    public boolean isEmpty() {
        return chapters.isEmpty();
    }
}