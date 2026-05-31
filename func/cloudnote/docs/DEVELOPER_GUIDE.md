# CloudNote – Developer Guide

## 1. Toolchain

- Flutter ≥ 3.22, Dart ≥ 3.3
- Visual Studio 2022 (Desktop C++) for the Windows build
- Android Studio with platform-tools for the Android build
- `git` on PATH (used at runtime by the desktop build)

## 2. Bootstrapping a fresh clone

```powershell
flutter pub get
flutter create --platforms=windows,android --project-name cloudnote .
flutter run -d windows --dart-define=GITHUB_CLIENT_ID=<your-id>
```

`flutter create` only adds platform-specific scaffolding the first time;
it never overwrites the `lib/` tree.

## 3. Project layout

| Folder                       | Responsibility                                    |
| ---------------------------- | ------------------------------------------------- |
| `lib/core/`                  | Cross-cutting: theme, config, service locator     |
| `lib/domain/entities/`       | Pure data classes (`Note`, `NoteRepo`)            |
| `lib/domain/repositories/`   | Abstract repository contracts                     |
| `lib/data/repositories/`     | Concrete repository implementations               |
| `lib/services/`              | I/O facades: GitHub auth/API, git, sync, markdown |
| `lib/presentation/providers/`| `ChangeNotifier` view-models                      |
| `lib/presentation/screens/`  | Top-level screens                                 |
| `lib/presentation/widgets/`  | Reusable widgets                                  |
| `test/`                      | Unit tests                                        |

## 4. Adding a feature – worked example

> **Goal**: store notes on GitLab instead of GitHub.

1. Add `lib/services/gitlab_api_service.dart` mirroring `GitHubApiService`.
2. Reuse `ProcessGitService` unchanged — the git protocol is the same.
3. Add a `RepoBackend` enum and let `SettingsProvider` persist the choice.
4. Swap the service in `ServiceLocator.init()`.

No screens, no entities, no tests outside the new service need to change.

## 5. Adding image clipboard support

`ClipboardService` accepts an `imageProvider` callback. Wire it like so:

```dart
final svc = ClipboardService(
  markdownService: ServiceLocator.markdown,
  noteRepo: notesProvider.repository!,
  imageProvider: () async {
    // platform-specific impl, e.g. super_clipboard or a MethodChannel
    return [RawClipboardImage(bytes: bytes, suggestedName: 'paste.png')];
  },
);
```

Then call `svc.readAsMarkdown()` from the editor's paste handler.

## 6. Running tests

```powershell
flutter test
flutter test --coverage      # produces coverage/lcov.info
```

Mocks use [`mocktail`](https://pub.dev/packages/mocktail) — see
`test/sync_service_test.dart` for a pattern.

## 7. Build pipeline

`scripts/build_all.ps1` is the single entry point on Windows; the POSIX
equivalent is `scripts/build_all.sh`. Both:

1. Verify `flutter` on PATH.
2. `flutter pub get`.
3. Run `flutter create` if `android/` or `windows/` is missing.
4. Run `flutter test` unless `-SkipTests` / `SKIP_TESTS=1`.
5. Build the requested targets with `--dart-define=GITHUB_CLIENT_ID=…`.

Artifacts:

- Windows: `build/windows/x64/runner/Release/cloudnote.exe`
- Android: `build/app/outputs/flutter-apk/app-release.apk`

## 8. Coding conventions

- `package:flutter_lints` + extra rules from `analysis_options.yaml`.
- Single quotes, trailing commas, no `print`.
- Public APIs documented; private helpers documented only when surprising.
- Never put secrets in source. The OAuth client id ships as a
  `--dart-define`, not as a file.

## 9. Security checklist for PRs

- [ ] No tokens or PII logged.
- [ ] No subprocess args interpolated from user input.
- [ ] HTML inputs (clipboard, network) pass through
      `MarkdownService.htmlToMarkdown` (strips scripts/styles).
- [ ] New external HTTP calls use HTTPS and set timeout/error handling.
- [ ] New native plugins reviewed for permissions.

## 10. Release checklist

1. Bump `version:` in `pubspec.yaml`.
2. Update `CHANGELOG.md` (create on first release).
3. Tag the commit `vX.Y.Z`.
4. Run `./scripts/build_all.ps1 -ClientId <prod>`.
5. Sign the APK with your release keystore.
6. Upload artifacts to the GitHub Release page.
