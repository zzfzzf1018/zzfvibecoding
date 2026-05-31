class NoteRepo {
  NoteRepo({
    required this.name,
    required this.owner,
    required this.cloneUrl,
    required this.localPath,
    this.isPrivate = true,
  });

  final String name;
  final String owner;
  final String cloneUrl;
  final String localPath;
  final bool isPrivate;

  String get fullName => '$owner/$name';

  Map<String, dynamic> toJson() => {
        'name': name,
        'owner': owner,
        'cloneUrl': cloneUrl,
        'localPath': localPath,
        'isPrivate': isPrivate,
      };

  factory NoteRepo.fromJson(Map<String, dynamic> j) => NoteRepo(
        name: j['name'] as String,
        owner: j['owner'] as String,
        cloneUrl: j['cloneUrl'] as String,
        localPath: j['localPath'] as String,
        isPrivate: j['isPrivate'] as bool? ?? true,
      );
}
