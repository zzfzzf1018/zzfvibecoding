import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;
import 'package:uuid/uuid.dart';

import '../../core/config.dart';
import '../../domain/entities/note.dart';
import '../../domain/repositories/note_repository.dart';

/// Filesystem-backed implementation rooted at a local clone of the user's
/// notes git repository. All notes live under `<root>/notes/`, attachments
/// under `<root>/assets/`. An index file keeps lightweight metadata.
class FsNoteRepository implements NoteRepository {
  FsNoteRepository(this.rootPath);

  final String rootPath;
  final Uuid _uuid = const Uuid();

  Directory get _notesDir =>
      Directory(p.join(rootPath, AppConfig.notesFolder));
  Directory get _assetsDir =>
      Directory(p.join(rootPath, AppConfig.assetsFolder));
  File get _indexFile => File(p.join(rootPath, '.cloudnote_index.json'));

  Future<void> _ensureDirs() async {
    await _notesDir.create(recursive: true);
    await _assetsDir.create(recursive: true);
  }

  Future<Map<String, Map<String, dynamic>>> _readIndex() async {
    if (!await _indexFile.exists()) return {};
    try {
      final raw = await _indexFile.readAsString();
      final m = jsonDecode(raw) as Map<String, dynamic>;
      return m.map((k, v) => MapEntry(k, Map<String, dynamic>.from(v as Map)));
    } catch (_) {
      return {};
    }
  }

  Future<void> _writeIndex(Map<String, Map<String, dynamic>> idx) async {
    await _indexFile.writeAsString(jsonEncode(idx));
  }

  @override
  Future<List<Note>> listAll() async {
    await _ensureDirs();
    final idx = await _readIndex();
    final notes = <Note>[];
    for (final entry in idx.entries) {
      final rel = entry.value['relativePath'] as String;
      final f = File(p.join(rootPath, rel));
      if (!await f.exists()) continue;
      final body = await f.readAsString();
      notes.add(Note(
        id: entry.key,
        title: entry.value['title'] as String,
        body: body,
        relativePath: rel,
        updatedAt: DateTime.parse(entry.value['updatedAt'] as String),
        format: NoteFormat.values.firstWhere(
          (e) => e.name == (entry.value['format'] ?? 'markdown'),
          orElse: () => NoteFormat.markdown,
        ),
      ));
    }
    notes.sort((a, b) => b.updatedAt.compareTo(a.updatedAt));
    return notes;
  }

  @override
  Future<Note?> get(String id) async {
    final all = await listAll();
    for (final n in all) {
      if (n.id == id) return n;
    }
    return null;
  }

  @override
  Future<void> save(Note note) async {
    await _ensureDirs();
    final idx = await _readIndex();

    if (note.relativePath.isEmpty) {
      final ext = note.format == NoteFormat.markdown ? 'md' : 'txt';
      final safe = note.title.trim().isEmpty
          ? 'untitled-${_uuid.v4().substring(0, 8)}'
          : note.title.replaceAll(RegExp(r'[^\w\-]+'), '_');
      note.relativePath = p.join(AppConfig.notesFolder, '$safe.$ext');
    }

    final file = File(p.join(rootPath, note.relativePath));
    await file.parent.create(recursive: true);
    await file.writeAsString(note.body);

    idx[note.id] = {
      'title': note.title,
      'relativePath': note.relativePath,
      'updatedAt': note.updatedAt.toIso8601String(),
      'format': note.format.name,
    };
    await _writeIndex(idx);
  }

  @override
  Future<void> delete(String id) async {
    final idx = await _readIndex();
    final meta = idx.remove(id);
    if (meta != null) {
      final f = File(p.join(rootPath, meta['relativePath'] as String));
      if (await f.exists()) await f.delete();
    }
    await _writeIndex(idx);
  }

  @override
  Future<String> saveAttachment(String fileName, List<int> bytes) async {
    await _ensureDirs();
    final unique = '${DateTime.now().millisecondsSinceEpoch}_$fileName';
    final rel = p.join(AppConfig.assetsFolder, unique);
    final f = File(p.join(rootPath, rel));
    await f.writeAsBytes(bytes, flush: true);
    return rel.replaceAll('\\', '/');
  }
}
