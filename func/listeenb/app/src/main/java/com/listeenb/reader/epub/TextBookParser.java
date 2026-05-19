package com.listeenb.reader.epub;

import android.content.ContentResolver;
import android.net.Uri;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

public class TextBookParser {
    private static final int CHAPTER_TARGET_LENGTH = 9000;

    public EpubBook parse(ContentResolver resolver, Uri uri, String displayName) throws Exception {
        byte[] data;
        try (InputStream inputStream = resolver.openInputStream(uri)) {
            if (inputStream == null) {
                throw new IllegalArgumentException("无法打开 TXT 文件");
            }
            ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = inputStream.read(buffer)) != -1) {
                outputStream.write(buffer, 0, count);
            }
            data = outputStream.toByteArray();
        }

        String text = decodeText(data)
                .replace("\r\n", "\n")
                .replace('\r', '\n')
                .replaceAll("[ \\t\\u000B\\f]+", " ")
                .replaceAll("\n{4,}", "\n\n\n")
                .trim();
        if (text.isEmpty()) {
            throw new IllegalArgumentException("TXT 文件没有可阅读内容");
        }
        return new EpubBook(cleanTitle(displayName), "本地 TXT", null, splitChapters(text));
    }

    private String decodeText(byte[] data) {
        if (data.length >= 3 && (data[0] & 0xFF) == 0xEF && (data[1] & 0xFF) == 0xBB && (data[2] & 0xFF) == 0xBF) {
            return new String(data, 3, data.length - 3, StandardCharsets.UTF_8);
        }
        String utf8 = new String(data, StandardCharsets.UTF_8);
        int replacementCount = 0;
        for (int index = 0; index < utf8.length(); index++) {
            if (utf8.charAt(index) == '\uFFFD') {
                replacementCount++;
            }
        }
        if (replacementCount > Math.max(8, utf8.length() / 50)) {
            return new String(data, Charset.forName("GBK"));
        }
        return utf8;
    }

    private List<EpubChapter> splitChapters(String text) {
        List<EpubChapter> chapters = new ArrayList<>();
        String[] blocks = text.split("(?m)(?=^\\s*(第[一二三四五六七八九十百千万0-9]+[章节回卷部篇].*|Chapter\\s+\\d+.*)$)");
        if (blocks.length > 1) {
            for (String block : blocks) {
                addChapter(chapters, block.trim());
            }
        }
        if (chapters.isEmpty()) {
            int start = 0;
            int chapterNumber = 1;
            while (start < text.length()) {
                int end = Math.min(text.length(), start + CHAPTER_TARGET_LENGTH);
                if (end < text.length()) {
                    int paragraphEnd = text.indexOf("\n\n", end);
                    if (paragraphEnd > end && paragraphEnd - end < 1600) {
                        end = paragraphEnd;
                    }
                }
                String chunk = text.substring(start, end).trim();
                if (!chunk.isEmpty()) {
                    chapters.add(new EpubChapter("TXT 章节 " + chapterNumber, chunk));
                    chapterNumber++;
                }
                start = end;
            }
        }
        return chapters;
    }

    private void addChapter(List<EpubChapter> chapters, String block) {
        if (block.isEmpty()) {
            return;
        }
        String title = "TXT 章节 " + (chapters.size() + 1);
        int newline = block.indexOf('\n');
        if (newline > 0 && newline < 80) {
            title = block.substring(0, newline).trim();
        }
        chapters.add(new EpubChapter(title, block));
    }

    private String cleanTitle(String displayName) {
        if (displayName == null || displayName.trim().isEmpty()) {
            return "TXT 书籍";
        }
        return displayName.replaceAll("(?i)\\.txt$", "").trim();
    }
}
