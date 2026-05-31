#!/usr/bin/env bash
# Build CloudNote for Linux/macOS hosts: produces Android APK (and optionally
# Linux desktop if you add 'linux' to TARGETS). Windows builds must run from
# the Windows PowerShell script.
set -euo pipefail

CLIENT_ID="${GITHUB_CLIENT_ID:-}"
SKIP_TESTS="${SKIP_TESTS:-0}"
TARGETS="${TARGETS:-android}"

cd "$(dirname "$0")/.."

command -v flutter >/dev/null || { echo 'flutter not in PATH'; exit 1; }

if [[ -z "$CLIENT_ID" ]]; then
  echo 'WARNING: GITHUB_CLIENT_ID not set; placeholder will be baked in.'
fi

echo '==> flutter pub get'
flutter pub get

if [[ ! -d android || ! -d windows ]]; then
  echo '==> Generating platform folders (one-time)'
  flutter create --platforms=windows,android --project-name cloudnote .
fi

if [[ "$SKIP_TESTS" != "1" ]]; then
  echo '==> flutter test'
  flutter test
fi

DEFINES=()
[[ -n "$CLIENT_ID" ]] && DEFINES+=(--dart-define=GITHUB_CLIENT_ID="$CLIENT_ID")

IFS=',' read -ra LIST <<< "$TARGETS"
for t in "${LIST[@]}"; do
  case "$t" in
    android) echo '==> Build: Android (APK)'; flutter build apk --release "${DEFINES[@]}" ;;
    linux)   echo '==> Build: Linux';         flutter build linux --release "${DEFINES[@]}" ;;
    windows) echo 'Windows builds must run from build_all.ps1 on a Windows host.' ;;
    *) echo "Unknown target: $t"; exit 1 ;;
  esac
done

echo 'Done.'
