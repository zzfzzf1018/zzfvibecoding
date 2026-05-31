# CloudNote

Cross-platform (Windows + Android) markdown note application with Git-based
cloud storage and GitHub OAuth.

## Highlights

- Markdown + plain-text notes, with image rendering (local + remote).
- Custom font, font size, paragraph spacing, font color, light/dark theme.
- Cloud storage over the **standard git protocol** — your notes are an
  ordinary git repository you fully own.
- GitHub login via OAuth **Device Flow** (works on desktop and mobile, no
  embedded browser or secret needed).
- One click "Create notebook" → creates a **private** GitHub repo and clones it.
- Manual sync button + configurable auto-sync interval.
- Smart paste: text, HTML (converted to markdown) and images (via injectable
  clipboard provider) drop into your note.
- Clean architecture (domain / data / services / presentation), suitable
  for refactoring and adding new backends.

## Repository layout

```
lib/
  core/                  config, theme, service locator
  domain/                entities + repository interfaces
  data/                  filesystem-backed repository impl
  services/              github auth/api, git, sync, markdown, clipboard
  presentation/          providers (state) + screens + widgets
test/                    unit tests
scripts/                 build_all.ps1 (Windows), build_all.sh (POSIX)
docs/                    DESIGN.md, USER_MANUAL.md, DEVELOPER_GUIDE.md
```

## Quick start

### Prerequisites

- Flutter SDK ≥ 3.22 (Dart ≥ 3.3).
- Windows: Visual Studio with the "Desktop development with C++" workload.
- Android: Android Studio with SDK + NDK + an emulator or device.
- A system `git` binary on PATH (used by `ProcessGitService`).
- A GitHub **OAuth App** with Device Flow enabled. Copy its Client ID.

### One-time platform scaffolding

The repo intentionally ships **only** the cross-platform Dart code. Generate
the `android/` and `windows/` host projects locally:

```powershell
flutter create --platforms=windows,android --project-name cloudnote .
```

### Run

```powershell
flutter pub get
flutter run -d windows  --dart-define=GITHUB_CLIENT_ID=YOUR_ID
flutter run -d <android-device-id> --dart-define=GITHUB_CLIENT_ID=YOUR_ID
```

### One-shot build (both platforms)

```powershell
./scripts/build_all.ps1 -ClientId YOUR_ID
```

```bash
GITHUB_CLIENT_ID=YOUR_ID TARGETS=android ./scripts/build_all.sh
```

### Tests

```powershell
flutter test
```

## Further reading

- [docs/DESIGN.md](docs/DESIGN.md) – software design specification
- [docs/USER_MANUAL.md](docs/USER_MANUAL.md) – end-user guide
- [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md) – contributor handbook

## License

MIT (see project owner's discretion; add LICENSE before publishing).
