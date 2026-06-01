---
name: cpp-library-api-design
description: 'Use when designing or reviewing a C++ library public API, module boundaries, header exposure, namespace structure, ownership model, error handling, PIMPL, ABI stability, binary compatibility, unique_ptr/shared_ptr choices, exceptions, or SDK-style interfaces.'
argument-hint: 'Library purpose, target users, ABI needs, and current API files'
---
# C++ Library API Design

## When to Use
- User asks to design, review, or refactor a C++ library public API.
- The change affects headers under `include/`, exported symbols, SDK users, or binary compatibility.
- There is uncertainty about ownership, exceptions, callbacks, threading, or module boundaries.

## Design Goals
- Make the smallest API that expresses the use case clearly.
- Keep implementation details private.
- Preserve source and binary compatibility when the library is already consumed externally.
- Make ownership, lifetime, errors, and thread-safety visible in the interface.

## Header Exposure
- Public headers should include only what users need.
- Prefer forward declarations when they reduce coupling without making usage awkward.
- Avoid exposing private third-party types unless they are part of the public contract.
- Avoid exposing templates, inline-heavy implementation, macros, and global state unless intentional.
- Keep namespace names stable and project-specific.

## Ownership Choices
- Use values for small, copyable data with clear semantics.
- Use `std::unique_ptr` for exclusive ownership and factory return values.
- Use `std::shared_ptr` only when shared lifetime is truly part of the model.
- Use references or non-owning pointers for borrowed objects, and document lifetime requirements.
- Avoid raw owning pointers in public C++ APIs.

## Error Handling
Choose one dominant style per API surface:
- Exceptions: good for idiomatic C++ library consumers when failures are exceptional and ABI boundary is C++ only.
- Error codes or result objects: good for stable SDKs, recoverable errors, C ABI, or cross-language boundaries.
- `std::optional` only for expected absence without diagnostic details.

Never let exceptions cross a C ABI boundary.

## ABI Stability
For binary-distributed libraries:
- Consider PIMPL for classes with evolving private state.
- Avoid changing virtual function order, data member layout, enum widths, or exported struct layout.
- Avoid STL containers in ABI-stable boundaries across compiler/runtime versions.
- Version exported APIs and document compatibility expectations.

## Module Boundaries
- Domain/model types should not depend on app, UI, transport, or persistence layers.
- Parsing, IO, network, and algorithm concerns should be separated when they evolve independently.
- Keep factories close to construction complexity, not scattered across callers.

## Review Checklist
- Can a new user understand how to create, use, and destroy the main object?
- Are invalid inputs and failure modes documented or encoded?
- Is the minimal include set enough for public usage?
- Can the implementation change without breaking callers?
- Are tests covering API behavior rather than private implementation details?

## Output Expectations
When designing an API, provide:
- Proposed public types and functions.
- Ownership and error handling policy.
- Compatibility notes.
- Example usage.
- Tests that should accompany the API.
