import 'dart:io';

import 'package:cloudnote/data/repositories/fs_note_repository.dart';
import 'package:cloudnote/domain/entities/note.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:path/path.dart' as p;

void main() {
  late Directory tmp;
  late FsNoteRepository repo;

  setUp(() async {
    tmp = await Directory.systemTemp.createTemp('cloudnote_test_');
    repo = FsNoteRepository(tmp.path);
  });

  tearDown(() async {
    if (await tmp.exists()) await tmp.delete(recursive: true);
  });

  test('save then listAll roundtrips', () async {
    final n = Note(
      id: '1',
      title: 'Hello',
      body: '# h',
      relativePath: '',
      updatedAt: DateTime.now(),
    );
    await repo.save(n);
    final all = await repo.listAll();
    expect(all, hasLength(1));
    expect(all.first.title, 'Hello');
    expect(await File(p.join(tmp.path, n.relativePath)).exists(), isTrue);
  });

  test('delete removes file and index entry', () async {
    final n = Note(
      id: '2',
      title: 'Del',
      body: 'b',
      relativePath: '',
      updatedAt: DateTime.now(),
    );
    await repo.save(n);
    await repo.delete('2');
    final all = await repo.listAll();
    expect(all, isEmpty);
  });

  test('saveAttachment writes bytes under assets/', () async {
    final rel = await repo.saveAttachment('img.png', [1, 2, 3]);
    expect(rel, startsWith('assets/'));
    final f = File(p.join(tmp.path, rel));
    expect(await f.exists(), isTrue);
    expect(await f.length(), 3);
  });
}
