"""akshare 子进程桥接。

背景：akshare 在部分 Python（3.14）/网络环境下，同一进程内连续多次网络调用会
相互污染（连接池复用/提供方限流），导致第二次及以后的调用返回脏数据（如 pe=1.0）。
为避免「根因未解、靠缓存掩盖」的错误做法，这里让每一个 akshare 调用都在隔离的
子进程中执行——子进程内只发生一次网络调用，永远等价于「首次调用」，结果稳定。

本文件仅依赖 akshare + pandas，严禁 import app（保持隔离）。父进程通过
`_runner.run_df` 以 JSON(split) 取回 DataFrame。
"""
from __future__ import annotations

import json
import sys

import pandas as pd

# 子进程 stdout/stderr 在 Windows 下默认可能不是 UTF-8（GBK），强制为 UTF-8，
# 否则含中文的 JSON 写出后父进程按 UTF-8 读取会解码失败。
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:  # noqa: BLE001
    pass

# 部分环境配置了不可达的 HTTP(S) 代理（如本机 dead proxy），会导致 eastmoney 等域名
# 请求报 ProxyError（"Unable to connect to proxy"）。由于该代理本身不可达，清除后改为
# 直连反而可用；对已在 NO_PROXY 中的 sina / lg / csindex 等源无影响。这从根因上修复
# `index_code_id_map_em` 等依赖 eastmoney 的「名->码」映射拉取失败问题。
import os

for _p in ("HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy",
          "ALL_PROXY", "all_proxy", "no_proxy", "NO_PROXY"):
    os.environ.pop(_p, None)


def _main() -> None:
    req = json.loads(sys.argv[1])
    import akshare as ak

    func = getattr(ak, req["func"])
    df = func(*req.get("args", []), **req.get("kwargs", {}))
    if df is None:
        sys.stdout.write("null")
        return
    # orient="split" 同时保留 columns/index/data，父进程 pd.read_json 可无损还原；
    # date_format="iso" 把日期列转成 ISO 字符串，父进程统一用 _to_date 解析。
    sys.stdout.write(df.to_json(orient="split", date_format="iso", force_ascii=False))


if __name__ == "__main__":
    try:
        _main()
    except Exception:  # noqa: BLE001 - 错误信息经 stderr 返回父进程，不在此吞掉
        import traceback

        traceback.print_exc()
        sys.exit(1)
