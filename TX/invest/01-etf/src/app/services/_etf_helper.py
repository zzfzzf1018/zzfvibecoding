"""ETF 解析辅助（同层内复用）。

负责：按 code 取 ETF；若缺失或缺少跟踪指数，则经 basic_sources 补全并落库。
保持 services 层只依赖 datasource.interfaces（抽象）与 repositories。
"""
from __future__ import annotations

from app.core.errors import NotFound
from app.core.logging import logger
from app.datasource.interfaces import EtfBasicSource
from app.models.schemas import EtfBasic
from app.repositories.etf_repo import EtfRepo


def resolve_etf(
    etf_repo: EtfRepo,
    basic_sources: list[EtfBasicSource],
    code: str,
) -> EtfBasic:
    """获取 ETF；若缺失或缺少跟踪指数，则尝试经 basic_sources 补全并落库。"""
    etf = etf_repo.get_basic(code)
    if etf is None or not etf.track_index_code:
        for src in basic_sources:
            try:
                fetched = src.get_etf(code)
            except Exception as exc:  # noqa: BLE001
                logger.warning("basic source %s 获取 %s 失败: %s", src.name, code, exc)
                continue
            if fetched:
                etf_repo.upsert_basic(fetched)
                etf = fetched
                if etf.track_index_code:
                    break
    if etf is None:
        raise NotFound(f"ETF {code} 不存在")
    return etf
