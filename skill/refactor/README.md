# MSVC C++ Refactor Skill

中文：这是一个面向 MSVC / Visual Studio C++ 重构场景的多 CLI skill 集合，默认强调保守重构、行为保持、ABI 稳定、项目文件同步。  
English: This repository contains a multi-CLI skill set for MSVC / Visual Studio C++ refactoring, optimized for conservative, behavior-preserving changes with ABI and project-file safety.

## CLI Support

- GitHub Copilot: use [.github/skills/msvc-cpp-refactor/SKILL.md](.github/skills/msvc-cpp-refactor/SKILL.md)
- Claude-compatible clients: use [.claude/skills/msvc-cpp-refactor/SKILL.md](.claude/skills/msvc-cpp-refactor/SKILL.md)
- Generic agent-style CLIs and WorkBuddy-style setups: use [.agents/skills/msvc-cpp-refactor/SKILL.md](.agents/skills/msvc-cpp-refactor/SKILL.md)

## How To Trigger

### GitHub Copilot

中文：把工程作为 VS Code 工作区打开后，在 Chat 中直接描述需求，或输入 `/msvc-cpp-refactor`。  
English: Open the project in VS Code, then describe the task in Chat or invoke `/msvc-cpp-refactor` directly.

建议提示词 / Suggested prompts:

- `重构这个 MSVC C++ 类，保持 ABI，不要改导出接口`
- `Refactor this Visual Studio C++ module, preserve ABI, and keep vcxproj in sync`

### Claude-Compatible CLI

中文：将本仓库或对应 skill 目录放到支持 `.claude/skills/` 的环境中，然后直接请求执行 MSVC C++ 重构。  
English: Place this repository or the skill folder into an environment that recognizes `.claude/skills/`, then ask for an MSVC C++ refactor task directly.

建议提示词 / Suggested prompts:

- `请按保守重构方式处理这个 Win32 模块，保留调用约定和 PCH 规则`
- `Use the MSVC refactor skill and split this header safely without breaking precompiled headers`

### WorkBuddy / Generic Agent CLI

中文：优先使用根目录 [AGENTS.md](AGENTS.md) 作为入口，并确保客户端会读取 `.agents/skills/`。如果 WorkBuddy 不能自动发现 skill，就在提示词里显式指定 [.agents/skills/msvc-cpp-refactor/SKILL.md](.agents/skills/msvc-cpp-refactor/SKILL.md)。  
English: Prefer the root [AGENTS.md](AGENTS.md) as the entry point and ensure the client reads `.agents/skills/`. If WorkBuddy cannot auto-discover the skill, explicitly point it to [.agents/skills/msvc-cpp-refactor/SKILL.md](.agents/skills/msvc-cpp-refactor/SKILL.md) in your prompt.

建议提示词 / Suggested prompts:

- `Use the skill at ./.agents/skills/msvc-cpp-refactor/SKILL.md to refactor this ATL code conservatively`
- `按 ./.agents/skills/msvc-cpp-refactor/SKILL.md 的规则重构这个 DLL 导出类`

## File Layout

- Copilot skill: [.github/skills/msvc-cpp-refactor/SKILL.md](.github/skills/msvc-cpp-refactor/SKILL.md)
- Claude skill: [.claude/skills/msvc-cpp-refactor/SKILL.md](.claude/skills/msvc-cpp-refactor/SKILL.md)
- Generic agent skill: [.agents/skills/msvc-cpp-refactor/SKILL.md](.agents/skills/msvc-cpp-refactor/SKILL.md)
- Generic CLI entry: [AGENTS.md](AGENTS.md)
- Claude entry: [CLAUDE.md](CLAUDE.md)

## Included References

- Refactor plan template / 重构计划模板
- MSVC refactor checklist / MSVC 重构检查清单
- VCXPROJ and SLN sync guide / 工程文件同步指南

## Maintenance Rule

中文：修改 skill 时，`.github`、`.claude`、`.agents` 三份内容应保持语义一致。  
English: When editing the skill, keep the `.github`, `.claude`, and `.agents` copies semantically aligned.
