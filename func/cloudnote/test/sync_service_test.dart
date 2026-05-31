import 'package:cloudnote/domain/entities/note_repo.dart';
import 'package:cloudnote/services/git_service.dart';
import 'package:cloudnote/services/github_api_service.dart';
import 'package:cloudnote/services/sync_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockGit extends Mock implements GitService {}

void main() {
  late _MockGit git;
  late SyncService sync;

  setUp(() {
    git = _MockGit();
    sync = SyncService(
      git: git,
      api: GitHubApiService(authTokenProvider: () => 'fake'),
    );
  });

  final repo = NoteRepo(
    name: 'r',
    owner: 'o',
    cloneUrl: 'https://github.com/o/r.git',
    localPath: '/tmp/r',
  );

  test('syncNow pulls, commits when dirty, then pushes', () async {
    when(() => git.pull(repoPath: any(named: 'repoPath'), token: any(named: 'token')))
        .thenAnswer((_) async {});
    when(() => git.hasChanges(repoPath: any(named: 'repoPath')))
        .thenAnswer((_) async => true);
    when(() => git.commitAll(
          repoPath: any(named: 'repoPath'),
          message: any(named: 'message'),
          authorName: any(named: 'authorName'),
          authorEmail: any(named: 'authorEmail'),
        )).thenAnswer((_) async {});
    when(() => git.push(
          repoPath: any(named: 'repoPath'),
          url: any(named: 'url'),
          token: any(named: 'token'),
        )).thenAnswer((_) async {});

    final r = await sync.syncNow(
      repo: repo,
      token: 'tok',
      authorName: 'me',
      authorEmail: 'me@example.com',
    );
    expect(r.status, SyncStatus.success);
    verify(() => git.commitAll(
          repoPath: '/tmp/r',
          message: any(named: 'message'),
          authorName: 'me',
          authorEmail: 'me@example.com',
        )).called(1);
  });

  test('syncNow skips commit when clean', () async {
    when(() => git.pull(repoPath: any(named: 'repoPath'), token: any(named: 'token')))
        .thenAnswer((_) async {});
    when(() => git.hasChanges(repoPath: any(named: 'repoPath')))
        .thenAnswer((_) async => false);
    when(() => git.push(
          repoPath: any(named: 'repoPath'),
          url: any(named: 'url'),
          token: any(named: 'token'),
        )).thenAnswer((_) async {});

    final r = await sync.syncNow(
      repo: repo,
      token: 't',
      authorName: 'a',
      authorEmail: 'a@b',
    );
    expect(r.status, SyncStatus.success);
    verifyNever(() => git.commitAll(
          repoPath: any(named: 'repoPath'),
          message: any(named: 'message'),
          authorName: any(named: 'authorName'),
          authorEmail: any(named: 'authorEmail'),
        ));
  });

  test('syncNow returns failure when git throws', () async {
    when(() => git.pull(
          repoPath: any(named: 'repoPath'),
          token: any(named: 'token'),
        )).thenThrow(GitException('boom'));
    final r = await sync.syncNow(
      repo: repo,
      token: 't',
      authorName: 'a',
      authorEmail: 'a@b',
    );
    expect(r.status, SyncStatus.failure);
    expect(r.message, contains('boom'));
  });
}
