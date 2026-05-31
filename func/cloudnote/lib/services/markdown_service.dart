import 'package:markdown/markdown.dart' as md;

class MarkdownService {
  /// Render markdown to HTML. Used both for preview and for HTML clipboard
  /// conversions in tests.
  String toHtml(String source) {
    return md.markdownToHtml(
      source,
      extensionSet: md.ExtensionSet.gitHubWeb,
      inlineSyntaxes: [md.InlineHtmlSyntax()],
    );
  }

  /// Very small HTML -> markdown helper that handles the common subset
  /// produced by browser clipboards: <p>, <br>, <strong>/<b>, <em>/<i>,
  /// <a>, <img>, <ul>/<ol>/<li>, <h1>..<h6>, <code>, <pre>.
  String htmlToMarkdown(String html) {
    var s = html;
    // Strip script/style entirely.
    s = s.replaceAll(
      RegExp(r'<(script|style)[^>]*>[\s\S]*?</\1>', caseSensitive: false),
      '',
    );

    String tag(String name, String repl) {
      s = s.replaceAllMapped(
        RegExp('<$name[^>]*>([\\s\\S]*?)</$name>', caseSensitive: false),
        (m) => repl.replaceAll(r'$1', m.group(1) ?? ''),
      );
      return s;
    }

    for (var i = 6; i >= 1; i--) {
      tag('h$i', '${'#' * i} \$1\n\n');
    }
    tag('strong', '**\$1**');
    tag('b', '**\$1**');
    tag('em', '*\$1*');
    tag('i', '*\$1*');
    tag('code', '`\$1`');
    tag('pre', '\n```\n\$1\n```\n');
    tag('li', '- \$1\n');
    tag('ul', '\$1\n');
    tag('ol', '\$1\n');
    tag('p', '\$1\n\n');
    s = s.replaceAll(RegExp(r'<br\s*/?>', caseSensitive: false), '  \n');

    // <a href="x">text</a> -> [text](x)
    s = s.replaceAllMapped(
      RegExp(r'<a[^>]*href="([^"]*)"[^>]*>([\s\S]*?)</a>',
          caseSensitive: false),
      (m) => '[${m.group(2)}](${m.group(1)})',
    );

    // <img src="x" alt="y"/> -> ![y](x)
    s = s.replaceAllMapped(
      RegExp(
          r'<img[^>]*?(?:alt="([^"]*)")?[^>]*?src="([^"]*)"[^>]*?>',
          caseSensitive: false),
      (m) => '![${m.group(1) ?? ''}](${m.group(2)})',
    );
    s = s.replaceAllMapped(
      RegExp(
          r'<img[^>]*?src="([^"]*)"[^>]*?(?:alt="([^"]*)")?[^>]*?>',
          caseSensitive: false),
      (m) => '![${m.group(2) ?? ''}](${m.group(1)})',
    );

    // Drop any remaining tags.
    s = s.replaceAll(RegExp(r'<[^>]+>'), '');

    // Decode minimal entities.
    s = s
        .replaceAll('&nbsp;', ' ')
        .replaceAll('&amp;', '&')
        .replaceAll('&lt;', '<')
        .replaceAll('&gt;', '>')
        .replaceAll('&quot;', '"')
        .replaceAll('&#39;', "'");

    // Collapse 3+ blank lines.
    s = s.replaceAll(RegExp(r'\n{3,}'), '\n\n').trim();
    return s;
  }
}
