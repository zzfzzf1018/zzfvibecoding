"""估值仓储（快照 + 历史序列，骨架/未实现）。

详见 SRS DR-02/DR-03、软件详细设计说明书 §1。
"""
from __future__ import annotations

from datetime import date
from typing import Callable

from sqlalchemy.orm import Session

from app.models.schemas import IndexValuation, IndexValuationPoint


class ValuationRepo:
    def __init__(self, session_factory: Callable[[], Session]) -> None:
        self._sf = session_factory

    def upsert_latest(self, v: IndexValuation) -> None:
        # TODO(DR-02): 估值快照 upsert
        raise NotImplementedError("ValuationRepo.upsert_latest 待实现")

    def get_latest(self, index_code: str) -> IndexValuation | None:
        # TODO(FR-03): 取最新估值快照
        raise NotImplementedError("ValuationRepo.get_latest 待实现")

    def append_history(self, point: IndexValuationPoint) -> None:
        # TODO(DR-03): 追加单日估值历史
        raise NotImplementedError("ValuationRepo.append_history 待实现")

    def get_history(
        self, index_code: str, start: date | None = None, end: date | None = None
    ) -> list[IndexValuationPoint]:
        # TODO(FR-04/FR-06): 取历史序列，按日期升序，支持区间
        raise NotImplementedError("ValuationRepo.get_history 待实现")


__all__ = ["ValuationRepo"]
