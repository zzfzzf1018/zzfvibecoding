# CLAUDE.md

中文：对于 MSVC 或 Visual Studio C++ 重构任务，优先使用 `./.claude/skills/msvc-cpp-refactor/SKILL.md`。  
English: For MSVC or Visual Studio C++ refactoring tasks, prefer `./.claude/skills/msvc-cpp-refactor/SKILL.md`.

默认策略 / Default policy:

- 保守重构，尽量保持行为不变。 / Keep refactors conservative and behavior-preserving.
- 除非用户明确允许破坏性修改，否则保留 ABI、导出宏、调用约定、COM 合约和预编译头规则。 / Preserve ABI, export macros, calling conventions, COM contracts, and precompiled-header rules unless the user explicitly approves a breaking change.
- 文件移动时，同步更新 `.vcxproj`、`.vcxproj.filters` 以及相关构建元数据。 / When files move, sync `.vcxproj`, `.vcxproj.filters`, and related build metadata in the same change.
