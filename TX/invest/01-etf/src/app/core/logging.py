"""结构化日志（core/logging）。

禁止裸 print；统一使用本模块 logger。关键路径记录 index_code/source/cost_ms。
详见 docs/编码规范.md §7。
"""
from __future__ import annotations

import logging
import sys

logger = logging.getLogger("etf_query")

if not logger.handlers:
    _handler = logging.StreamHandler(sys.stdout)
    _handler.setFormatter(
        logging.Formatter("%(asctime)s %(levelname)s %(name)s %(message)s")
    )
    logger.addHandler(_handler)
    logger.setLevel(logging.INFO)

__all__ = ["logger"]
