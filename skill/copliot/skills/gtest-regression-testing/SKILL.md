---
name: gtest-regression-testing
description: 'Use when adding C++ GTest unit tests, regression tests, characterization tests, parameterized tests, fixtures, golden files, floating-point comparisons, temporary directories, test data management, CTest integration, or bug-fix coverage.'
argument-hint: 'Code path, bug, behavior, or module that needs tests'
---
# GTest Regression Testing

## When to Use
- User asks to add tests for C++ code using GoogleTest.
- A bug fix needs regression coverage.
- Existing behavior must be characterized before refactoring.
- Test data, golden files, floating-point comparisons, or CTest integration are involved.

## Start With Existing Patterns
1. Find nearby tests for the same module.
2. Reuse naming style, fixtures, helper utilities, and test data conventions.
3. Check how tests are added in CMake and CTest.
4. Keep new tests focused on the risk being covered.

## Test Types
- Unit test: validates one small function or class with controlled inputs.
- Regression test: protects a known bug or expected output.
- Characterization test: captures current behavior before changing legacy code.
- Integration test: validates several modules together and should be fewer and slower.

## Fixture Guidelines
- Use fixtures when setup is shared and meaningful.
- Keep fixture state simple and reset between tests.
- Avoid hidden coupling through mutable shared objects.
- Prefer helper functions for data construction when no fixture lifecycle is needed.

## Parameterized Tests
Use parameterized tests when:
- Same behavior should hold for multiple input/output pairs.
- Edge cases differ only by data.
- The test body remains readable.

Avoid parameterization when each case needs different explanation or setup.

## Golden File Guidelines
- Use golden files for large or domain-specific outputs where inline expectations are unreadable.
- Keep files small, deterministic, and human-reviewable when possible.
- Never update golden files silently; explain why expected output changed.
- Normalize paths, timestamps, ordering, and floating precision before comparison.

## Floating-Point Comparisons
- Use `EXPECT_NEAR` or domain-specific tolerances.
- Choose tolerance based on numerical meaning, not arbitrary looseness.
- Avoid exact equality except for values that are mathematically or representation guaranteed.
- For arrays, assert size first, then compare element-wise with useful failure context if available.

## Temporary Files and Directories
- Do not write test output into source directories.
- Use framework or platform temp directories.
- Clean up when the test creates persistent files.
- Keep tests independent of current working directory unless explicitly configured.

## Test Data
- Prefer minimal synthetic data for unit tests.
- Use real fixture data only when the parser or integration behavior requires it.
- Avoid adding large binary files unless justified.
- Document any non-obvious fixture origin or expected value derivation.

## CMake/CTest
- Add test source to the appropriate test target.
- Link `GTest::gtest`, `GTest::gtest_main`, or existing project test helpers as locally conventional.
- Register tests with `gtest_discover_tests` or the repository's existing pattern.
- Provide the command to run the new test.

## Done Criteria
- Test fails before the fix when possible and passes after.
- Test is deterministic and isolated.
- The test name states the behavior under test.
- The user gets the exact command used or recommended.
