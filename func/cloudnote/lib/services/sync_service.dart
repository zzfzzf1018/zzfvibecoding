import 'dart:async';

import '../domain/entities/note_repo.dart';
import 'git_service.dart';
import 'github_api_service.dart';

enum SyncStatus { idle, syncing, success, failure }

class SyncResult {
  SyncResult(this.status, [this.message]);
  final SyncStatus status;
  final String? message;
}

/// Coordinates pull/commit/push against the active [NoteRepo].
class SyncService {
  SyncService({required this.git, required this.api});

  final GitService git;
  final GitHubApiService api;

  Timer? _autoTimer;
  final _statusController = StreamController<SyncResult>.broadcast();
  Stream<SyncResult> get status => _statusController.stream;

  void startAuto({
    required NoteRepo repo,
    required Duration interval,
    required String Function() tokenProvider,
    required String authorName,
    required String authorEmail,
  }) {
    stopAuto();
    _autoTimer = Timer.periodic(interval, (_) {
      syncNow(
        repo: repo,
        token: tokenProvider(),
        authorName: authorName,
        authorEmail: authorEmail,
      );
    });
  }

  void stopAuto() {
    _autoTimer?.cancel();
    _autoTimer = null;
  }

  Future<SyncResult> syncNow({
    required NoteRepo repo,
    required String token,
    required String authorName,
    required String authorEmail,
    String commitMessage = 'cloudnote: sync',
  }) async {
    _statusController.add(SyncResult(SyncStatus.syncing));
    try {
      await git.pull(repoPath: repo.localPath, token: token);
      if (await git.hasChanges(repoPath: repo.localPath)) {
        await git.commitAll(
          repoPath: repo.localPath,
          message: commitMessage,
          authorName: authorName,
          authorEmail: authorEmail,
        );
      }
      await git.push(
        repoPath: repo.localPath,
        url: repo.cloneUrl,
        token: token,
      );
      final r = SyncResult(SyncStatus.success);
      _statusController.add(r);
      return r;
    } catch (e) {
      final r = SyncResult(SyncStatus.failure, e.toString());
      _statusController.add(r);
      return r;
    }
  }

  void dispose() {
    stopAuto();
    _statusController.close();
  }
}
