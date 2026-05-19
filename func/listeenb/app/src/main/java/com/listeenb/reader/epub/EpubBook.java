package com.listeenb.reader.epub;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class EpubBook {
    private final String title;
    private final List<EpubChapter> chapters;

    public EpubBook(String title, List<EpubChapter> chapters) {
        this.title = title == null || title.trim().isEmpty() ? "未命名书籍" : title.trim();
        this.chapters = Collections.unmodifiableList(new ArrayList<>(chapters));
    }

    public String getTitle() {
        return title;
    }

    public List<EpubChapter> getChapters() {
        return chapters;
    }

    public boolean isEmpty() {
        return chapters.isEmpty();
    }
}