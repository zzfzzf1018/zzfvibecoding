"""ETF 基础信息仓储（数据访问）。

SQL 仅允许出现在本层；services 调用方法而非拼 SQL。详见 docs/AI开发约束.md §5。
"""
from __future__ import annotations

from typing import Callable

from sqlalchemy import case, or_, select
from sqlalchemy.orm import Session

from app.models.orm import EtfBasic as EtfBasicORM, EtfType
from app.models.schemas import EtfBasic, ETFSearchResult

_TYPE_ORDER = {"宽基": 0, "行业": 1, "主题": 2, "策略": 3, "债券": 4, "货币": 5, "商品": 6, "跨境": 7}


class EtfRepo:
    def __init__(self, session_factory: Callable[[], Session]) -> None:
        self._sf = session_factory

    def upsert_basic(self, etf: EtfBasic) -> None:
        with self._sf() as s:
            s.merge(self._to_orm(etf))
            s.commit()

    def get_basic(self, code: str) -> EtfBasic | None:
        with self._sf() as s:
            orm = s.get(EtfBasicORM, code)
            return self._to_schema(orm) if orm else None

    def search_by_code(self, keyword: str, page: int, size: int) -> list[ETFSearchResult]:
        with self._sf() as s:
            stmt = (
                select(EtfBasicORM)
                .where(EtfBasicORM.code.like(f"{keyword}%"))
                .order_by(EtfBasicORM.fund_scale.is_(None), EtfBasicORM.fund_scale.desc())
                .limit(size)
                .offset((page - 1) * size)
            )
            rows = s.execute(stmt).scalars().all()
            return [self._to_search(r) for r in rows]

    def search_by_name(self, keyword: str, page: int, size: int) -> list[ETFSearchResult]:
        like = f"%{keyword}%"
        order = case(_TYPE_ORDER, value=EtfBasicORM.type, else_=9)
        with self._sf() as s:
            stmt = (
                select(EtfBasicORM)
                .where(
                    or_(
                        EtfBasicORM.name.ilike(like),
                        EtfBasicORM.short_name.ilike(like),
                        EtfBasicORM.track_index.ilike(like),
                    )
                )
                .order_by(order, EtfBasicORM.fund_scale.is_(None), EtfBasicORM.fund_scale.desc())
                .limit(size)
                .offset((page - 1) * size)
            )
            rows = s.execute(stmt).scalars().all()
            return [self._to_search(r) for r in rows]

    def distinct_track_indices(self) -> list[str]:
        with self._sf() as s:
            stmt = (
                select(EtfBasicORM.track_index_code)
                .where(EtfBasicORM.track_index_code.isnot(None))
                .distinct()
            )
            return [r[0] for r in s.execute(stmt).all()]

    # ---- mappers ----
    @staticmethod
    def _type_enum(value: str | None) -> EtfType | None:
        if not value:
            return None
        try:
            return EtfType(value)
        except ValueError:
            return None

    @staticmethod
    def _to_orm(e: EtfBasic) -> EtfBasicORM:
        return EtfBasicORM(
            code=e.code,
            name=e.name,
            short_name=e.short_name,
            type=EtfRepo._type_enum(e.type),
            track_index=e.track_index,
            track_index_code=e.track_index_code,
            fund_manager=e.fund_manager,
            custodian=e.custodian,
            fund_scale=e.fund_scale,
            shares=e.shares,
            establish_date=e.establish_date,
            manager=e.manager,
            management_fee_rate=e.management_fee_rate,
            custody_fee_rate=e.custody_fee_rate,
            tracking_error=e.tracking_error,
            exchange=e.exchange,
            update_time=e.update_time,
        )

    @staticmethod
    def _to_schema(o: EtfBasicORM) -> EtfBasic:
        return EtfBasic(
            code=o.code,
            name=o.name,
            short_name=o.short_name,
            type=o.type.value if o.type else None,
            track_index=o.track_index,
            track_index_code=o.track_index_code,
            fund_manager=o.fund_manager,
            custodian=o.custodian,
            fund_scale=float(o.fund_scale) if o.fund_scale is not None else None,
            shares=float(o.shares) if o.shares is not None else None,
            establish_date=o.establish_date,
            manager=o.manager,
            management_fee_rate=float(o.management_fee_rate) if o.management_fee_rate is not None else None,
            custody_fee_rate=float(o.custody_fee_rate) if o.custody_fee_rate is not None else None,
            tracking_error=float(o.tracking_error) if o.tracking_error is not None else None,
            exchange=o.exchange.value if o.exchange else None,
            update_time=o.update_time,
        )

    @staticmethod
    def _to_search(o: EtfBasicORM) -> ETFSearchResult:
        return ETFSearchResult(
            code=o.code,
            name=o.name,
            type=o.type.value if o.type else None,
            track_index=o.track_index,
            track_index_code=o.track_index_code,
            latest_price=None,
            update_time=o.update_time,
        )
