# MSVC 重构检查清单 / MSVC Refactor Checklist

中文：在修改面向 MSVC 的 C++ 代码前后，都用这份清单做核对。  
English: Use this checklist before and after editing MSVC-targeted C++ code.

## 1. 构建面 / Build Surface

- 这次改动是否影响 `.sln`、`.vcxproj`、`.vcxproj.filters`、CMake、props 或生成头文件？ / Does the change affect `.sln`, `.vcxproj`, `.vcxproj.filters`, CMake, props, or generated headers?
- 如果新增或移动了文件，它是否已经加入 project file 和 filter file？ / If a file is added or moved, is it included in the project file and filter file?
- 目标是否依赖特定语言标准、告警级别或 `/permissive-` 行为？ / Does the target rely on a specific language standard, warning set, or `/permissive-` behavior?
- 这次改动是否引入当前工具集版本不可用的依赖？ / Will this change introduce dependencies unavailable on the current toolset version?

## 2. 头文件与 PCH 规则 / Header and PCH Rules

- 当前翻译单元是否要求 `pch.h` 或 `stdafx.h` 作为第一个 include？ / Does the translation unit require `pch.h` or `stdafx.h` as the first include?
- 这次重构是否不小心把重量级 Windows 头文件移进了公共头文件？ / Did the refactor accidentally move Windows-heavy headers into widely included public headers?
- 能否在不破坏 inline 代码、模板或 `sizeof` 使用的前提下，用前向声明替换 include？ / Can a forward declaration replace an include without breaking inline code, templates, or `sizeof` usage?
- include 顺序变化是否会通过宏定义或 Windows 头文件交互改变行为？ / Did include order change behavior through macro definitions or Windows header interactions?

## 3. ABI 与二进制兼容 / ABI and Binary Compatibility

- 被修改的符号是否跨 DLL 或共享边界导出？ / Is the changed symbol exported from a DLL or shared boundary?
- 是否改变了任何公共 struct 或 class 的布局？ / Did any public struct or class layout change?
- 是否改变了对齐、打包、vtable 形态、枚举宽度或成员顺序？ / Did alignment, packing, vtable shape, enum width, or member order change?
- 是否改变了公共 inline 函数或模板签名？ / Did a public inline function or template signature change?
- 命名修饰输入是否因命名空间、调用约定、cv/ref 限定或默认参数而变化？ / Did name mangling inputs change through namespace, calling convention, cv/ref qualifiers, or default args?

## 4. Windows 与 MSVC 细节 / Windows and MSVC Details

- 保留 `__declspec(dllexport/dllimport)` 或项目 API 宏。 / Preserve `__declspec(dllexport/dllimport)` or project API macros.
- 保留 `__cdecl`、`__stdcall`、`WINAPI`、`CALLBACK` 等调用约定。 / Preserve `__cdecl`, `__stdcall`, `WINAPI`, `CALLBACK`, and related calling conventions.
- 当 SAL 标注承载合约含义时，不要丢失它们。 / Preserve SAL annotations when they carry contract information.
- 留意 Windows 头文件带来的 `min`、`max` 宏冲突。 / Watch for `min` and `max` macro collisions from Windows headers.
- 保持 `UNICODE`、`_UNICODE`、`TCHAR` 以及宽窄字符假设一致。 / Respect `UNICODE`, `_UNICODE`, `TCHAR`, and wide-string assumptions.
- 除非用户要求，否则保持 `HRESULT`、`BOOL` 和 Win32 错误路径行为不变。 / Keep `HRESULT`, `BOOL`, and Win32 error-path behavior unchanged unless requested.

## 5. COM、ATL 与 MFC / COM, ATL, and MFC

- 保持 COM 引用计数语义和所有权转移不变。 / Preserve COM reference-counting semantics and ownership transfers.
- 不要改动 `QueryInterface`、`AddRef` 或 `Release` 合约。 / Do not change `QueryInterface`, `AddRef`, or `Release` contracts.
- 除非请求明确针对它们，否则不要破坏 ATL 或 MFC 的宏块结构。 / Keep ATL and MFC macro blocks intact unless the request explicitly targets them.
- 重命名后检查 message map、COM map、interface map 和资源 ID。 / Check message maps, COM maps, interface maps, and resource IDs after renames.

## 6. 所有权与生命周期 / Ownership and Lifetime

- 确认谁拥有裸指针、句柄、缓冲区和返回的字符串。 / Confirm who owns raw pointers, handles, buffers, and returned strings.
- 优先使用项目里已有的 RAII 包装。 / Use the project's existing RAII wrappers when available.
- 避免跨模块边界混用不同分配器或 CRT 所有权。 / Avoid mixing allocators or CRT ownership across module boundaries.
- 对句柄封装保持无效值约定和关闭语义一致。 / Preserve invalid-value conventions and close semantics for handle wrappers.

## 7. 模板、Inline 与 ODR / Templates, Inline Code, and ODR

- 头文件内联或模板代码必须在实例化点可见。 / Header-only code must remain visible where instantiated.
- 把 inline 代码移到 `.cpp` 可能导致调用方失效或链接报错。 / Moving inline code to a `.cpp` may break callers or produce link errors.
- 静态数据成员、`constexpr` 和模板特化需要匹配的定义。 / Static data members, `constexpr`, and template specializations need matching definitions.
- 抽取辅助逻辑后要检查匿名命名空间和内部链接是否仍然正确。 / Check anonymous namespaces and internal linkage after extraction.

## 8. 验证顺序 / Validation Sequence

1. 重新检查声明与定义是否漂移。 / Re-scan declarations and definitions for signature drift.
2. 重新检查 include、PCH 和前向声明是否正确。 / Re-scan includes for PCH and forward-declaration correctness.
3. 构建最小受影响项目或翻译单元。 / Build the smallest affected project or translation unit.
4. 运行最小受影响测试。 / Run the narrowest affected tests.
5. 通过后再扩大构建或测试范围。 / Only then widen the build or test scope.

## 9. 常见重构模式 / Typical Refactor Patterns

### 重命名符号 / Rename a Symbol

- 一起修改声明、定义和直接调用点。 / Rename declaration, definition, and direct call sites together.
- 重新检查导出、COM map、MFC message map、资源和字符串型注册项。 / Recheck exports, COM maps, MFC message maps, resources, and string-based registrations.

### 从头文件移动到源文件 / Move Code from Header to Source

- 在头文件中只保留声明。 / Keep only declarations in the header.
- 确保源文件按项目要求先包含匹配头文件，再包含 PCH。 / Ensure the source includes the matching header first, then PCH if the project requires that order.
- 如果可能，重建所有依赖旧 inline 代码的翻译单元。 / Rebuild all translation units that consumed the old inline code if possible.

### 用 RAII 替换裸所有权 / Replace Raw Ownership with RAII

- 编辑前先识别创建、转移、释放和失败路径。 / Identify construction, transfer, release, and failure paths before editing.
- 保持释放顺序和错误行为不变。 / Preserve release order and error behavior.
- 除非用户要求，否则不要改变对外可见的所有权合约。 / Avoid changing externally visible ownership contracts unless requested.

### 拆分类 / Split a Large Class

- 在保持导出表面不变的前提下分离接口与实现。 / Separate interface from implementation while preserving the exported surface.
- 优先使用内部 helper class 或源文件级 helper，再考虑调整公共继承。 / Prefer internal helper classes or source-local helpers before changing public inheritance.
- 检查 friend 声明、pimpl 边界以及序列化或反射钩子。 / Recheck friend declarations, pimpl boundaries, and serialization or reflection hooks.
