"""中证指数公司官网适配器（兜底源，骨架/未实现）。

实现 ValuationSource / ConstituentSource 端口，用于 AkShare 失效时兜底。
取数逻辑待按 docs/数据源调研.md 的中证官网接口填充。
"""
from __future__ import annotations

from datetime import date

from app.models.schemas import ConstituentStock, IndexValuation, IndexValuationPoint
from app.core.clients import HttpClient


class CsIndexValuationSource:
    name = "csindex"

    def __init__(self, client: HttpClient | None = None) -> None:
        self._client = client or HttpClient()

    def get_latest(self, index_code: str) -> IndexValuation | None:
        # TODO(SRS FR-03): 中证估值接口 https://www.csindex.com.cn/csindex-home/valuation/indices
        raise NotImplementedError("CsIndexValuationSource.get_latest 待实现")

    def get_history(
        self, index_code: str, start: date, end: date
    ) -> list[IndexValuationPoint]:
        # TODO(SRS FR-06): 中证历史估值
        raise NotImplementedError("CsIndexValuationSource.get_history 待实现")


class CsIndexConstituentSource:
    name = "csindex"

    def __init__(self, client: HttpClient | None = None) -> None:
        self._client = client or HttpClient()

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        # TODO(SRS FR-05): 中证成分股 https://www.csindex.com.cn/csindex-home/component/detail
        raise NotImplementedError("CsIndexConstituentSource.get_constituents 待实现")


__all__ = ["CsIndexValuationSource", "CsIndexConstituentSource"]
