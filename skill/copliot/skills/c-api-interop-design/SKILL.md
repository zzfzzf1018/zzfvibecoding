---
name: c-api-interop-design
description: 'Use when designing or reviewing C-compatible APIs for C++ libraries used by C#, Python, Rust, C, Java, or other languages. Covers extern C, export macros, DTO layout, memory ownership, free functions, error codes, P/Invoke, ctypes, cffi, FFI, and ABI compatibility.'
argument-hint: 'Interop language, API files, DTOs, ownership requirements, and target platforms'
---
# C API Interop Design

## When to Use
- C++ functionality must be called from C#, Python, Rust, C, Java, or another runtime.
- User asks about `extern "C"`, DLL exports, shared libraries, P/Invoke, ctypes, cffi, or FFI.
- Public structs, arrays, strings, buffers, callbacks, or error codes cross a binary boundary.

## Core Rules
- Export functions with `extern "C"` when compiled as C++.
- Use platform export macros for Windows and ELF visibility for Linux when needed.
- Do not expose C++ STL types, references, templates, exceptions, overloaded functions, or class layout.
- Use fixed-width integer types when size matters.
- Make string encoding explicit, usually UTF-8 unless a platform API requires otherwise.
- Pair every allocation-returning function with a documented free function.

## DTO Design
- DTOs should contain only C-compatible fields: primitive values, fixed arrays, pointers, sizes, and nested C structs.
- Every pointer field that represents an array needs a size field.
- Every optional pointer must state whether null is allowed.
- Avoid `bool` across ABI unless size and marshalling are controlled; prefer `int` or fixed-width values.
- Avoid public struct changes after release; append carefully only when versioning supports it.

## Ownership Policy
For each pointer crossing the boundary, state:
- Who allocates it.
- Who owns it after the call.
- Who frees it.
- Which function frees it.
- Whether null is valid.
- Whether the callee copies input data or borrows it during the call only.

Prefer simple policies:
- Caller allocates inputs and retains ownership.
- Callee allocates outputs and caller releases them through `Free...`.
- Free functions must tolerate null.

## Error Handling
- Return stable integer error codes or an enum with fixed underlying representation.
- Do not throw exceptions across the ABI.
- Catch internal exceptions at the boundary and convert them to error codes.
- Provide a way to retrieve diagnostics when simple codes are not enough.

## C# P/Invoke Notes
- Match calling convention and library name.
- Use `IntPtr` for complex native structures when ownership is non-trivial.
- Use explicit `StructLayout` and fixed marshalling rules.
- Avoid automatic string marshalling for buffers that require explicit length.

## Python ctypes/cffi Notes
- Define `argtypes` and `restype` explicitly.
- Use bytes plus length for binary data.
- Wrap native allocations in Python objects that call the free function.

## Rust FFI Notes
- Use `repr(C)` for DTOs.
- Treat native pointers as unsafe and wrap them in RAII types.
- Do not let Rust panics cross the C ABI when exporting back.

## Test Checklist
- Success path.
- Null inputs.
- Invalid sizes.
- Empty arrays and strings.
- Allocation failure if practical.
- Freeing null and freeing normal outputs.
- Cross-language smoke test when a binding exists.

## Output Expectations
Provide a checklist of risks, a proposed API shape, ownership table, error-code strategy, and at least one caller-side usage example.
