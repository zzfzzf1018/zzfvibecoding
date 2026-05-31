import 'package:cloudnote/services/markdown_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final svc = MarkdownService();

  group('MarkdownService.toHtml', () {
    test('renders headers and images', () {
      final html = svc.toHtml('# Hello\n\n![alt](img.png)');
      expect(html, contains('<h1'));
      expect(html, contains('<img'));
      expect(html, contains('src="img.png"'));
    });
  });

  group('MarkdownService.htmlToMarkdown', () {
    test('converts paragraphs and bold', () {
      final md = svc.htmlToMarkdown('<p>Hello <b>world</b></p>');
      expect(md.trim(), 'Hello **world**');
    });

    test('converts images', () {
      final md = svc.htmlToMarkdown('<img src="a.png" alt="logo"/>');
      expect(md.trim(), '![logo](a.png)');
    });

    test('converts links', () {
      final md = svc.htmlToMarkdown('<a href="https://x.io">x</a>');
      expect(md.trim(), '[x](https://x.io)');
    });

    test('strips scripts', () {
      final md = svc.htmlToMarkdown(
        '<p>hi</p><script>alert(1)</script>',
      );
      expect(md, isNot(contains('alert')));
    });

    test('handles entities', () {
      final md = svc.htmlToMarkdown('<p>a &amp; b &lt; c</p>');
      expect(md.trim(), 'a & b < c');
    });
  });
}
