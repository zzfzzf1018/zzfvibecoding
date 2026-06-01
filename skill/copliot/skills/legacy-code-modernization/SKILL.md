---
name: legacy-code-modernization
description: 'Use when modernizing legacy C or C++ code, wrapping old APIs, reducing risk, adding characterization tests, improving safety incrementally, replacing globals, isolating old modules, refactoring without behavior changes, or planning gradual migration.'
argument-hint: 'Legacy module, pain point, desired outcome, and risk tolerance'
---
# Legacy Code Modernization

## When to Use
- User is working with old C/C++ code and wants safer evolution.
- There are few tests, unclear ownership, global state, raw pointers, macros, or platform assumptions.
- The goal is incremental modernization rather than a rewrite.

## Working Principles
- Preserve behavior first; improve structure second.
- Add characterization tests before risky refactors.
- Make small reversible changes.
- Keep public contracts stable unless the user explicitly wants a breaking change.
- Avoid large formatting churn that hides behavior changes.

## Assessment Steps
1. Identify entry points and callers.
2. Find existing tests, sample data, logs, or expected outputs.
3. Map ownership and lifetime of important objects.
4. Identify global state, hidden dependencies, macros, and platform-specific code.
5. Choose the smallest modernization step that reduces real risk.

## Characterization Tests
- Capture current behavior before changing implementation.
- Cover typical input, edge input, and known strange behavior.
- Preserve quirks intentionally when callers may depend on them.
- Name tests around observed behavior, not ideal behavior.

## Isolation Techniques
- Wrap legacy functions behind a narrow adapter.
- Move IO, parsing, or platform calls behind interfaces where testing requires it.
- Introduce seams only where they remove real risk.
- Keep new code calling old code through one controlled boundary when possible.

## Safe Refactoring Moves
- Rename local variables for clarity when low risk.
- Extract pure helper functions.
- Replace manual allocation with RAII inside implementation boundaries.
- Convert magic constants to named constants.
- Reduce duplicated branches after tests exist.
- Replace macros with functions or constants when semantics are clear.

## Risky Moves Requiring Extra Care
- Changing public headers or binary layout.
- Replacing algorithms without golden/regression coverage.
- Changing floating-point order of operations.
- Changing threading or global state behavior.
- Updating test data without explaining why.

## Migration Plan Template
For larger work, propose phases:
1. Characterize behavior and add smoke tests.
2. Isolate external boundary.
3. Improve ownership and error handling inside boundary.
4. Replace or refactor implementation in small slices.
5. Remove obsolete paths after coverage and callers are updated.

## Done Criteria
- Behavior-preserving changes have tests or a clear verification method.
- The modernization step is narrow and reviewable.
- The user understands remaining risk and next possible step.
