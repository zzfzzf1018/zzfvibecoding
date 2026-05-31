import 'dart:io';

/// Abstract Git operations. Two backends are envisioned:
///  * [ProcessGitService] – shells out to the system `git` binary. Works on
///    Windows and Linux/macOS desktops where git is installed.
///  * A future REST-API-only backend for Android, committing through the
///    GitHub Contents API when no native git binary is available.
abstract class GitService {
  Future<void> clone({
    required String url,
    required String dest,
    String? token,
  });

  Future<void> pull({required String repoPath, String? token});

  Future<bool> hasChanges({required String repoPath});

  Future<void> commitAll({
    required String repoPath,
    required String message,
    required String authorName,
    required String authorEmail,
  });

  Future<void> push({
    required String repoPath,
    required String url,
    String? token,
  });
}

class GitException implements Exception {
  GitException(this.message, [this.stderr]);
  final String message;
  final String? stderr;
  @override
  String toString() =>
      'GitException: $message${stderr == null ? '' : '\n$stderr'}';
}

class ProcessGitService implements GitService {
  Future<ProcessResult> _run(
    List<String> args, {
    String? cwd,
    Map<String, String>? env,
  }) async {
    final res = await Process.run(
      'git',
      args,
      workingDirectory: cwd,
      environment: env,
      runInShell: true,
    );
    if (res.exitCode != 0) {
      throw GitException(
        'git ${args.join(' ')} failed (exit ${res.exitCode})',
        res.stderr.toString(),
      );
    }
    return res;
  }

  String _authedUrl(String url, String? token) {
    if (token == null || token.isEmpty) return url;
    final u = Uri.parse(url);
    return u.replace(userInfo: 'x-access-token:$token').toString();
  }

  @override
  Future<void> clone({
    required String url,
    required String dest,
    String? token,
  }) async {
    await _run(['clone', _authedUrl(url, token), dest]);
    // Scrub credentials from .git/config so the token never sits on disk.
    await _run(['remote', 'set-url', 'origin', url], cwd: dest);
  }

  @override
  Future<void> pull({required String repoPath, String? token}) async {
    final origUrl = await _remoteUrl(repoPath);
    if (token != null && token.isNotEmpty) {
      await _run(['remote', 'set-url', 'origin', _authedUrl(origUrl, token)],
          cwd: repoPath);
    }
    try {
      await _run(['pull', '--rebase', '--autostash'], cwd: repoPath);
    } finally {
      await _run(['remote', 'set-url', 'origin', origUrl], cwd: repoPath);
    }
  }

  Future<String> _remoteUrl(String repoPath) async {
    final res = await Process.run(
      'git',
      ['remote', 'get-url', 'origin'],
      workingDirectory: repoPath,
      runInShell: true,
    );
    if (res.exitCode != 0) {
      throw GitException('git remote get-url failed', res.stderr.toString());
    }
    // Strip any pre-existing credentials before reading it back.
    final raw = (res.stdout as String).trim();
    final u = Uri.parse(raw);
    return u.replace(userInfo: '').toString();
  }

  @override
  Future<bool> hasChanges({required String repoPath}) async {
    final res = await Process.run(
      'git',
      ['status', '--porcelain'],
      workingDirectory: repoPath,
      runInShell: true,
    );
    if (res.exitCode != 0) {
      throw GitException('git status failed', res.stderr.toString());
    }
    return (res.stdout as String).trim().isNotEmpty;
  }

  @override
  Future<void> commitAll({
    required String repoPath,
    required String message,
    required String authorName,
    required String authorEmail,
  }) async {
    await _run(['add', '-A'], cwd: repoPath);
    await _run([
      '-c', 'user.name=$authorName',
      '-c', 'user.email=$authorEmail',
      'commit', '-m', message,
    ], cwd: repoPath);
  }

  @override
  Future<void> push({
    required String repoPath,
    required String url,
    String? token,
  }) async {
    // Set the authenticated URL transiently via -c http.extraheader is
    // brittle; instead temporarily set the remote URL with credentials.
    await _run(['remote', 'set-url', 'origin', _authedUrl(url, token)],
        cwd: repoPath);
    try {
      await _run(['push', 'origin', 'HEAD'], cwd: repoPath);
    } finally {
      await _run(['remote', 'set-url', 'origin', url], cwd: repoPath);
    }
  }
}
