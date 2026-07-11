"""成分股仓储。

详见 SRS DR-04、软件详细设计说明书 §1。
"""
from __future__ import annotations

from typing import Callable

from sqlalchemy import delete, select
from sqlalchemy.orm import Session

from app.models.orm import Exchange, IndexConstituent as IndexConstituentORM
from app.models.schemas import ConstituentStock


class ConstituentRepo:
    def __init__(self, session_factory: Callable[[], Session]) -> None:
        self._sf = session_factory

    def replace_constituents(self, index_code: str, rows: list[ConstituentStock]) -> None:
        with self._sf() as s:
            s.execute(
                delete(IndexConstituentORM).where(IndexConstituentORM.index_code == index_code)
            )
            for r in rows:
                s.add(
                    IndexConstituentORM(
                        index_code=index_code,
                        stock_code=r.stock_code,
                        stock_name=r.stock_name,
                        sw_l1=r.sw_l1,
                        weight=r.weight,
                        exchange=self._exchange(r.exchange),
                        effective_date=None,
                    )
                )
            s.commit()

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        with self._sf() as s:
            stmt = select(IndexConstituentORM).where(
                IndexConstituentORM.index_code == index_code
            )
            rows = s.execute(stmt).scalars().all()
            return [
                ConstituentStock(
                    stock_code=r.stock_code,
                    stock_name=r.stock_name,
                    sw_l1=r.sw_l1,
                    weight=float(r.weight) if r.weight is not None else None,
                    exchange=r.exchange.value if r.exchange else None,
                )
                for r in rows
            ]

    @staticmethod
    def _exchange(value: str | None) -> Exchange | None:
        if not value:
            return None
        try:
            return Exchange(value)
        except ValueError:
            return None
