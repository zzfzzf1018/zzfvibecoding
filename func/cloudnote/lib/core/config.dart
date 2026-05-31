/// App-wide configuration constants. The GitHub OAuth client id should be
/// supplied at build time via --dart-define=GITHUB_CLIENT_ID=xxx.
library;

class AppConfig {
  static const String appName = 'CloudNote';

  /// Public OAuth client id for the GitHub Device Flow. Device Flow does
  /// not use a client secret, so the id is safe to ship in the binary.
  static const String githubClientId = String.fromEnvironment(
    'GITHUB_CLIENT_ID',
    defaultValue: 'Iv1.0000000000000000',
  );

  /// Scopes required for private repo create + push.
  static const List<String> githubScopes = ['repo', 'user:email'];

  static const String defaultBranch = 'main';
  static const String notesFolder = 'notes';
  static const String assetsFolder = 'assets';
}
