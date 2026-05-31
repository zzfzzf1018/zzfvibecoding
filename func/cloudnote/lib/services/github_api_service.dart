import 'dart:convert';

import 'package:http/http.dart' as http;

import '../domain/entities/note_repo.dart';

typedef TokenProvider = String? Function();

class GitHubApiService {
  GitHubApiService({
    required this.authTokenProvider,
    http.Client? client,
  }) : _client = client ?? http.Client();

  final TokenProvider authTokenProvider;
  final http.Client _client;

  static const _base = 'https://api.github.com';

  Map<String, String> get _headers => {
        'Accept': 'application/vnd.github+json',
        'Authorization': 'Bearer ${authTokenProvider() ?? ''}',
        'X-GitHub-Api-Version': '2022-11-28',
      };

  Future<List<NoteRepo>> listOwnedRepos() async {
    final res = await _client.get(
      Uri.parse('$_base/user/repos?per_page=100&affiliation=owner'),
      headers: _headers,
    );
    if (res.statusCode != 200) {
      throw StateError('listOwnedRepos failed: ${res.statusCode} ${res.body}');
    }
    final arr = jsonDecode(res.body) as List<dynamic>;
    return arr.map((e) {
      final m = e as Map<String, dynamic>;
      return NoteRepo(
        name: m['name'] as String,
        owner: (m['owner'] as Map<String, dynamic>)['login'] as String,
        cloneUrl: m['clone_url'] as String,
        localPath: '',
        isPrivate: m['private'] as bool? ?? false,
      );
    }).toList();
  }

  /// Create a private repo for notes. Returns the new repo metadata.
  Future<NoteRepo> createRepo({
    required String name,
    String description = 'CloudNote notebook',
    bool private = true,
  }) async {
    final res = await _client.post(
      Uri.parse('$_base/user/repos'),
      headers: _headers,
      body: jsonEncode({
        'name': name,
        'description': description,
        'private': private,
        'auto_init': true,
      }),
    );
    if (res.statusCode != 201) {
      throw StateError('createRepo failed: ${res.statusCode} ${res.body}');
    }
    final m = jsonDecode(res.body) as Map<String, dynamic>;
    return NoteRepo(
      name: m['name'] as String,
      owner: (m['owner'] as Map<String, dynamic>)['login'] as String,
      cloneUrl: m['clone_url'] as String,
      localPath: '',
      isPrivate: m['private'] as bool? ?? true,
    );
  }
}
