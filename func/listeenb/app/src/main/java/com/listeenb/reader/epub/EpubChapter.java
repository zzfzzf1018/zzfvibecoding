package com.listeenb.reader.epub;

public class EpubChapter {
    private final String title;
    private final String text;

    public EpubChapter(String title, String text) {
        this.title = title == null || title.trim().isEmpty() ? "章节" : title.trim();
        this.text = text == null ? "" : text.trim();
    }

    public String getTitle() {
        return title;
    }

    public String getText() {
        return text;
    }
}