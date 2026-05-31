import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/service_locator.dart';
import '../../domain/entities/note.dart';
import '../../services/clipboard_service.dart';
import '../providers/notes_provider.dart';
import '../widgets/markdown_preview.dart';

class EditorScreen extends StatefulWidget {
  const EditorScreen({super.key, required this.note});
  final Note note;

  @override
  State<EditorScreen> createState() => _EditorScreenState();
}

class _EditorScreenState extends State<EditorScreen> {
  late TextEditingController _title;
  late TextEditingController _body;
  bool _preview = false;
  bool _dirty = false;

  @override
  void initState() {
    super.initState();
    _title = TextEditingController(text: widget.note.title);
    _body = TextEditingController(text: widget.note.body);
    _title.addListener(() => _dirty = true);
    _body.addListener(() => _dirty = true);
  }

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final notes = context.read<NotesProvider>();
    widget.note
      ..title = _title.text.trim().isEmpty ? 'Untitled' : _title.text.trim()
      ..body = _body.text;
    await notes.save(widget.note);
    _dirty = false;
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Saved')),
      );
    }
  }

  Future<void> _pasteSmart() async {
    final notes = context.read<NotesProvider>();
    if (notes.repository == null) return;
    final svc = ClipboardService(
      markdownService: ServiceLocator.markdown,
      noteRepo: notes.repository!,
    );
    final paste = await svc.readAsMarkdown();
    if (paste.markdown == null) return;
    final selection = _body.selection;
    final insert = paste.markdown!;
    final text = _body.text;
    final start = selection.start < 0 ? text.length : selection.start;
    final end = selection.end < 0 ? text.length : selection.end;
    _body.text = text.replaceRange(start, end, insert);
    _body.selection =
        TextSelection.collapsed(offset: start + insert.length);
  }

  Future<bool> _confirmDiscard() async {
    if (!_dirty) return true;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Discard changes?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Keep editing'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Discard'),
          ),
        ],
      ),
    );
    return ok ?? false;
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        if (await _confirmDiscard() && mounted) Navigator.of(context).pop();
      },
      child: Scaffold(
        appBar: AppBar(
          title: TextField(
            controller: _title,
            decoration: const InputDecoration(
              hintText: 'Title',
              border: InputBorder.none,
            ),
          ),
          actions: [
            IconButton(
              tooltip: _preview ? 'Edit' : 'Preview',
              onPressed: () => setState(() => _preview = !_preview),
              icon: Icon(_preview ? Icons.edit : Icons.visibility),
            ),
            IconButton(
              tooltip: 'Paste from clipboard',
              onPressed: _pasteSmart,
              icon: const Icon(Icons.content_paste),
            ),
            IconButton(
              tooltip: 'Save',
              onPressed: _save,
              icon: const Icon(Icons.save),
            ),
          ],
        ),
        body: _preview
            ? Padding(
                padding: const EdgeInsets.all(16),
                child: MarkdownPreview(
                  source: _body.text,
                  assetsBasePath: _assetsBase(),
                ),
              )
            : Padding(
                padding: const EdgeInsets.all(12),
                child: TextField(
                  controller: _body,
                  maxLines: null,
                  expands: true,
                  keyboardType: TextInputType.multiline,
                  textAlignVertical: TextAlignVertical.top,
                  decoration: const InputDecoration(
                    hintText: 'Write markdown here…',
                    border: OutlineInputBorder(),
                  ),
                ),
              ),
      ),
    );
  }

  String? _assetsBase() {
    final repo = context.read<NotesProvider>().repository;
    if (repo == null) return null;
    // The FsNoteRepository keeps a known rootPath; expose via a cast for now.
    final dynamic any = repo;
    try {
      return any.rootPath as String;
    } catch (_) {
      return null;
    }
  }
}
