import 'package:path/path.dart' as p;

enum NoteFormat { markdown, plainText }

class Note {
  Note({
    required this.id,
    required this.title,
    required this.body,
    required this.relativePath,
    required this.updatedAt,
    this.format = NoteFormat.markdown,
  });

  final String id;
  String title;
  String body;

  /// Path inside the repo, e.g. "notes/daily/2026-05-31.md".
  String relativePath;
  DateTime updatedAt;
  NoteFormat format;

  String get fileName => p.basename(relativePath);

  Note copyWith({String? title, String? body, DateTime? updatedAt}) => Note(
        id: id,
        title: title ?? this.title,
        body: body ?? this.body,
        relativePath: relativePath,
        updatedAt: updatedAt ?? this.updatedAt,
        format: format,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'title': title,
        'body': body,
        'relativePath': relativePath,
        'updatedAt': updatedAt.toIso8601String(),
        'format': format.name,
      };

  factory Note.fromJson(Map<String, dynamic> j) => Note(
        id: j['id'] as String,
        title: j['title'] as String,
        body: j['body'] as String,
        relativePath: j['relativePath'] as String,
        updatedAt: DateTime.parse(j['updatedAt'] as String),
        format: NoteFormat.values.firstWhere(
          (e) => e.name == (j['format'] ?? 'markdown'),
          orElse: () => NoteFormat.markdown,
        ),
      );
}
