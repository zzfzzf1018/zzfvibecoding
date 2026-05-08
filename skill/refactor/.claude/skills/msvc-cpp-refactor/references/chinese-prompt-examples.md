# 中文优先提示词示例集 / Chinese-First Prompt Examples

中文：下面这些提示词优先面向中文团队，重点覆盖保 ABI、保 DLL 导出、保调用约定、保预编译头、以及工程文件同步。  
English: These prompts are optimized for Chinese-first workflows and focus on preserving ABI, DLL exports, calling conventions, precompiled headers, and project-file sync.

## 1. 保 ABI 重构 / Preserve ABI

- `请重构这个 MSVC C++ 类，保持 ABI、导出接口、调用约定和 public header 形态不变，只做最小必要修改。`
- `重构这个 Visual Studio C++ 模块，但不要改 struct layout、vtable、enum underlying type，也不要扩大现代化范围。`
- `按保守重构方式整理这段 Win32 C++ 代码，保留二进制兼容性，并在每次修改后做最窄验证。`

## 2. 保 DLL 导出 / Preserve DLL Exports

- `重构这个 DLL 导出类，保留 __declspec(dllexport/dllimport)、导出宏、命名修饰和现有头文件接口。`
- `请整理这个导出函数实现，但不要改函数签名、调用约定、导出名或 DEF 文件关联。`
- `重构这个共享库边界附近的代码，保持 ABI 和导出表面稳定，不要引入破坏性变更。`

## 3. 保 PCH 与 Include 顺序 / Preserve PCH and Include Order

- `拆分这个源文件，但保留 pch.h 作为第一个 include，并检查新的翻译单元是否仍然满足预编译头规则。`
- `请把这段 helper 挪到新的 .cpp 中，但不要破坏 stdafx.h/pch.h 顺序，也不要引入额外的 Windows 头污染。`
- `重构这个头文件依赖时，优先用前向声明，但不能破坏 inline、template、sizeof 或 PCH 约束。`

## 4. 文件移动与工程同步 / File Move and Project Sync

- `把这个类拆到新文件里，并同步更新 .vcxproj、.vcxproj.filters 和相关构建元数据。`
- `重命名这些源文件后，请检查 Visual Studio 工程项、filters 和自定义构建步骤是否都已经同步。`
- `移动这几个 C++ 文件时，保留文件级编译选项，并确认新路径在 vcxproj 与 filters 中都存在。`

## 5. COM / ATL / MFC 场景 / COM, ATL, and MFC Scenarios

- `请重构这个 COM 组件实现，但保留 QueryInterface/AddRef/Release 语义，不要破坏 HRESULT 和接口所有权约定。`
- `整理这段 ATL 代码，但不要破坏 COM map、消息映射、导出宏和注册相关字符串。`
- `重构这个 MFC 类时，保留消息映射宏、资源 ID 和现有头文件暴露面。`

## 6. 要求先出计划再改代码 / Ask for a Plan First

- `先按 skill 的重构计划模板输出一个简短计划，再开始改这个 MSVC C++ 模块；重点列出 ABI、导出、PCH 和工程文件同步风险。`
- `先给出一个最小可证伪假设和最小验证步骤，再执行这次 Visual Studio C++ 重构。`

## 7. 同时要求脚本检查 / Ask for Script-Assisted Validation

- `改完后请运行工程文件同步检查脚本模板，确认 vcxproj、vcxproj.filters 和移动后的文件路径一致。`
- `在完成文件移动后，用 check-vcxproj-sync-template.ps1 检查旧路径是否还残留在工程文件中。`
