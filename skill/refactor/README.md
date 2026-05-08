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
- Chinese-first prompt examples / 中文优先提示词示例集
- VCXPROJ sync check script template / 工程文件同步检查脚本模板

## Extra Resources

- 中文优先提示词示例集 / Chinese-first prompt examples: [.github/skills/msvc-cpp-refactor/references/chinese-prompt-examples.md](.github/skills/msvc-cpp-refactor/references/chinese-prompt-examples.md)
- 工程文件同步检查脚本模板 / VCXPROJ sync check script template: [.github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1](.github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1)
- GitHub Actions workflow 模板 / GitHub Actions workflow template: [.github/skills/msvc-cpp-refactor/assets/vcxproj-sync-workflow-template.yml](.github/skills/msvc-cpp-refactor/assets/vcxproj-sync-workflow-template.yml)

运行示例 / Example command:

```powershell
pwsh -File .github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1 -ProjectFile .\MyProject.vcxproj -MovedPathPairs 'src\old.cpp=>src\new.cpp'
```

扫描整个仓库 / Scan the whole repository:

```powershell
pwsh -File .github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1 -WorkspaceRoot . -FailOnIssue
```

只检查 git diff 里受影响的工程 / Check only projects affected by git diff:

```powershell
pwsh -File .github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1 -GitRoot . -GitDiffRange HEAD -FailOnIssue
```

Pull Request 场景推荐范围 / Recommended pull request range:

```powershell
pwsh -File .github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1 -GitRoot . -GitDiffRange 'origin/main...HEAD' -FailOnIssue
```

## Automation Examples

### GitHub Actions

中文：下面的示例会在 Pull Request 上扫描仓库中的所有 `.vcxproj`，如果发现工程文件与磁盘状态不同步就让检查失败。  
English: The example below scans all `.vcxproj` files in the repository on pull requests and fails the check when project metadata is out of sync.

可直接复制的模板文件 / Ready-to-copy template file:

- [.github/skills/msvc-cpp-refactor/assets/vcxproj-sync-workflow-template.yml](.github/skills/msvc-cpp-refactor/assets/vcxproj-sync-workflow-template.yml)

```yaml
name: Verify VCXPROJ Sync

on:
  pull_request:

jobs:
  vcxproj-sync:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Check Visual Studio project sync
        shell: pwsh
        run: |
          .\.github\skills\msvc-cpp-refactor\scripts\check-vcxproj-sync-template.ps1 -WorkspaceRoot $PWD -FailOnIssue
```

### Local Pre-Commit

中文：如果团队希望在提交前阻止遗漏 `.vcxproj` / `.vcxproj.filters` 同步，可以把下面的示例放进本地 pre-commit hook。  
English: If the team wants to block commits that forget to sync `.vcxproj` / `.vcxproj.filters`, place one of the examples below in a local pre-commit hook.

` .git/hooks/pre-commit ` 示例 / example:

```sh
#!/usr/bin/env sh
pwsh -File ./.github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1 -WorkspaceRoot . -FailOnIssue
```

PowerShell 版本 / PowerShell variant:

```powershell
pwsh -File .github/skills/msvc-cpp-refactor/scripts/check-vcxproj-sync-template.ps1 -WorkspaceRoot $PWD -FailOnIssue
```

中文：如果只想检查单个工程，也可以继续使用 `-ProjectFile` 模式，并通过 `-MovedPathPairs` 明确声明本次移动的路径对。  
English: If you only want to check one project, continue to use `-ProjectFile` mode and specify moved-file expectations with `-MovedPathPairs`.

中文：如果仓库很大，优先用 `-GitDiffRange` 模式，只检查当前 diff 里出现的 `.vcxproj` / `.vcxproj.filters`，能显著降低扫描成本。  
English: For large repositories, prefer `-GitDiffRange` mode so the script only checks `.vcxproj` / `.vcxproj.filters` files present in the current diff.

## Maintenance Rule

中文：修改 skill 时，`.github`、`.claude`、`.agents` 三份内容应保持语义一致。  
English: When editing the skill, keep the `.github`, `.claude`, and `.agents` copies semantically aligned.
