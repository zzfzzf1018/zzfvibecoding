# CloudNote – Software Design Specification

## 1. Goals

| ID  | Requirement                                                            |
| --- | ---------------------------------------------------------------------- |
| F1  | Markdown + TXT notes with embedded image rendering                     |
| F2  | Customisable font family, size, paragraph spacing, font color          |
| F3  | Cloud storage over the standard **git protocol**                       |
| F4  | GitHub login + ability to create a private notebook repo               |
| F5  | Manual sync button + configurable auto-sync                            |
| F6  | Smart paste from browser (text, HTML, images) → markdown               |
| NF1 | Refactor-friendly architecture                                         |
| NF2 | One-shot dual-platform build script                                    |
| NF3 | Unit tests                                                             |
| NF4 | Security & bug review                                                  |
| NF5 | Complete docs (design, user manual, developer guide, README)           |
| NF6 | `.gitignore` and other hygiene files                                   |

## 2. Technology choices

- **Flutter / Dart** for a single codebase targeting Windows desktop and
  Android with native performance and a mature UI toolkit.
- **`flutter_markdown` + `markdown`** for rendering and parsing.
- **`google_fonts`** for runtime font selection.
- **System `git` binary** invoked via `Process.run` for the Windows desktop
  build. This keeps the cloud format 100% standard git: any backend that
  speaks git (GitHub, GitLab, self-hosted Gitea, …) works without code
  changes.
- **GitHub OAuth Device Flow** for authentication. Device Flow does not
  require a redirect URI or a client secret, which means the same binary
  works identically on Windows and Android without per-platform plumbing.
- **`flutter_secure_storage`** to persist the access token in the OS
  keystore (DPAPI / Android Keystore).
- **`provider`** for state management — lightweight and easy to refactor.

## 3. Architecture

Clean / layered:

```
┌─────────────────────────────────────────────┐
│              Presentation                   │  Screens, Widgets,
│   (Screens, Widgets, ChangeNotifiers)       │  Providers (view-models)
├─────────────────────────────────────────────┤
│                  Domain                     │  Entities + Repository
│        (Entities, Repository APIs)          │  interfaces (pure Dart)
├─────────────────────────────────────────────┤
│                   Data                      │  FsNoteRepository
│        (Repository implementations)         │  (filesystem on top of clone)
├─────────────────────────────────────────────┤
│                 Services                    │  GitHubAuthService, GitHubApi,
│   (GitHub Auth, GitHub API, Git, Sync,      │  GitService, SyncService,
│    Markdown, Clipboard)                     │  MarkdownService, Clipboard
└─────────────────────────────────────────────┘
```

Dependency direction is downward only. Presentation depends on Domain and
Services; Domain depends on nothing. This makes it trivial to swap, for
example, `ProcessGitService` for a libgit2-based one, or `FsNoteRepository`
for a SQLite-backed one, without touching screens.

## 4. Storage model

A "notebook" is a private GitHub repo cloned locally. Layout inside the
repo:

```
notes/                    Markdown / TXT files written by the user
assets/                   Pasted images and other binary attachments
.cloudnote_index.json     Lightweight metadata (id → relative path, title,
                          mtime, format). Versioned with the notes so the
                          app can reopen on any device with the same ids.
```

Sync algorithm (`SyncService.syncNow`):

1. `git pull --rebase --autostash`
2. If `git status --porcelain` is non-empty → `git add -A && git commit -m`
3. `git push origin HEAD` with a transient credential URL
   (`x-access-token:<token>@github.com/...`); the URL is reset immediately
   after to avoid leaving the token on disk in `.git/config`.

Auto-sync is a `Timer.periodic` configured from `SettingsProvider`.

## 5. Authentication

OAuth Device Flow:

1. App POSTs `https://github.com/login/device/code` with the client id.
2. UI shows the user code and opens `verification_uri` in the system
   browser via `url_launcher`.
3. App polls `login/oauth/access_token` honouring `interval` / `slow_down`.
4. On success, the access token is written to `FlutterSecureStorage`.

The token only requires the `repo` and `user:email` scopes — repo create,
clone, push and pull all flow through this single credential.

## 6. Markdown + clipboard

- **Render**: `flutter_markdown` with a custom `imageBuilder` that
  resolves relative paths against the repo root so pasted images display
  inline.
- **Paste**: `ClipboardService.readAsMarkdown` reads text from Flutter's
  built-in clipboard, auto-detects HTML, and converts it via
  `MarkdownService.htmlToMarkdown` (a small, dependency-free converter
  covering the subset browsers actually emit).
- **Clipboard extension point**: image bytes are injected through a
  `Future<List<RawClipboardImage>> Function()` callback so any platform
  channel or third-party plugin (`super_clipboard`, `rich_clipboard`, a
  native MethodChannel) can be plugged in without altering the service or
  its tests.

## 7. Settings

`SettingsProvider` (a `ChangeNotifier`) persists to `SharedPreferences`:

- `fontFamily`, `fontScale`, `fontColor`, `paragraphSpacing`, `themeMode`
- `autoSync`, `syncIntervalMinutes`
- `activeRepoJson` (the currently bound notebook)

The theme is rebuilt from these values on every change.

## 8. Mobile git strategy

`ProcessGitService` requires a `git` binary; this is fine for Windows
(Git for Windows / scoop / chocolatey). On Android there is no system
`git`. The `GitService` interface exists precisely so a future
`GitHubContentsApiGitService` can implement `pull/commit/push` against the
GitHub REST Contents API (a single PUT per changed file). This is
documented as a planned backend and is straightforward to add because the
sync algorithm depends only on the interface.

## 9. Security

- Tokens stored in OS keystore (DPAPI on Windows, Android Keystore).
- Token never written into committed files. Credentials are injected at
  push time and then scrubbed from `.git/config`.
- Repos are created **private** by default.
- HTML paste sanitisation strips `<script>` and `<style>` blocks before
  conversion to markdown.
- Network calls use HTTPS only; the GitHub API client sets
  `X-GitHub-Api-Version` and `Accept: application/vnd.github+json`.
- No use of `dart:io` `Process.run` with shell-interpolated user input —
  arguments are always passed as a `List<String>`.

## 10. Testing

Unit tests cover the layers that are not coupled to Flutter widgets:

- `note_test.dart` – entity JSON roundtrip & copyWith.
- `fs_note_repository_test.dart` – filesystem save/list/delete/attachment.
- `markdown_service_test.dart` – HTML→Markdown and Markdown→HTML.
- `sync_service_test.dart` – pull/commit/push orchestration with a mocked
  `GitService` (mocktail).

Widget tests for screens are intentionally out of scope of the first cut
but the providers are written so adding `pumpWidget` tests later is
trivial.

## 11. Build & release

`scripts/build_all.ps1` and `scripts/build_all.sh` perform:
`pub get` → `flutter create` (if platform folders missing) → `flutter
test` → `flutter build windows --release` and/or `flutter build apk
--release`, injecting `GITHUB_CLIENT_ID` via `--dart-define`.

## 12. Future work

- libgit2dart backend for offline-capable git on Android.
- Per-note conflict resolution UI.
- Full-text search over the local notes directory.
- WebDAV / S3 backends behind the same `NoteRepository` interface.
