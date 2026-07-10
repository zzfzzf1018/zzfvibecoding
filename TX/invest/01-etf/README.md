# 中国股市 ETF 查询工具

> 已实现：检索、基本信息、PE/PB 估值（多源降级）、历史分位、成分股，以及最简 Web UI。
> 设计依据见 `docs/` 下各文档（PRD / SRS / 架构 / 概要 / 详细 / REVIEW / 编码规范 / AI开发约束）。

## 分层结构（单向依赖）
```
api → services → (datasource.interfaces + repositories)
                     ↑ 具体源(akshare/csindex/em) 经 core/di 注入
```
- `services/` 只依赖 `datasource.interfaces` 抽象与 `repositories`，**禁止**直接 `import akshare` 或具体源。
- 外部请求只在 `datasource/*` 与 `core/clients/http_client.py`。
- SQL 只在 `repositories/`。

## 快速开始
```bash
python -m venv .venv && .venv\Scripts\activate   # Windows
pip install -r requirements.txt

# 测试（pyproject 已将 src 加入 pythonpath）
pytest                                         # 护栏测试 + 服务单测

# 启动后端 + Web UI（模块使用 `from app...`，需在 src 目录运行）
cd src
python -m uvicorn main:app --reload
# 打开页面： http://127.0.0.1:8000/            （最简 Web UI）
# 接口文档： http://127.0.0.1:8000/docs
```

> 注意：本机 `python` 若指向 Microsoft Store 占位程序，请使用真实 Python（如
> `C:\Users\zzf\.workbuddy\binaries\python\versions\3.14.3\python.exe`）。
> 首次访问会建表（SQLite `etf.db`）；数据需先运行 `refresh_*` 或访问接口触发自采。

## 一键运行（推荐）
无需手动装依赖，脚本自动装运行时依赖并启动：

```bash
# 跨平台（Python 启动器）
python run.py                 # 装依赖 + 启动 + 自动打开浏览器
python run.py --seed          # 启动前先播种 ETF 列表（让名称搜索可用，需联网）
python run.py --port 8080 --no-browser
python run.py --dev            # 热重载

# Windows / PowerShell
.\scripts\run.ps1              # 同上，自动选用 CodeBuddy 内置 Python
.\scripts\run.ps1 -Seed        # 启动前播种 ETF 列表
```

`run.py` / `run.ps1` 会：① 解析 Python（默认 CodeBuddy 内置，可用 `-Python` 或 `ETF_PYTHON` 覆盖）；
② `pip` 安装运行时依赖（`requirements-runtime.txt`，akshare 安装失败则降级到中证/东财兜底源）；
③ 可选 `--seed` 调用 `refresh_etf_basic` 播种基础列表；④ 启动 `uvicorn main:app` 并打开浏览器。
运行入口在 `src/` 目录（`from app...` 导入要求），故脚本内部已 `cd src` 并使用 `main:app` 模块路径。

## 数据源与多源降级
估值/成分股依次尝试：**AkShare（主）→ 中证官网（兜底）→ 东方财富（兜底）**。
任一源解析失败返回 `None`，由 `ValuationService` 自动降级到下一个源，保证主流程不中断。
（中证官网/东财接口形态未公开、按经验实现，真实环境需联网验证；解析失败时安全降级。）

## 接手开发顺序（建议）
1. 完善 `datasource/csindex_src.py` / `em_src.py` 的字段解析（联网联调验证）。
2. 实现 `scheduler/jobs.py` 的 APScheduler 定时入口（当前已有 refresh_* 函数）。
3. 补充历史估值采集（调度自采落库，支撑历史分位）。
4. 每次改动按 `docs/AI开发约束.md` 在 `docs/开发记录.md` 追加 `CHG-` 记录。

## 测试
- `tests/test_coupling.py`：静态断言 `services/`、`api/` 不跨层依赖（松耦合护栏）。
- `tests/test_interfaces.py`：断言数据源实现满足 Protocol。

## 配置
通过环境变量（见 `src/app/core/config.py`）：`DB_URL`、`HTTP_TIMEOUT`、`RATE_LIMIT`、
`CACHE_TTL_*`、`MIN_SAMPLES`(默认 250)。
