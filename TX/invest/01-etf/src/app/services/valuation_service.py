"""估值服务（FR-03/FR-06）。

依赖 etf_repo + valuation_repo + ValuationSource 端口列表（主→兜底顺序注入）。
债券/货币/商品类返回 applicable=False。详见软件详细设计说明书 §3.2。
"""
from __future__ import annotations

from datetime import date

from app.core.errors import SourceUnavailable
from app.core.logging import logger
from app.datasource.interfaces import EtfBasicSource, ValuationSource
from app.models.schemas import IndexValuation, IndexValuationPoint, ValuationView
from app.repositories.etf_repo import EtfRepo
from app.repositories.valuation_repo import ValuationRepo
from app.services._etf_helper import resolve_etf

_APPLICABLE_FALSE_TYPES = {"债券", "货币", "商品"}


class ValuationService:
    def __init__(
        self,
        etf_repo: EtfRepo,
        valuation_repo: ValuationRepo,
        valuation_sources: list[ValuationSource],
        basic_sources: list[EtfBasicSource] | None = None,
    ) -> None:
        self._etf_repo = etf_repo
        self._valuation_repo = valuation_repo
        self._sources = valuation_sources
        self._basic_sources = basic_sources or []

    def get_valuation(self, code: str) -> ValuationView:
        """FR-03: 取估值；缓存命中优先；否则遍历 sources 降级；不适用类显式标注。"""
        etf = resolve_etf(self._etf_repo, self._basic_sources, code)
        if etf.type in _APPLICABLE_FALSE_TYPES:
            return ValuationView(applicable=False, reason=f"{etf.type}类 ETF 无指数估值")
        idx = etf.track_index_code or etf.track_index
        if not idx:
            return ValuationView(applicable=False, reason="未配置跟踪指数")

        cached = self._valuation_repo.get_latest(idx)
        if cached:
            return self._to_view(cached)

        last_exc: Exception | None = None
        for src in self._sources:
            try:
                v = src.get_latest(idx, etf.track_index)
            except Exception as exc:  # noqa: BLE001
                logger.warning("估值源 %s 获取 %s 失败: %s", src.name, idx, exc)
                last_exc = exc
                continue
            if v is not None:
                self._valuation_repo.upsert_latest(v)
                if v.valuation_date:
                    self._valuation_repo.append_history(
                        idx, IndexValuationPoint(date=v.valuation_date, pe_ttm=v.pe_ttm, pb=v.pb)
                    )
                return self._to_view(v)

        raise SourceUnavailable(f"估值数据源不可用: {idx}")

    def get_history(
        self, code: str, start: date | None = None, end: date | None = None
    ) -> list[IndexValuationPoint]:
        """FR-06: 历史估值序列。"""
        etf = resolve_etf(self._etf_repo, self._basic_sources, code)
        idx = etf.track_index_code
        if not idx:
            return []
        return self._valuation_repo.get_history(idx, start, end)

    @staticmethod
    def _to_view(v: IndexValuation) -> ValuationView:
        return ValuationView(
            applicable=True,
            pe_ttm=v.pe_ttm,
            pb=v.pb,
            dividend_yield=v.dividend_yield,
            valuation_date=v.valuation_date,
            source=v.source,
        )
