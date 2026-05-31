import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import 'package:path/path.dart' as p;
import 'package:provider/provider.dart';

import '../providers/settings_provider.dart';

/// Renders markdown with images. Image references can be:
///   * http(s) URLs   -> network image
///   * relative paths -> resolved against [assetsBasePath] (the repo root)
class MarkdownPreview extends StatelessWidget {
  const MarkdownPreview({
    super.key,
    required this.source,
    this.assetsBasePath,
  });

  final String source;
  final String? assetsBasePath;

  @override
  Widget build(BuildContext context) {
    final settings = context.watch<SettingsProvider>();
    return Markdown(
      data: source,
      selectable: true,
      styleSheet: MarkdownStyleSheet.fromTheme(Theme.of(context)).copyWith(
        p: Theme.of(context).textTheme.bodyMedium?.copyWith(
              height: settings.paragraphSpacing,
              color: settings.fontColor,
            ),
      ),
      imageBuilder: (uri, title, alt) {
        final s = uri.toString();
        if (s.startsWith('http://') || s.startsWith('https://')) {
          return Image.network(s);
        }
        if (assetsBasePath != null) {
          final full = p.join(assetsBasePath!, s);
          final f = File(full);
          if (f.existsSync()) return Image.file(f);
        }
        return Text('[image: $alt]');
      },
    );
  }
}
