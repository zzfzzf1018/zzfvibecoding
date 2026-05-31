/// Lightweight service locator. Kept intentionally simple so it can be
/// swapped for `get_it` or DI of choice without affecting call sites.
library;

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../services/git_service.dart';
import '../services/github_api_service.dart';
import '../services/github_auth_service.dart';
import '../services/markdown_service.dart';
import '../services/sync_service.dart';

class ServiceLocator {
  ServiceLocator._();

  static late SharedPreferences prefs;
  static late FlutterSecureStorage secure;
  static late GitHubAuthService auth;
  static late GitHubApiService api;
  static late GitService git;
  static late MarkdownService markdown;
  static late SyncService sync;

  static Future<void> init() async {
    prefs = await SharedPreferences.getInstance();
    secure = const FlutterSecureStorage();
    auth = GitHubAuthService(secure: secure);
    api = GitHubApiService(authTokenProvider: () => auth.token);
    git = ProcessGitService();
    markdown = MarkdownService();
    sync = SyncService(git: git, api: api);
  }
}
