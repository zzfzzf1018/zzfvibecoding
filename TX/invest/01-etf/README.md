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
无需手动装依赖，脚本自动装运行时依赖并启动。下面给出**三种方式**，任选其一。

### 方式一：Windows 双击（最简单）
直接双击项目根目录的 **`run.bat`** 即可。它会：
- 自动绕过 PowerShell 执行策略限制（避免"双击没反应"）；
- 在**同一个窗口**显示启动日志（窗口不会神秘消失）；
- 启动成功后**自动打开浏览器**到 `http://localhost:8000/`。
> 关闭那个黑色窗口 = 停止服务。

### 方式二：PowerShell 命令行（可加 -Seed 等参数）
```powershell
# 在项目根目录执行
powershell -ExecutionPolicy Bypass -File .\scripts\run.ps1            # 默认端口 8000，自动开浏览器
powershell -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Seed      # 启动前播种 ETF 列表（让名称搜索可用，需联网）
powershell -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Port 8080 -NoBrowser -Dev
```
参数：`-Seed`(播种列表) / `-Port <端口>` / `-NoBrowser`(不开浏览器) / `-Dev`(热重载) / `-Python <python路径>`。

### 方式三：跨平台 Python 启动器（macOS / Linux，或 Windows 终端）
```bash
python run.py                 # 装依赖 + 启动 + 自动打开浏览器
python run.py --seed          # 启动前播种 ETF 列表（让名称搜索可用，需联网）
python run.py --port 8080 --no-browser
python run.py --dev           # 热重载
```
> 注意：Windows 上若 `python` 指向 Microsoft Store 占位程序，请改用 `run.bat`，或用真实 Python：
> `C:\Users\zzf\.workbuddy\binaries\python\versions\3.14.3\python.exe run.py`

### 启动后如何打开网页查看
1. 脚本启动成功会**自动打开浏览器**；若没自动打开，请手动访问：
   - **Web 页面（查询界面）**：`http://localhost:8000/`
   - **接口文档（Swagger）**：`http://localhost:8000/docs`
2. 在页面输入 ETF 代码（如 `510300`、`159915`）即可查看基本信息 / 估值 / 历史分位 / 成分股。
3. 搜索数据从哪来（重要）：
   - **名称搜索**（如 `沪深300`、`白酒`）：命中本地库，因此**必须先用 `-Seed`/`--seed` 落库**（联网拉全量 ETF 列表，可能耗时/被限频）。未 seed 时名称搜不到是预期的。
   - **代码搜索**（如 `510300`、`159915`）：即使**未先 `-Seed`**，也会走 AkShare 单条解析直接查出（需联网）；先 `-Seed` 后更快且可离线。
   - 数据库统一落在 **`src/etf.db`**（seed 与 server 共用同一文件，已修复旧版"seed 写根目录、server 读 src 目录"导致搜不到的问题）。

### 没反应 / 排错
- **双击 `.ps1` 没反应**：是 Windows 默认禁止运行脚本所致 —— 改用双击 **`run.bat`** 或在 PowerShell 里加 `-ExecutionPolicy Bypass` 运行（见方式二）。
- **双击 `run.py` 一闪而过**：多为 Python 未关联或 `pip` 安装失败。请用上面的命令行方式，或在 `run.py` 末尾查看错误信息（已加保护，双击报错会停在"按 Enter 退出"）。
- **浏览器没自动打开**：手动访问 `http://localhost:8000/`；或加 `-NoBrowser` 后用该地址。
- **端口被占用**：换端口，如 `-Port 8080` 或 `python run.py --port 8080`。
- **本机无外网**：akshare 取数会失败并降级到 404/兜底源，页面与接口仍可正常打开、不崩溃；联网后即有真实数据。
- **搜索一直为空（旧库不一致）**：若曾用旧版脚本导致项目根 `etf.db` 与 `src/etf.db` 各一份（seed 与 server 不在同一库），请删掉这两个 `etf.db`，再用 `-Seed`/`--seed` 重新落库即可。新脚本已统一到 `src/etf.db`。

### 脚本内部做了什么
`run.py` / `run.ps1` / `run.bat` 会：① 解析 Python（默认 CodeBuddy 内置，可用 `-Python` 或 `ETF_PYTHON` 覆盖）；
② `pip` 安装运行时依赖（`requirements-runtime.txt`，akshare 安装失败则降级到中证/东财兜底源）；
③ 可选 `--seed`/`-Seed` 调用 `refresh_etf_basic` 播种基础列表；④ 在 `src/` 下启动 `uvicorn main:app`（前台运行，日志可见）。
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
