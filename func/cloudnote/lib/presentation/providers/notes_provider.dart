import 'package:flutter/foundation.dart';
import 'package:uuid/uuid.dart';

import '../../data/repositories/fs_note_repository.dart';
import '../../domain/entities/note.dart';
import '../../domain/repositories/note_repository.dart';

class NotesProvider extends ChangeNotifier {
  NoteRepository? _repo;
  List<Note> _notes = [];
  Note? _current;
  bool _loading = false;
  final _uuid = const Uuid();

  List<Note> get notes => List.unmodifiable(_notes);
  Note? get current => _current;
  bool get loading => _loading;
  bool get hasRepo => _repo != null;

  NoteRepository? get repository => _repo;

  void bindRepository(String localPath) {
    _repo = FsNoteRepository(localPath);
    refresh();
  }

  Future<void> refresh() async {
    if (_repo == null) return;
    _loading = true;
    notifyListeners();
    _notes = await _repo!.listAll();
    _loading = false;
    notifyListeners();
  }

  Note createDraft() {
    final n = Note(
      id: _uuid.v4(),
      title: 'Untitled',
      body: '',
      relativePath: '',
      updatedAt: DateTime.now(),
    );
    _current = n;
    notifyListeners();
    return n;
  }

  void select(Note n) {
    _current = n;
    notifyListeners();
  }

  Future<void> save(Note n) async {
    if (_repo == null) return;
    n.updatedAt = DateTime.now();
    await _repo!.save(n);
    await refresh();
  }

  Future<void> delete(String id) async {
    if (_repo == null) return;
    await _repo!.delete(id);
    if (_current?.id == id) _current = null;
    await refresh();
  }
}
