package com.listeenb.reader.epub;

import android.content.ContentResolver;
import android.net.Uri;

import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.NamedNodeMap;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;
import org.xml.sax.InputSource;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.StringReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;

public class EpubParser {
    public EpubBook parse(ContentResolver resolver, Uri uri) throws Exception {
        Map<String, byte[]> entries = readZipEntries(resolver, uri);
        if (entries.isEmpty()) {
            throw new IOException("EPUB 文件为空或无法读取");
        }

        String rootFile = findRootFile(entries);
        Document packageDocument = parseXml(readText(entries, rootFile));
        String packageDir = parentDir(rootFile);
        String title = firstTextByName(packageDocument, "title");
        String author = firstTextByName(packageDocument, "creator");
        Map<String, ManifestItem> manifest = readManifest(packageDocument, packageDir);
        byte[] coverImage = findCoverImage(entries, packageDocument, manifest);
        List<String> spine = readSpine(packageDocument);
        List<EpubChapter> chapters = new ArrayList<>();

        for (String idRef : spine) {
            ManifestItem item = manifest.get(idRef);
            if (item == null || !isReadableXhtml(item)) {
                continue;
            }
            byte[] raw = entries.get(item.href);
            if (raw == null) {
                continue;
            }
            String html = new String(raw, StandardCharsets.UTF_8);
            String chapterText = extractReadableText(html);
            if (!chapterText.isEmpty()) {
                String chapterTitle = firstHeading(html);
                chapters.add(new EpubChapter(chapterTitle, chapterText));
            }
        }

        if (chapters.isEmpty()) {
            for (ManifestItem item : manifest.values()) {
                if (!isReadableXhtml(item) || !entries.containsKey(item.href)) {
                    continue;
                }
                String html = new String(entries.get(item.href), StandardCharsets.UTF_8);
                String chapterText = extractReadableText(html);
                if (!chapterText.isEmpty()) {
                    chapters.add(new EpubChapter(firstHeading(html), chapterText));
                }
            }
        }

        if (chapters.isEmpty()) {
            throw new IOException("未在 EPUB 中找到可阅读正文");
        }
        return new EpubBook(title, author, coverImage, chapters);
    }

    private Map<String, byte[]> readZipEntries(ContentResolver resolver, Uri uri) throws IOException {
        Map<String, byte[]> entries = new LinkedHashMap<>();
        InputStream stream = resolver.openInputStream(uri);
        if (stream == null) {
            throw new IOException("无法打开所选文件");
        }
        try (ZipInputStream zipInputStream = new ZipInputStream(stream)) {
            ZipEntry entry;
            byte[] buffer = new byte[8192];
            while ((entry = zipInputStream.getNextEntry()) != null) {
                if (entry.isDirectory()) {
                    continue;
                }
                ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
                int count;
                while ((count = zipInputStream.read(buffer)) != -1) {
                    outputStream.write(buffer, 0, count);
                }
                entries.put(normalizePath(entry.getName()), outputStream.toByteArray());
            }
        }
        return entries;
    }

    private String findRootFile(Map<String, byte[]> entries) throws Exception {
        byte[] container = entries.get("META-INF/container.xml");
        if (container == null) {
            throw new IOException("缺少 META-INF/container.xml，不是有效 EPUB");
        }
        Document document = parseXml(new String(container, StandardCharsets.UTF_8));
        NodeList rootFiles = document.getElementsByTagName("rootfile");
        for (int index = 0; index < rootFiles.getLength(); index++) {
            Node node = rootFiles.item(index);
            Node fullPath = node.getAttributes().getNamedItem("full-path");
            if (fullPath != null) {
                String path = normalizePath(fullPath.getTextContent());
                if (entries.containsKey(path)) {
                    return path;
                }
            }
        }
        throw new IOException("无法定位 EPUB 包描述文件");
    }

    private Document parseXml(String xml) throws Exception {
        DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
        factory.setNamespaceAware(false);
        disableExternalEntities(factory);
        DocumentBuilder builder = factory.newDocumentBuilder();
        return builder.parse(new InputSource(new StringReader(xml)));
    }

    private void disableExternalEntities(DocumentBuilderFactory factory) {
        try {
            factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
            factory.setFeature("http://xml.org/sax/features/external-general-entities", false);
            factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false);
            factory.setXIncludeAware(false);
            factory.setExpandEntityReferences(false);
        } catch (Exception ignored) {
        }
    }

    private String readText(Map<String, byte[]> entries, String path) throws IOException {
        byte[] data = entries.get(path);
        if (data == null) {
            throw new IOException("找不到 EPUB 内部文件: " + path);
        }
        return new String(data, StandardCharsets.UTF_8);
    }

    private Map<String, ManifestItem> readManifest(Document document, String packageDir) {
        Map<String, ManifestItem> items = new HashMap<>();
        NodeList manifests = document.getElementsByTagName("manifest");
        if (manifests.getLength() == 0) {
            return items;
        }
        NodeList children = manifests.item(0).getChildNodes();
        for (int index = 0; index < children.getLength(); index++) {
            Node node = children.item(index);
            if (!"item".equals(node.getNodeName())) {
                continue;
            }
            NamedNodeMap attributes = node.getAttributes();
            String id = attr(attributes, "id");
            String href = attr(attributes, "href");
            String mediaType = attr(attributes, "media-type");
            String properties = attr(attributes, "properties");
            if (!id.isEmpty() && !href.isEmpty()) {
                items.put(id, new ManifestItem(resolvePath(packageDir, href), mediaType, properties));
            }
        }
        return items;
    }

    private byte[] findCoverImage(Map<String, byte[]> entries, Document document, Map<String, ManifestItem> manifest) {
        String coverId = "";
        NodeList metas = document.getElementsByTagName("meta");
        for (int index = 0; index < metas.getLength(); index++) {
            Node node = metas.item(index);
            String name = attr(node.getAttributes(), "name");
            if ("cover".equalsIgnoreCase(name)) {
                coverId = attr(node.getAttributes(), "content");
                break;
            }
        }
        if (!coverId.isEmpty() && manifest.containsKey(coverId)) {
            byte[] image = entries.get(manifest.get(coverId).href);
            if (image != null) {
                return image;
            }
        }
        for (ManifestItem item : manifest.values()) {
            String mediaType = item.mediaType.toLowerCase(Locale.US);
            String href = item.href.toLowerCase(Locale.US);
            boolean likelyCover = item.properties.toLowerCase(Locale.US).contains("cover-image")
                    || (href.contains("cover") && mediaType.startsWith("image/"));
            if (likelyCover) {
                byte[] image = entries.get(item.href);
                if (image != null) {
                    return image;
                }
            }
        }
        return null;
    }

    private List<String> readSpine(Document document) {
        List<String> spine = new ArrayList<>();
        NodeList spines = document.getElementsByTagName("spine");
        if (spines.getLength() == 0) {
            return spine;
        }
        NodeList children = spines.item(0).getChildNodes();
        for (int index = 0; index < children.getLength(); index++) {
            Node node = children.item(index);
            if (!"itemref".equals(node.getNodeName())) {
                continue;
            }
            String idRef = attr(node.getAttributes(), "idref");
            if (!idRef.isEmpty()) {
                spine.add(idRef);
            }
        }
        return spine;
    }

    private boolean isReadableXhtml(ManifestItem item) {
        String lowerHref = item.href.toLowerCase(Locale.US);
        String lowerType = item.mediaType.toLowerCase(Locale.US);
        return lowerType.contains("xhtml")
                || lowerType.contains("html")
                || lowerHref.endsWith(".xhtml")
                || lowerHref.endsWith(".html")
                || lowerHref.endsWith(".htm");
    }

    private String extractReadableText(String html) {
        String normalized = html
                .replaceAll("(?is)<script[^>]*>.*?</script>", " ")
                .replaceAll("(?is)<style[^>]*>.*?</style>", " ")
                .replaceAll("(?i)<br\\s*/?>", "\n")
                .replaceAll("(?i)</p>", "\n\n")
                .replaceAll("(?i)</h[1-6]>", "\n\n")
                .replaceAll("(?i)</div>", "\n")
                .replaceAll("<[^>]+>", " ");
        return decodeEntities(normalized)
                .replace('\u00A0', ' ')
                .replaceAll("[ \\t\\x0B\\f\\r]+", " ")
                .replaceAll(" *\\n *", "\n")
                .replaceAll("\\n{3,}", "\n\n")
                .trim();
    }

    private String firstHeading(String html) {
        String text = html.replaceAll("(?is).*?<h[1-3][^>]*>(.*?)</h[1-3]>.*", "$1");
        if (text.equals(html)) {
            return "章节";
        }
        String heading = extractReadableText(text);
        return heading.isEmpty() ? "章节" : heading;
    }

    private String firstTextByName(Document document, String name) {
        NodeList nodes = document.getElementsByTagName(name);
        if (nodes.getLength() > 0) {
            return nodes.item(0).getTextContent();
        }
        NodeList all = document.getElementsByTagName("*");
        for (int index = 0; index < all.getLength(); index++) {
            Node node = all.item(index);
            String nodeName = node.getNodeName();
            if (node instanceof Element && (name.equals(nodeName) || nodeName.endsWith(":" + name))) {
                return node.getTextContent();
            }
        }
        return "未命名书籍";
    }

    private String decodeEntities(String text) {
        return text.replace("&nbsp;", " ")
                .replace("&amp;", "&")
                .replace("&lt;", "<")
                .replace("&gt;", ">")
                .replace("&quot;", "\"")
                .replace("&apos;", "'");
    }

    private String attr(NamedNodeMap attributes, String name) {
        if (attributes == null) {
            return "";
        }
        Node node = attributes.getNamedItem(name);
        return node == null ? "" : node.getTextContent();
    }

    private String resolvePath(String baseDir, String href) {
        String hrefWithoutAnchor = href.split("#", 2)[0];
        return normalizePath(baseDir.isEmpty() ? hrefWithoutAnchor : baseDir + "/" + hrefWithoutAnchor);
    }

    private String normalizePath(String path) {
        String[] parts = path.replace('\\', '/').split("/");
        List<String> clean = new ArrayList<>();
        for (String part : parts) {
            if (part.isEmpty() || ".".equals(part)) {
                continue;
            }
            if ("..".equals(part)) {
                if (!clean.isEmpty()) {
                    clean.remove(clean.size() - 1);
                }
            } else {
                clean.add(part);
            }
        }
        StringBuilder builder = new StringBuilder();
        for (int index = 0; index < clean.size(); index++) {
            if (index > 0) {
                builder.append('/');
            }
            builder.append(clean.get(index));
        }
        return builder.toString();
    }

    private String parentDir(String path) {
        int slash = path.lastIndexOf('/');
        return slash < 0 ? "" : path.substring(0, slash);
    }

    private static class ManifestItem {
        final String href;
        final String mediaType;
        final String properties;

        ManifestItem(String href, String mediaType, String properties) {
            this.href = href;
            this.mediaType = mediaType == null ? "" : mediaType;
            this.properties = properties == null ? "" : properties;
        }
    }
}