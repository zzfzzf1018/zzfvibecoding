"""akshare 调用的隔离运行器。

每个 akshare 网络调用都在独立子进程中执行（见 `_akshare_bridge.py` 说明），
彻底规避同进程多次调用相互污染导致的脏数据。调用失败（子进程非零退出）会抛出
RuntimeError，由上层数据源的 try/except 正常降级，绝不静默吞掉。
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import threading
import time

from app.core.logging import logger

_BRIDGE = os.path.join(os.path.dirname(__file__), "_akshare_bridge.py")

# 提供方（东财/中证/乐咕乐股）对同进程/同 IP 的连续请求会限流，导致子进程返回脏数据。
# 这里在父进程侧对每次 akshare 调用施加最小间隔并加重试，从根因上规避限流，而非吞错。
_MIN_GAP = 1.5
_MAX_RETRY = 3
_RETRY_BACKOFF = 2.0

_lock = threading.Lock()
_last_call = 0.0


def run_df(func: str, args=None, kwargs=None, timeout: int = 60):
    """在隔离子进程中执行 `akshare.<func>(*args, **kwargs)`，返回 DataFrame 或 None。

    返回值是用 `pd.read_json(orient="split")` 还原的 DataFrame，与直接在进程内
    调用 akshare 得到的 DataFrame 等价，父进程无需改动解析逻辑。

    通过全局最小间隔 + 指数退避重试规避提供方限流（限流时子进程通常非零退出，
    由重试捕获；个别脏响应由上层数据源的合理性校验兜底）。
    """
    payload = json.dumps({"func": func, "args": args or [], "kwargs": kwargs or {}})
    last_exc: Exception | None = None
    global _last_call
    for attempt in range(_MAX_RETRY):
        # 全局最小间隔：保证相邻两次 akshare 调用至少相隔 _MIN_GAP 秒
        with _lock:
            wait = _MIN_GAP - (time.monotonic() - _last_call)
            if wait > 0:
                time.sleep(wait)
            _last_call = time.monotonic()
        try:
            proc = subprocess.run(
                [sys.executable, _BRIDGE, payload],
                capture_output=True,
                text=True,
                timeout=timeout,
                encoding="utf-8",
            )
        except subprocess.TimeoutExpired:
            raise RuntimeError(f"akshare.{func} 子进程超时（>{timeout}s）") from None

        if proc.returncode != 0:
            last_exc = RuntimeError(f"akshare.{func} 子进程失败: {proc.stderr[-1500:]}")
            logger.warning("akshare.%s 第%d次失败（将重试）: %s", func, attempt + 1, proc.stderr[-500:])
            try:
                with open("runner_debug.txt", "a", encoding="utf-8") as _df:
                    _df.write("FAIL %s attempt=%d err=%s\n" % (func, attempt + 1, proc.stderr[-300:].replace("\n", " ")))
            except Exception:
                pass
            time.sleep(_RETRY_BACKOFF * (attempt + 1))
            continue
        try:
            with open("runner_debug.txt", "a", encoding="utf-8") as _df:
                _df.write("OK %s\n" % func)
        except Exception:
            pass

        out = (proc.stdout or "").strip()
        if not out or out == "null":
            return None
        import io

        import pandas as pd

        # out 是 JSON 字符串，必须用 StringIO 包裹，否则 read_json 会把它当成文件路径。
        return pd.read_json(io.StringIO(out), orient="split")
    # 重试耗尽：抛出，交由上层数据源降级（不静默吞掉）
    raise last_exc or RuntimeError(f"akshare.{func} 调用失败")
