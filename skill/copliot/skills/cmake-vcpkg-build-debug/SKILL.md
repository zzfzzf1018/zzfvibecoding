---
name: cmake-vcpkg-build-debug
description: 'Use when debugging CMake, vcpkg, CMakePresets, configure failures, build failures, toolchain files, VCPKG_ROOT, triplets, dependency discovery, Debug/Release mismatch, Ninja, Visual Studio generator, Windows/Linux native builds, ctest, or package problems.'
argument-hint: 'Build error, preset name, platform, command output, or repository path'
---
# CMake vcpkg Build Debug

## When to Use
- CMake configure/build/test/package fails.
- Dependencies are not found through vcpkg.
- Presets, generators, toolchains, triplets, or compiler selection behave unexpectedly.
- Windows and Linux builds differ.

## Triage Order
1. Identify the exact command that failed: configure, build, test, install, or package.
2. Read `CMakePresets.json`, root `CMakeLists.txt`, `vcpkg.json`, and any toolchain-related docs.
3. Confirm current working directory; many projects require running commands from the source root or `src/`.
4. Check environment variables: `VCPKG_ROOT`, compiler paths, generator tools, and SDK availability.
5. Check preset inheritance and cache variables.
6. Inspect the first real error in the log, not the final cascade.
7. Reproduce with the smallest command that isolates the failing phase.

## CMakePresets Checks
- Verify preset names used by the command exist in `configurePresets`, `buildPresets`, `testPresets`, or `packagePresets`.
- Ensure build presets reference the intended configure preset.
- Check `binaryDir` for stale cache conflicts.
- Confirm generator compatibility: Visual Studio on Windows, Ninja where configured, Makefiles where expected.
- Confirm `architecture` is valid for Visual Studio presets.

## vcpkg Checks
- Confirm `vcpkg.json` exists and dependency names are correct.
- Confirm `CMAKE_TOOLCHAIN_FILE` points to `$env{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake` or an existing absolute path.
- Confirm `VCPKG_TARGET_TRIPLET` matches platform and linkage needs, such as `x64-windows`, `x64-windows-static-md`, or `x64-linux`.
- For missing packages, distinguish between package install failure and `find_package` name mismatch.
- Avoid editing installed package files; fix manifest, triplet, overlay, or CMake usage instead.

## Debug/Release Checks
- On single-config generators, check `CMAKE_BUILD_TYPE`.
- On multi-config generators such as Visual Studio, check build `configuration`.
- Avoid mixing Debug libraries into Release builds unless explicitly required.
- Check runtime library mismatch on Windows, especially static/dynamic CRT choices.

## Dependency Discovery Checks
- Prefer config packages from vcpkg: `find_package(Package CONFIG REQUIRED)`.
- Link imported targets instead of raw library names.
- If headers are found but link fails, inspect target linkage and dependency visibility.
- If link succeeds on one platform only, inspect conditional source files and platform libraries.

## Test Debugging
- Use `ctest --preset <name> --output-on-failure` when presets exist.
- Check test working directory, data paths, environment variables, and generated files.
- Separate build failure from runtime test failure.

## Output Expectations
When reporting back:
- State the failing phase and root cause hypothesis.
- Cite the relevant preset or CMake target.
- Provide the smallest fix.
- Provide the exact command to verify.
