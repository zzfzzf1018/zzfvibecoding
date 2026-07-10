"""定时更新任务。

每日增量刷新基础信息、T+1 刷新估值、按频率刷新成分股。
失败单条目不影响整体，记录日志 + 告警。详见软件详细设计说明书 §7。
"""
from __future__ import annotations

from app.core.di import Container
from app.core.logging import logger
from app.models.schemas import IndexValuationPoint


def refresh_etf_basic(container: Container | None = None) -> None:
    c = container or Container()
    for src in c.basic_sources:
        try:
            etfs = src.list_etfs()
        except Exception as exc:  # noqa: BLE001
            logger.warning("basic source %s 刷新失败: %s", src.name, exc)
            continue
        for e in etfs:
            try:
                c.etf_repo.upsert_basic(e)
            except Exception as exc:  # noqa: BLE001
                logger.warning("upsert %s 失败: %s", e.code, exc)
    logger.info("refresh_etf_basic 完成")


def refresh_valuation(container: Container | None = None) -> None:
    c = container or Container()
    for idx in c.etf_repo.distinct_track_indices():
        v = None
        for src in c.valuation_sources:
            try:
                v = src.get_latest(idx)
            except Exception as exc:  # noqa: BLE001
                logger.warning("估值源 %s 获取 %s 失败: %s", src.name, idx, exc)
                continue
            if v:
                break
        if not v:
            logger.warning("估值缺失: %s", idx)
            continue
        try:
            c.valuation_repo.upsert_latest(v)
            if v.valuation_date:
                c.valuation_repo.append_history(
                    idx,
                    IndexValuationPoint(date=v.valuation_date, pe_ttm=v.pe_ttm, pb=v.pb),
                )
        except Exception as exc:  # noqa: BLE001
            logger.warning("估值写入 %s 失败: %s", idx, exc)
    logger.info("refresh_valuation 完成")


def refresh_constituents(container: Container | None = None) -> None:
    c = container or Container()
    for idx in c.etf_repo.distinct_track_indices():
        rows = None
        for src in c.constituent_sources:
            try:
                rows = src.get_constituents(idx)
            except Exception as exc:  # noqa: BLE001
                logger.warning("成分股源 %s 获取 %s 失败: %s", src.name, idx, exc)
                continue
            if rows:
                break
        if not rows:
            logger.warning("成分股缺失: %s", idx)
            continue
        try:
            c.constituent_repo.replace_constituents(idx, rows)
        except Exception as exc:  # noqa: BLE001
            logger.warning("成分股写入 %s 失败: %s", idx, exc)
    logger.info("refresh_constituents 完成")
