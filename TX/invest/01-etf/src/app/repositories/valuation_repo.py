"""估值仓储（快照 + 历史序列）。

详见 SRS DR-02/DR-03、软件详细设计说明书 §1。
"""
from __future__ import annotations

from datetime import date, datetime
from typing import Callable

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.models.orm import (
    IndexValuation as IndexValuationORM,
    IndexValuationHistory as IndexValuationHistoryORM,
)
from app.models.schemas import IndexValuation, IndexValuationPoint


class ValuationRepo:
    def __init__(self, session_factory: Callable[[], Session]) -> None:
        self._sf = session_factory

    def upsert_latest(self, v: IndexValuation) -> None:
        with self._sf() as s:
            s.merge(
                IndexValuationORM(
                    index_code=v.index_code,
                    pe_ttm=v.pe_ttm,
                    pb=v.pb,
                    dividend_yield=v.dividend_yield,
                    valuation_date=v.valuation_date,
                    source=v.source,
                    update_time=datetime.now(),
                )
            )
            s.commit()

    def get_latest(self, index_code: str) -> IndexValuation | None:
        with self._sf() as s:
            o = s.get(IndexValuationORM, index_code)
            if not o:
                return None
            return IndexValuation(
                index_code=o.index_code,
                pe_ttm=float(o.pe_ttm) if o.pe_ttm is not None else None,
                pb=float(o.pb) if o.pb is not None else None,
                dividend_yield=float(o.dividend_yield) if o.dividend_yield is not None else None,
                valuation_date=o.valuation_date,
                source=o.source,
            )

    def append_history(self, index_code: str, point: IndexValuationPoint) -> None:
        with self._sf() as s:
            s.merge(
                IndexValuationHistoryORM(
                    index_code=index_code,
                    date=point.date,
                    pe_ttm=point.pe_ttm,
                    pb=point.pb,
                )
            )
            s.commit()

    def get_history(
        self, index_code: str, start: date | None = None, end: date | None = None
    ) -> list[IndexValuationPoint]:
        with self._sf() as s:
            stmt = select(IndexValuationHistoryORM).where(
                IndexValuationHistoryORM.index_code == index_code
            )
            if start is not None:
                stmt = stmt.where(IndexValuationHistoryORM.date >= start)
            if end is not None:
                stmt = stmt.where(IndexValuationHistoryORM.date <= end)
            stmt = stmt.order_by(IndexValuationHistoryORM.date.asc())
            rows = s.execute(stmt).scalars().all()
            return [
                IndexValuationPoint(
                    date=r.date,
                    pe_ttm=float(r.pe_ttm) if r.pe_ttm is not None else None,
                    pb=float(r.pb) if r.pb is not None else None,
                )
                for r in rows
            ]
