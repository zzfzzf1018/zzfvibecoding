import 'package:cloudnote/domain/entities/note.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Note', () {
    test('roundtrips through JSON', () {
      final n = Note(
        id: 'abc',
        title: 'Hello',
        body: '# H1',
        relativePath: 'notes/hello.md',
        updatedAt: DateTime.parse('2026-05-31T10:00:00Z'),
      );
      final j = n.toJson();
      final back = Note.fromJson(j);
      expect(back.id, n.id);
      expect(back.title, n.title);
      expect(back.body, n.body);
      expect(back.relativePath, n.relativePath);
      expect(back.updatedAt, n.updatedAt);
      expect(back.format, NoteFormat.markdown);
    });

    test('copyWith updates title', () {
      final n = Note(
        id: '1',
        title: 't',
        body: 'b',
        relativePath: 'x.md',
        updatedAt: DateTime.now(),
      );
      final n2 = n.copyWith(title: 'new');
      expect(n2.title, 'new');
      expect(n2.body, 'b');
    });
  });
}
