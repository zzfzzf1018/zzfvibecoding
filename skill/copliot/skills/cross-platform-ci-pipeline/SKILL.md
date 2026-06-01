---
name: cross-platform-ci-pipeline
description: 'Use when adding or debugging GitHub Actions, Azure Pipelines, Windows/Linux CI matrix, CMake configure build test package workflows, vcpkg cache, CTest results, coverage upload, artifacts, release packages, or native C++ continuous integration.'
argument-hint: 'CI provider, target platforms, presets, dependencies, and desired artifacts'
---
# Cross-Platform CI Pipeline

## When to Use
- User wants GitHub Actions or Azure Pipelines for a C++/CMake project.
- CI must build/test on Windows and Linux.
- vcpkg dependency caching, CTest results, coverage, packages, or artifacts are needed.
- Existing CI fails and needs diagnosis.

## First Decisions
Clarify or infer:
- CI provider: GitHub Actions or Azure Pipelines.
- Platforms: Windows, Linux, macOS.
- Build presets and test presets.
- Required artifacts: binaries, packages, coverage, test logs, docs.
- Whether vcpkg manifest mode is used.

## Pipeline Stages
Default stages:
1. Checkout.
2. Install or locate build tools.
3. Restore/cache dependencies.
4. Configure CMake.
5. Build.
6. Test with CTest.
7. Package if required.
8. Publish test results, coverage, and artifacts.

## Matrix Strategy
- Use a platform/configuration matrix only as broad as needed.
- Prefer existing CMakePresets over duplicating flags in YAML.
- Keep platform-specific setup isolated in the matrix or conditional steps.
- Use clear preset names such as `windows-release`, `linux-debug`, or project-specific equivalents.

## vcpkg Cache
- Cache vcpkg downloads, binary cache, or installed artifacts according to provider capabilities.
- Include `vcpkg.json`, triplet files, compiler identity, and OS in cache keys.
- Do not cache build directories blindly unless the project has proven it is safe.
- Prefer vcpkg binary caching for reliable dependency reuse.

## CMake Commands
Use the preset flow when available:

```sh
cmake --preset <configure-preset>
cmake --build --preset <build-preset>
ctest --preset <test-preset> --output-on-failure
cpack --preset <package-preset>
```

If presets do not exist, recommend adding them rather than hardcoding many flags in CI.

## Test Results and Coverage
- Configure CTest to emit JUnit XML when the provider can publish it.
- Upload raw logs on failure.
- Use platform-appropriate coverage: gcov/lcov on Linux, OpenCppCoverage or VS tools on Windows when needed.
- Convert coverage formats only when the CI system requires it.

## Azure Pipelines Notes
- Use `PublishTestResults@2` for test reports.
- Use `PublishCodeCoverageResults` when coverage format is supported.
- Use pipeline cache tasks for vcpkg if stable.
- Keep YAML templates small and parameterized only when duplication becomes real.

## GitHub Actions Notes
- Use `actions/checkout`.
- Use `actions/cache` or vcpkg binary cache setup.
- Use `actions/upload-artifact` for packages and logs.
- Use matrix jobs for OS/configuration combinations.

## Debugging CI Failures
- Identify whether failure is environment setup, dependency restore, configure, compile, test, package, or publish.
- Compare CI command with local documented commands.
- Check path separators, shell differences, and case sensitivity.
- Preserve logs and artifacts needed for reproduction.

## Done Criteria
- CI uses documented local commands as much as possible.
- Artifacts and reports are published only when useful.
- The pipeline is small enough for maintainers to understand.
