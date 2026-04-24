# cppchecktool

`cppchecktool` is a lightweight C++ static analysis command-line tool focused on common risks in Windows/MSVC codebases. The current version combines built-in heuristic rules with an optional `cppcheck` backend and uses `yaml-cpp` for full YAML config parsing while remaining practical for MSVC v141 environments.

## Current checks

- `SEC001`: dangerous runtime APIs such as `strcpy`, `sprintf`, `gets`, `system`
- `MEM001`: raw `new` usage
- `MEM002`: raw `delete` usage
- `CON001`: detached thread usage
- `MNT001`: `using namespace std;`
- `MSC001`: `_CRT_SECURE_NO_WARNINGS`
- `REL001`: empty `catch (...) {}` blocks

## Current capabilities

- Scan a file, directory, or `compile_commands.json`
- Load a simple YAML config file
- Run built-in checks only, `cppcheck` only, or both
- Emit plain text or SARIF
- Suppress issues inline with `check-ignore: RULE_ID`
- Auto-discover `cppcheck` in common Windows install locations

## Build

### Visual Studio 2017 / MSVC v141

```powershell
cmake -S . -B build-v141 -G "Visual Studio 15 2017" -A x64 -T v141
cmake --build build-v141 --config Release
```

Or use the preset:

```powershell
cmake --preset vs2017-v141-release
cmake --build --preset build-vs2017-v141-release
```

### Newer Visual Studio toolsets

```powershell
cmake -S . -B build
cmake --build build --config Debug
```

Or use the preset:

```powershell
cmake --preset vs2022-debug
cmake --build --preset build-vs2022-debug
```

### Ninja preset for compile database generation

```powershell
cmake --preset ninja-debug
./build/Debug/cppchecktool.exe --compile-db build/ninja-debug/compile_commands.json --backend builtin --exclude _deps
```

This preset is useful when you want a real `compile_commands.json` for scanning the current project.

If the environment contains an invalid `CMAKE_TOOLCHAIN_FILE`, the project will ignore it and continue configuring.

## Usage

```powershell
./build/Debug/cppchecktool.exe tests/samples
./build/Debug/cppchecktool.exe --compile-db tests/samples/compile_commands.json --backend builtin
./build/Debug/cppchecktool.exe --config tests/samples/tool.yml
./build/Debug/cppchecktool.exe --backend both --cppcheck-path C:/tools/cppcheck/cppcheck.exe tests/samples
./build/Debug/cppchecktool.exe --format sarif --output report.sarif tests/samples
./build/Debug/cppchecktool.exe --exclude third_party src
```

### YAML config example

```yaml
target: .
format: text
backend: builtin
exclude:
	- clean.cpp
```

Relative paths in the YAML file are resolved from the directory that contains the config file.

Supported keys in the current parser:

- `target`
- `output`
- `compile_database`
- `cppcheck_path`
- `cppcheck_enable`
- `format`
- `backend`
- `recursive`
- `exclude`
- `extensions`

The config parser is backed by `yaml-cpp`, so standard YAML quoting, indentation, and sequences are supported.

## Suppression

Suppress a rule on the same line or the next line with:

```cpp
// check-ignore: SEC001
system("pause");
```

You can also suppress all rules for a line with `check-ignore: all`.

## Notes

- This version is heuristic. It does not perform full AST parsing or cross-translation-unit data-flow analysis.
- `cppcheck` integration is optional. In `auto` mode the tool first probes the configured executable name, then common Windows install paths such as `C:/Program Files/Cppcheck/cppcheck.exe`, and falls back to built-in rules only if nothing is available.
- The current output formats are plain text and SARIF.
- The focus is quick risk surfacing, not zero false positives.