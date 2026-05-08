# VCXPROJ 与 SLN 同步指南 / VCXPROJ and SLN Sync Guide

中文：当重构涉及新增、删除、重命名或移动文件时，使用这份指南同步 Visual Studio 工程元数据。  
English: Use this guide when a refactor adds, removes, renames, or moves files in a Visual Studio C++ codebase.

## 1. 更新工程文件 / Update the Project File

- 在 `.vcxproj` 中更新受影响的条目，例如 `ClCompile`、`ClInclude`、`ResourceCompile`、`None` 或 `CustomBuild`。 / In `.vcxproj`, update affected items such as `ClCompile`, `ClInclude`, `ResourceCompile`, `None`, or `CustomBuild`.
- 如果源文件移动了，确保旧路径被移除且新路径只添加一次。 / If a source file moved, ensure the old path is removed and the new path is added exactly once.
- 检查条目节点上的文件级设置，例如 PCH 模式、排除构建、自定义命令行或生成输出。 / Check file-specific settings stored on the item node, such as PCH mode, excluded-from-build settings, custom command lines, or generated outputs.
- 如果该文件依赖配置特定选项，重新检查 `ItemDefinitionGroup` 假设。 / Recheck `ItemDefinitionGroup` assumptions if the moved file relied on configuration-specific options.

## 2. 更新 Filters 文件 / Update the Filters File

- 在 `.vcxproj.filters` 中镜像文件移动。 / Mirror the file move in `.vcxproj.filters`.
- 让 filter 条目与新的虚拟目录布局保持一致。 / Keep filter entries aligned with the new virtual folder layout.
- 删除只引用已删除文件的陈旧 filter 节点。 / Remove stale filter nodes that only referenced deleted files.

## 3. 检查解决方案级影响 / Check Solution-Level Impact

- 对于现有项目内的简单文件重命名，`.sln` 通常不需要修改，但如果项目本身被移动、重命名、新增或删除，就必须核对。 / `.sln` usually does not need edits for simple file renames inside an existing project, but verify when projects are moved, renamed, added, or removed.
- 如果重构把代码拆到新项目里，要更新 solution project 条目、依赖关系和构建配置。 / If the refactor split code into a new project, update solution project entries, dependencies, and build configurations.

## 4. 重新检查 PCH 与 Include 假设 / Recheck PCH and Include Assumptions

- 如果新增了文件，确保它遵循项目的预编译头设置。 / If a file was newly added, ensure it follows the project's precompiled-header settings.
- 确认翻译单元按要求包含 `pch.h` 或 `stdafx.h`。 / Confirm the translation unit includes `pch.h` or `stdafx.h` as required.
- 如果头文件跨目录移动，重新检查相对 include 路径和附加 include 目录。 / If headers moved across folders, recheck relative include paths and additional include directories.

## 5. 生成文件与自定义构建步骤 / Generated Files and Custom Build Steps

- 保留 IDL、RC、MIDL、代码生成或资源预处理相关的自定义构建步骤。 / Preserve custom build steps for IDL, RC, MIDL, code generation, or asset preprocessing.
- 如果文件移动改变了相对路径假设，重新检查输出路径。 / Recheck output paths if the file move changed relative-path assumptions.
- 确认生成的头文件仍然输出到依赖项目预期的位置。 / Confirm generated headers still land where dependent projects expect them.

## 6. 常见失败模式 / Common Failure Modes

- 磁盘上已有文件，但 `.vcxproj` 中没有它。 / The file exists on disk but is missing from `.vcxproj`.
- `.vcxproj` 有文件，但 `.vcxproj.filters` 没同步，导致 Visual Studio 树结构漂移。 / The file exists in `.vcxproj` but not in `.vcxproj.filters`, causing Solution Explorer drift.
- 文件移动后丢失了文件级编译选项。 / A moved file lost per-file compile flags.
- 新源文件没有匹配的 PCH 设置，导致无法编译。 / A new source file was added without matching PCH settings and fails to compile.
- 自定义构建输出仍指向旧的相对路径。 / Custom build outputs still point at the old relative path.

## 7. 最小验证 / Minimal Validation

1. 如果编辑器缓存了工程状态，重新打开或 reload 项目。 / Re-open or reload the project if the editor caches project state.
2. 构建最小受影响项目。 / Build the smallest affected project.
3. 确认移动或新增文件出现在预期的 Solution Explorer 过滤器下。 / Confirm the moved or added file appears under the intended Solution Explorer filter.
4. 重新运行依赖这些移动文件的生成步骤。 / Re-run any generation step that depends on the moved files.
