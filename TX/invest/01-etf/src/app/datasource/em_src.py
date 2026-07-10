"""东方财富适配器（兜底源，骨架/未实现）。

实现 ValuationSource 端口，行情/估值兜底。取数逻辑待按 docs/数据源调研.md 填充。
"""
from __future__ import annotations

from datetime import date

from app.models.schemas import IndexValuation, IndexValuationPoint
from app.core.clients import HttpClient


class EmValuationSource:
    name = "eastmoney"

    def __init__(self, client: HttpClient | None = None) -> None:
        self._client = client or HttpClient()

    def get_latest(self, index_code: str) -> IndexValuation | None:
        # TODO(SRS FR-03): 东方财富指数估值兜底
        raise NotImplementedError("EmValuationSource.get_latest 待实现")

    def get_history(
        self, index_code: str, start: date, end: date
    ) -> list[IndexValuationPoint]:
        # TODO(SRS FR-06): 东方财富历史估值兜底
        raise NotImplementedError("EmValuationSource.get_history 待实现")


__all__ = ["EmValuationSource"]
