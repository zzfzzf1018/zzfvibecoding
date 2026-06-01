---
name: code-review-checklist
description: 'Use when performing cross-project code review, PR review, diff review, bug-risk assessment, regression review, C++ review, API review, test review, security-minded review, maintainability review, or checking correctness, resource ownership, exception safety, concurrency, validation, tests, and cross-platform risks.'
argument-hint: 'Review target such as current diff, branch, PR, selected files, or module'
---
# Code Review Checklist

## When to Use
- User asks for a review, PR review, diff review, or risk assessment.
- User wants bugs and regressions found before merging.
- The code touches APIs, resource management, concurrency, persistence, builds, tests, or platform-specific behavior.

## Review Stance
Lead with findings. Prioritize concrete bugs and risks over style preferences. Keep summaries brief and secondary.

## Review Procedure
1. Identify the scope: current diff, selected files, branch comparison, or module.
2. Read nearby code and tests needed to understand behavior.
3. Look for correctness, lifecycle, error handling, and test coverage issues.
4. Prefer specific file references and actionable fixes.
5. Avoid commenting on unrelated code unless it blocks the change.

## Findings Format
For each finding, include:
- Severity: Critical, High, Medium, or Low.
- Location.
- Problem.
- Why it matters.
- Suggested fix or direction.

If no issues are found, say so clearly and mention remaining test gaps or residual risk.

## Correctness Checklist
- Inputs validated at the right boundary.
- Edge cases handled: null, empty, zero, overflow, underflow, invalid enum, missing fields.
- State transitions are legal and complete.
- Error paths do not accidentally continue as success.
- Algorithms preserve ordering, precision, units, and invariants.

## Resource and Lifetime Checklist
- Ownership is clear for pointers, handles, memory, files, sockets, transactions, and locks.
- Allocations have matching cleanup on success and failure paths.
- No use-after-free, double-free, dangling references, or leaked resources.
- RAII is preferred in C++ implementation code.
- Cross-ABI allocation/free rules are explicit.

## Exception and Error Safety
- Exceptions do not cross C ABI or FFI boundaries.
- Partial failures leave objects in a valid state.
- Error codes are checked and propagated.
- Cleanup occurs when construction or intermediate steps fail.

## Concurrency Checklist
- Shared mutable state is protected or avoided.
- Lock ordering avoids deadlock.
- Async callbacks do not outlive captured objects.
- Caches and singletons are thread-safe when used concurrently.

## API and Compatibility Checklist
- Public API changes are intentional and documented.
- Struct layout, enum values, exported symbols, and serialization formats remain compatible when required.
- DTOs are separated from domain models when useful.
- Error behavior is predictable for callers.

## Test Checklist
- Tests cover new behavior and failure paths.
- Regression tests protect fixed bugs.
- Floating-point tests use meaningful tolerances.
- Test data is deterministic and not silently updated.
- CI/build files include new tests where required.

## Cross-Platform Checklist
- Path separators, filesystem case sensitivity, encodings, line endings, and locale are considered.
- Compiler differences are handled without undefined behavior.
- Windows/Linux/macOS dependencies and link libraries are correctly gated.

## Output Structure
Use this order:
1. Findings.
2. Open Questions or Assumptions.
3. Brief Summary.
4. Tests run or not run.
