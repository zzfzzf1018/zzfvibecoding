#!/usr/bin/env python3
"""ETF 查询工具 —— 一键编译/运行入口（跨平台）。

自动安装运行时依赖、可选播种 ETF 列表，再启动 uvicorn 并打开浏览器。

用法:
  python run.py                 # 安装依赖 + 启动服务 + 打开浏览器
  python run.py --seed          # 启动前播种 ETF 列表（让名称搜索可用，需联网）
  python run.py --port 8080 --no-browser
  python run.py --dev           # 开启热重载
"""
from __future__ import annotations

import argparse
import subprocess
import sys
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "src"


def _pip(packages: list[str]) -> int:
    return subprocess.run([sys.executable, "-m", "pip", "install", "-q", *packages]).returncode


def ensure_deps() -> None:
    print("==> 安装运行时基础依赖...")
    _pip(["fastapi", "uvicorn[standard]", "sqlalchemy", "pydantic", "requests", "apscheduler"])
    print("==> 安装主数据源 akshare（失败则降级到中证/东财兜底源，仍可运行）...")
    if _pip(["akshare"]) != 0:
        print("警告: akshare 安装失败，将仅使用中证/东财兜底源（需联网）。")


def seed() -> None:
    print("==> 播种 ETF 基础列表（联网拉取，可能需要几十秒）...")
    sys.path.insert(0, str(SRC))
    from app.core.di import Container
    from app.scheduler.jobs import refresh_etf_basic

    refresh_etf_basic(Container())
    print("==> 播种完成，名称/代码搜索已可用。")


def _open_browser(url: str) -> None:
    time.sleep(2)
    import webbrowser

    webbrowser.open(url)


def main() -> None:
    ap = argparse.ArgumentParser(description="ETF 查询工具 一键运行")
    ap.add_argument("--port", type=int, default=8000)
    ap.add_argument("--host", default="0.0.0.0")
    ap.add_argument("--seed", action="store_true", help="启动前播种 ETF 列表（让名称搜索可用）")
    ap.add_argument("--no-browser", action="store_true")
    ap.add_argument("--dev", action="store_true", help="开启热重载")
    args = ap.parse_args()

    ensure_deps()
    if args.seed:
        seed()

    if not args.no_browser:
        threading.Thread(
            target=_open_browser, args=(f"http://localhost:{args.port}/",), daemon=True
        ).start()

    # 通过 uvicorn CLI 在 src/ 下启动：CLI 会把 cwd(src) 加入 sys.path，
    # 使 `from app...` 与模块 `main:app` 均可导入（等价于 `cd src && uvicorn main:app`）。
    cmd = [sys.executable, "-m", "uvicorn", "main:app", "--host", args.host, "--port", str(args.port)]
    if args.dev:
        cmd.append("--reload")

    print(f"==> 启动服务: http://localhost:{args.port}/  (Ctrl+C 退出)")
    subprocess.run(cmd, cwd=str(SRC))


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:  # 双击运行时避免窗口一闪而过
        import traceback

        print(f"[错误] 启动失败: {e}", file=sys.stderr)
        traceback.print_exc()
        if sys.platform == "win32":
            try:
                input("按 Enter 退出...")
            except Exception:
                pass
