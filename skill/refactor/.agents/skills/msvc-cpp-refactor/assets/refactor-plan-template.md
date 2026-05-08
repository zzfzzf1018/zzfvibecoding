# MSVC 重构计划模板 / MSVC Refactor Plan Template

中文：在做非平凡重构前，先填写这份模板。  
English: Use this template before making non-trivial changes.

## 重构目标 / Refactor Goal

- 请求内容 / Requested change:
- 目标文件或符号 / Target files or symbols:
- 非目标范围 / Non-goals:

## 需要保留的约束 / Preserved Constraints

- ABI 与导出表面 / ABI and exported surface:
- 调用约定与 SAL 合约 / Calling convention and SAL contracts:
- COM、ATL 或 MFC 约束 / COM, ATL, or MFC constraints:
- 预编译头与 include 顺序约束 / PCH and include-order constraints:
- 告警策略与工具集约束 / Warning policy and toolset constraints:

## 局部假设 / Local Hypothesis

- 假设 / Hypothesis:
- 最小证伪或验证检查 / Smallest confirming or falsifying check:

## 变更计划 / Change Plan

1. 第一个最小修改 / Smallest first edit:
2. 紧随其后的聚焦验证 / Immediate focused validation:
3. 若通过则继续的后续修改 / Follow-up edit if validation passes:

## 工程元数据影响 / Project Metadata Impact

- 需要更新的 `.vcxproj` / `.vcxproj` updates needed:
- 需要更新的 `.vcxproj.filters` / `.vcxproj.filters` updates needed:
- 需要更新的 `.sln` / `.sln` updates needed:
- 其他需要同步的构建文件 / Other build files to sync:

## 风险 / Risks

- 二进制兼容风险 / Binary compatibility risk:
- 所有权或生命周期风险 / Ownership or lifetime risk:
- 头文件或模板可见性风险 / Header or template visibility risk:
- Windows 宏或 Unicode 风险 / Windows macro or Unicode risk:

## 完成标准 / Done Criteria

- 已完成最小范围验证 / Narrow validation completed:
- 已同步工程元数据 / Project metadata synced:
- 未观察到意外 ABI 或合约漂移 / No unintended ABI or contract drift observed:
