import '../entities/note.dart';

abstract class NoteRepository {
  Future<List<Note>> listAll();
  Future<Note?> get(String id);
  Future<void> save(Note note);
  Future<void> delete(String id);

  /// Write attachment bytes (e.g. pasted images) under assets/ and return
  /// the relative path that should be used in the markdown body.
  Future<String> saveAttachment(String fileName, List<int> bytes);
}
