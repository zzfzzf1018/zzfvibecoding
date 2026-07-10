# 中国股市 ETF 查询工具（代码骨架）

> 本仓库为**骨架**：包结构、接口契约、依赖注入与护栏测试已就绪，**业务实现为 TODO 占位**。
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

# 启动后端（模块使用 `from app...`，需在 src 目录运行）
cd src
python -m uvicorn main:app --reload            # 打开 http://127.0.0.1:8000/docs
```

> 注意：本机 `python` 若指向 Microsoft Store 占位程序，请使用真实 Python（如
> `C:\Users\zzf\.workbuddy\binaries\python\versions\3.14.3\python.exe`）。
> 首次访问会建表（SQLite `etf.db`）；数据需先运行 `refresh_*` 或访问接口触发自采。

## 接手开发顺序（建议）
1. 按 `docs/数据源调研.md` 实现 `datasource/akshare_src.py` 各方法。
2. 实现 `repositories/*` 的 SQL（upsert / 模糊检索 / 历史序列）。
3. 实现 `services/*` 业务逻辑（检索、估值降级、分位算法、成分股聚合）。
4. 实现 `scheduler/jobs.py` 定时刷新。
5. 每次改动按 `docs/AI开发约束.md` 在 `docs/开发记录.md` 追加 `CHG-` 记录。

## 测试
- `tests/test_coupling.py`：静态断言 `services/`、`api/` 不跨层依赖（松耦合护栏）。
- `tests/test_interfaces.py`：断言数据源实现满足 Protocol。

## 配置
通过环境变量（见 `src/app/core/config.py`）：`DB_URL`、`HTTP_TIMEOUT`、`RATE_LIMIT`、
`CACHE_TTL_*`、`MIN_SAMPLES`(默认 250)。
