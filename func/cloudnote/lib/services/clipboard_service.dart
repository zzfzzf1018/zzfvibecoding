import 'package:flutter/services.dart';

import '../domain/repositories/note_repository.dart';
import 'markdown_service.dart';

class ClipboardPaste {
  ClipboardPaste({this.markdown, this.imageRelPaths = const []});
  final String? markdown;
  final List<String> imageRelPaths;
}

class RawClipboardImage {
  RawClipboardImage({required this.bytes, required this.suggestedName});
  final List<int> bytes;
  final String suggestedName;
}

/// Cross-platform clipboard reader. Uses Flutter's built-in [Clipboard] for
/// text/HTML; image bytes are supplied via an injected [imageProvider] so
/// platform channels can be wired per-OS without coupling this service to
/// any specific plugin. See docs/DESIGN.md ("Clipboard extension point").
class ClipboardService {
  ClipboardService({
    required this.markdownService,
    required this.noteRepo,
    Future<List<RawClipboardImage>> Function()? imageProvider,
  }) : _imageProvider = imageProvider ?? (() async => const []);

  final MarkdownService markdownService;
  final NoteRepository noteRepo;
  final Future<List<RawClipboardImage>> Function() _imageProvider;

  Future<ClipboardPaste> readAsMarkdown() async {
    final imagePaths = <String>[];
    for (final img in await _imageProvider()) {
      final rel = await noteRepo.saveAttachment(img.suggestedName, img.bytes);
      imagePaths.add(rel);
    }

    String? body;
    final data = await Clipboard.getData(Clipboard.kTextPlain);
    final raw = data?.text;
    if (raw != null && raw.isNotEmpty) {
      final looksHtml = RegExp(r'<[a-zA-Z][^>]*>').hasMatch(raw);
      body = looksHtml ? markdownService.htmlToMarkdown(raw) : raw;
    }

    final imgMd = imagePaths.map((p) => '![]($p)').join('\n');
    final combined = <String>[
      if (body != null && body.isNotEmpty) body,
      if (imgMd.isNotEmpty) imgMd,
    ].join('\n\n');

    return ClipboardPaste(
      markdown: combined.isEmpty ? null : combined,
      imageRelPaths: imagePaths,
    );
  }
}
