---
name: msvc-cpp-refactor
description: 'Safely refactor MSVC / Visual Studio C++ code. Use for Win32, ATL, MFC, COM, DLL, vcxproj, sln, precompiled headers, SAL, __declspec, calling conventions, TCHAR, UNICODE, HRESULT, and Windows-specific C++ refactors. 也适用于中文请求，例如：重构 MSVC C++ 代码、重构 Visual Studio C++ 工程、保 ABI 重构。'
argument-hint: 'Describe the target files, intended refactor, and any ABI or build constraints. 或说明目标文件、重构意图、以及 ABI/构建约束。'
user-invocable: true
---

# MSVC C++ 重构 / MSVC C++ Refactor

中文：这个 skill 用于 MSVC 或 Visual Studio C++ 工程中的非平凡重构任务。  
English: Use this skill for non-trivial refactoring in C++ projects built with MSVC or Visual Studio.

中文：它特别适合包含 Windows API、Visual Studio 工程文件、预编译头、COM 风格所有权、旧式宏、SAL 标注、导出符号或 ABI 约束的代码库。  
English: It is optimized for codebases that contain Windows APIs, Visual Studio project files, precompiled headers, COM-style ownership, legacy macros, SAL annotations, exported symbols, or ABI-sensitive interfaces.

## 适用场景 / When to Use

- 重命名或移动类、函数、文件、命名空间或模块。 / Rename or move classes, functions, files, namespaces, or modules.
- 拆分过大的头文件或源文件，同时保留 include 顺序和预编译头规则。 / Split oversized headers or source files while preserving include order and precompiled-header rules.
- 将裸资源所有权替换为 RAII，例如 `std::unique_ptr`、`std::shared_ptr`、`wil::unique_handle` 或项目内现有封装。 / Replace raw ownership with RAII types such as `std::unique_ptr`, `std::shared_ptr`, `wil::unique_handle`, or existing project wrappers.
- 在不破坏 MSVC 兼容性、ABI、告警策略或 Windows 行为的前提下做现代化重构。 / Modernize legacy C++ constructs without breaking MSVC compatibility, ABI, warning policy, or Windows behavior.
- 重构 Win32、ATL、MFC、COM、DLL、服务程序或混合 C/C++ 代码。 / Refactor Win32, ATL, MFC, COM, DLL, service, or mixed C/C++ code.
- 当文件新增、重命名或移动时，同步更新 `.vcxproj`、`.filters` 及相关构建文件。 / Update `.vcxproj`, `.filters`, and adjacent build files when files are added, renamed, or moved.

## 目标结果 / Outcomes

- 除非用户明确要求改变行为，否则保持行为不变。 / Keep behavior unchanged unless the request explicitly asks for behavior changes.
- 保持 MSVC 可构建性、调用约定、导出可见性和预编译头要求。 / Preserve MSVC buildability, calling conventions, export visibility, and precompiled-header requirements.
- 最小化 diff，避免无关现代化扩散。 / Minimize diff size and avoid unrelated modernization.
- 每次实质修改后都做最窄范围验证。 / Validate with the narrowest available compile, test, or static check after each meaningful edit.

## 默认团队策略 / Default Team Policy

- 默认做保守重构。 / Default to conservative refactoring.
- 除非用户明确批准破坏性修改，否则保留 ABI、DLL 导出、调用约定、COM 合约和公共头文件形态。 / Preserve ABI, DLL exports, calling conventions, COM contracts, and public header shape unless the user explicitly approves a breaking change.
- 不要把现代化范围扩展到请求之外。 / Do not widen modernization scope beyond the requested slice.
- 如果重构改动了文件结构，必须在同一改动中同步 Visual Studio 工程元数据。 / When a refactor changes file layout, update Visual Studio project metadata in the same change.
- 动手前先用 [重构计划模板 / refactor plan template](./assets/refactor-plan-template.md) 形成紧凑计划。 / Before editing, draft a compact plan with the [refactor plan template](./assets/refactor-plan-template.md).

## 执行步骤 / Procedure

1. 找到最小的具体锚点。 / Identify the smallest concrete anchor.  
   中文：从请求里的文件、符号、构建报错、警告、测试或调用点开始。优先看真正控制行为的代码，而不是只负责转发的代码。  
   English: Start from the requested file, symbol, build error, warning, test, or call site. Prefer the code that directly controls behavior, not just wiring.

2. 形成一个可证伪的局部假设。 / Form one falsifiable local hypothesis.  
   中文：明确这次重构必须保留什么，或当前阻碍是什么。  
   English: State what the refactor should preserve or what currently blocks it.

3. 只读取控制这次改动的附近代码。 / Read only the nearby surfaces that control the change.  
   中文：检查声明、实现、直接调用点以及邻近测试；对 Windows 代码还要看宏、typedef、SAL 和导出装饰。  
   English: Inspect the declaration, implementation, direct call sites, neighboring tests, and for Windows code also macros, typedefs, SAL, and export decorators.

4. 编辑前检查 MSVC 特定约束。 / Check MSVC-specific constraints before editing.  
   中文：使用 [MSVC 重构检查清单 / MSVC refactor checklist](./references/msvc-refactor-checklist.md)，重点关注 `pch.h` / `stdafx.h`、`__declspec`、调用约定、`UNICODE` / `TCHAR`、COM 生命周期、`HRESULT`、`/WX` 以及头文件里的 inline 定义。  
   English: Use the [MSVC refactor checklist](./references/msvc-refactor-checklist.md), paying special attention to `pch.h` / `stdafx.h`, `__declspec`, calling conventions, `UNICODE` / `TCHAR`, COM lifetime, `HRESULT`, `/WX`, and inline header definitions.

5. 做最小的有根据的修改。 / Make the smallest grounded edit.  
   中文：优先做可逆的局部重构，避免在用户未要求时扩大到大范围清理。  
   English: Prefer a reversible, local refactor and avoid broad cleanup unless the user asked for it.

6. 立刻执行一次聚焦验证。 / Immediately run one focused validation.  
   中文：优先顺序是最小测试、最小项目构建、受影响翻译单元的编译检查，最后才是 diff 检查。  
   English: Prefer the smallest behavior test, then a narrow project build, then a focused compile/type check, and use diff review only if no executable validation exists.

7. 先局部修复，再扩大范围。 / Repair locally before expanding scope.  
   中文：如果验证失败，先修当前切片，再继续。  
   English: If validation fails, repair the current slice before broadening the refactor.

8. 结构变化时同步工程文件。 / Update project files when structure changes.  
   中文：文件移动或新增时，同步 `.vcxproj`、`.vcxproj.filters`、CMake 或其他工程元数据。  
   English: If files move or are added, update `.vcxproj`, `.vcxproj.filters`, CMake, or other project metadata.

9. 用 MSVC 术语总结结果。 / Summarize the result in MSVC terms.  
   中文：说明改了什么、保留了哪些约束、如何验证。  
   English: State what changed, what constraints were preserved, and how it was validated.

## 重构启发 / Refactor Heuristics

### 较安全的修改 / Safer Changes

- 抽取仅源文件内部使用的 helper 到匿名命名空间或 `static` 辅助函数。 / Extract internal helpers into an unnamed namespace or `static` source-local helpers when external linkage is unnecessary.
- 用项目里已有的作用域清理或 RAII 方式替换手工清理。 / Replace manual cleanup with scope-bound cleanup already used by the codebase.
- 仅在头文件不需要完整类型时，才用前向声明缩窄暴露面。 / Narrow header exposure with forward declarations only when complete types are not required.
- 在链接和头文件放置安全时，将魔法常量改为 `constexpr`。 / Convert magic constants to `constexpr` when linkage and header placement remain safe.

### 高风险修改 / High-Risk Changes

- 重命名导出类、接口、GUID、资源、注册表键或消息处理器。 / Renaming exported classes, interfaces, GUIDs, resources, registry keys, or message handlers.
- 改变结构体布局、对齐、打包、位域、枚举底层类型或公共头文件顺序。 / Changing struct layout, packing, alignment, bit fields, enum underlying types, or public header order.
- 把 inline 或模板代码移出头文件而未确认实例化仍然有效。 / Moving inline or template code out of headers without confirming instantiations remain valid.
- 改变异常边界、`noexcept`、`/EH*` 假设或 CRT 所有权。 / Changing exception boundaries, `noexcept`, `/EH*` assumptions, or CRT ownership.
- 修改 `AFX_MSG`、COM map、ATL 宏或消息映射宏。 / Altering `AFX_MSG`, COM maps, ATL macros, or message map macros.

## 验证规则 / Validation Rules

- 如果存在解决方案或工程文件，优先构建最小受影响项目。 / If a solution or project file exists, prefer building the smallest affected project first.
- 如果存在单元测试，优先运行最小受影响测试集。 / If unit tests exist, run the narrowest affected test set first.
- 如果只能拿到编译器反馈，把可能在 `/WX` 下失败的警告视为阻塞。 / If only compiler feedback is available, treat warnings that commonly fail under `/WX` as blockers.
- 如果环境无法构建，就对声明、定义、include 和工程文件引用做定点一致性检查，并明确说明验证缺口。 / If the environment cannot build, perform a targeted consistency review of declarations, definitions, includes, and project-file references, then report the validation gap clearly.

## 多 CLI 说明 / Multi-CLI Notes

- GitHub Copilot 使用 `.github/skills/msvc-cpp-refactor/`。 / GitHub Copilot uses `.github/skills/msvc-cpp-refactor/`.
- Claude 兼容客户端使用 `.claude/skills/msvc-cpp-refactor/`。 / Claude-compatible clients can use `.claude/skills/msvc-cpp-refactor/`.
- 通用 agent 风格 CLI，包括 WorkBuddy 类环境，可使用 `.agents/skills/msvc-cpp-refactor/`。 / Generic agent-style clients, including WorkBuddy-style setups, can use `.agents/skills/msvc-cpp-refactor/`.
- 编辑 skill 时，三份内容要保持语义一致。 / Keep all three copies semantically aligned when editing the skill.

## 输出风格 / Response Style

- 明确写出保留了哪些约束，例如 ABI、导出宏、调用约定、所有权、预编译头和告警洁净度。 / Be explicit about preserved constraints such as ABI, export macros, calling conventions, ownership, precompiled headers, and warning cleanliness.
- 保持修改小而局部。 / Keep changes small and local.
- 不要假设 GCC 或 Clang 的行为与 MSVC 相同。 / Do not assume GCC or Clang behavior matches MSVC behavior.
- 优先遵循项目现有习惯，而不是泛化的现代 C++ 改写。 / Prefer existing project idioms over generic modern C++ rewrites.

## 参考资料 / References

- [MSVC 重构检查清单 / MSVC refactor checklist](./references/msvc-refactor-checklist.md)
- [重构计划模板 / Refactor plan template](./assets/refactor-plan-template.md)
- [VCXPROJ 与 SLN 同步指南 / VCXPROJ and SLN sync guide](./references/vcxproj-sln-sync.md)
- [中文优先提示词示例集 / Chinese-first prompt examples](./references/chinese-prompt-examples.md)
- [工程文件同步检查脚本模板 / VCXPROJ sync check script template](./scripts/check-vcxproj-sync-template.ps1)
- [GitHub Actions workflow 模板 / GitHub Actions workflow template](./assets/vcxproj-sync-workflow-template.yml)
