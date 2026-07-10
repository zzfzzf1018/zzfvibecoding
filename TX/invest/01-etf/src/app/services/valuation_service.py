"""估值服务（FR-03/FR-06，骨架/未实现）。

依赖 etf_repo + valuation_repo + ValuationSource 端口列表（主→兜底顺序注入）。
债券/货币/商品类返回 applicable=False。详见软件详细设计说明书 §3.2。
"""
from __future__ import annotations

from datetime import date

from app.datasource.interfaces import ValuationSource
from app.models.schemas import IndexValuationPoint, ValuationView
from app.repositories.etf_repo import EtfRepo
from app.repositories.valuation_repo import ValuationRepo


class ValuationService:
    def __init__(
        self,
        etf_repo: EtfRepo,
        valuation_repo: ValuationRepo,
        sources: list[ValuationSource],
    ) -> None:
        self._etf_repo = etf_repo
        self._valuation_repo = valuation_repo
        self._sources = sources  # 主源在前，兜底源在后

    def get_valuation(self, code: str) -> ValuationView:
        """FR-03: 取估值；缓存命中优先；否则遍历 sources 降级；不适用类显式标注。"""
        raise NotImplementedError("ValuationService.get_valuation 待实现")

    def get_history(
        self, code: str, start: date | None = None, end: date | None = None
    ) -> list[IndexValuationPoint]:
        """FR-06: 历史估值序列。"""
        raise NotImplementedError("ValuationService.get_history 待实现")


__all__ = ["ValuationService"]
