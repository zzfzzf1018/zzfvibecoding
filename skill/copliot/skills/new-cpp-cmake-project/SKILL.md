---
name: new-cpp-cmake-project
description: 'Use when creating a new C++ project, new CMake project, initializing vcpkg, adding GTest, setting up CMakePresets, src/include/tests layout, CI starter, library, CLI app, SDK, or cross-platform Windows/Linux native project.'
argument-hint: 'Project name, type, target platforms, dependencies, and whether to create files'
---
# New C++ CMake Project

## When to Use
- User asks to create a C++ project, CMake project, native library, CLI app, SDK, or service.
- User wants vcpkg, GTest, CMakePresets, CI, package, or docs scaffolding.
- User is starting from an empty folder or wants a clean project structure proposal.

## First Questions
Ask only the missing essentials:
- Project name and type: library, executable, CLI, service, SDK, or mixed.
- Target platforms: Windows, Linux, macOS, or cross-platform.
- Public API shape: C++, C ABI, command line, REST, plugin, or none yet.
- Dependencies, test framework, CI provider, and packaging needs.

If the user says to proceed with defaults, use a small cross-platform C++ library plus sample executable and GTest.

## Default Structure
Use this structure unless the repository already has a stronger convention:

```text
.
├── CMakeLists.txt
├── CMakePresets.json
├── vcpkg.json
├── README.md
├── .gitignore
├── include/<project>/
├── src/
├── apps/
├── tests/
├── docs/
└── cmake/
```

For a tiny executable-only project, `include/` and `apps/` may be omitted.

## CMake Guidelines
- Require a modern but conservative CMake version, usually `3.24` or newer unless constrained.
- Set `CMAKE_CXX_STANDARD` through target properties, not global compiler flags when possible.
- Prefer target-based commands: `target_sources`, `target_include_directories`, `target_link_libraries`, `target_compile_features`.
- Keep public headers under `include/` and private implementation under `src/`.
- Export only needed include directories as `PUBLIC`; keep implementation dependencies `PRIVATE`.
- Add options for tests and examples only when useful, such as `BUILD_TESTING`.

## vcpkg Guidelines
- Use manifest mode with `vcpkg.json`.
- Add `gtest` only when tests are enabled.
- Avoid pinning triplets in CMake files; let presets or the user select triplets.
- In `CMakePresets.json`, reference `$env{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake` when vcpkg is required.

## Preset Guidelines
Include at least:
- `windows-debug` and `windows-release` when targeting Windows.
- `linux-debug` and `linux-release` when targeting Linux.
- `build` presets tied to configure presets.
- `test` presets that run CTest with output on failure.

Keep preset names readable and predictable. Do not create many variants before they are needed.

## Testing Guidelines
- Enable CTest with `include(CTest)` and conditionally add `tests/` when `BUILD_TESTING` is on.
- Use GTest via `find_package(GTest CONFIG REQUIRED)` in manifest-based projects.
- Add one small test that proves the build and link path works.
- Avoid tests that depend on the current working directory unless explicitly set.

## CI Starter
When asked for CI, create a minimal matrix:
- Configure with CMake preset.
- Build with CMake build preset.
- Run `ctest --preset ... --output-on-failure`.
- Cache vcpkg artifacts when the CI provider supports it.

## README Minimum
Document:
- What the project is.
- Prerequisites.
- Configure/build/test commands.
- Directory structure.
- How to add dependencies.

## Done Criteria
- Project can configure, build, and run tests using documented commands.
- Generated files are small, idiomatic, and consistent.
- The user receives exact commands to run next.
