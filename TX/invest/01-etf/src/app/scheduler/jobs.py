"""定时更新任务（骨架/未实现）。

每日增量刷新基础信息、T+1 刷新估值、按频率刷新成分股。
失败单条目不影响整体，记录日志 + 告警。详见软件详细设计说明书 §7。
"""
from __future__ import annotations

from app.core.di import Container
from app.core.logging import logger


def refresh_etf_basic(container: Container | None = None) -> None:
    c = container or Container()
    # TODO(SRS FR-02): 遍历 c.basic_sources -> EtfRepo.upsert_basic
    logger.info("refresh_etf_basic: 待实现")
    raise NotImplementedError("refresh_etf_basic 待实现")


def refresh_valuation(container: Container | None = None) -> None:
    c = container or Container()
    # TODO(SRS FR-03/FR-06): 遍历跟踪指数 -> 选源取估值 -> upsert + append_history
    logger.info("refresh_valuation: 待实现")
    raise NotImplementedError("refresh_valuation 待实现")


def refresh_constituents(container: Container | None = None) -> None:
    c = container or Container()
    # TODO(SRS FR-05): 遍历跟踪指数 -> 选源取成分股 -> replace_constituents
    logger.info("refresh_constituents: 待实现")
    raise NotImplementedError("refresh_constituents 待实现")


__all__ = ["refresh_etf_basic", "refresh_valuation", "refresh_constituents"]
