"""ORM 模型（SQLAlchemy 2.0 风格）。

表结构与 SRS DR-01~05、软件详细设计说明书 §1 对齐。
SQL 只允许出现在 repositories/ 层；本文件仅定义映射。
"""
from __future__ import annotations

from datetime import date, datetime
from enum import Enum

from sqlalchemy import Date, DateTime, Enum as SAEnum, Numeric, String
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


class EtfType(str, Enum):
    宽基 = "宽基"
    行业 = "行业"
    主题 = "主题"
    策略 = "策略"
    债券 = "债券"
    货币 = "货币"
    商品 = "商品"
    跨境 = "跨境"


class Exchange(str, Enum):
    SSE = "SSE"
    SZSE = "SZSE"


class Base(DeclarativeBase):
    pass


class EtfBasic(Base):
    __tablename__ = "etf_basic"

    code: Mapped[str] = mapped_column(String(6), primary_key=True)
    name: Mapped[str] = mapped_column(String)
    short_name: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    type: Mapped[Optional[str]] = mapped_column(SAEnum(EtfType), nullable=True)
    track_index: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    track_index_code: Mapped[Optional[str]] = mapped_column(String, nullable=True, index=True)
    fund_manager: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    custodian: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    fund_scale: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    shares: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    establish_date: Mapped[Optional[date]] = mapped_column(Date, nullable=True)
    manager: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    management_fee_rate: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    custody_fee_rate: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    tracking_error: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    exchange: Mapped[Optional[str]] = mapped_column(SAEnum(Exchange), nullable=True)
    update_time: Mapped[Optional[datetime]] = mapped_column(DateTime, nullable=True)


class IndexValuation(Base):
    __tablename__ = "index_valuation"

    index_code: Mapped[str] = mapped_column(String, primary_key=True)
    pe_ttm: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    pb: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    dividend_yield: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    valuation_date: Mapped[Optional[date]] = mapped_column(Date, nullable=True)
    source: Mapped[str] = mapped_column(String)
    update_time: Mapped[Optional[datetime]] = mapped_column(DateTime, nullable=True)


class IndexValuationHistory(Base):
    __tablename__ = "index_valuation_history"

    index_code: Mapped[str] = mapped_column(String, primary_key=True)
    date: Mapped[date] = mapped_column(Date, primary_key=True)
    pe_ttm: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    pb: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)


class IndexConstituent(Base):
    __tablename__ = "index_constituent"

    index_code: Mapped[str] = mapped_column(String, primary_key=True)
    stock_code: Mapped[str] = mapped_column(String, primary_key=True)
    stock_name: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    sw_l1: Mapped[Optional[str]] = mapped_column(String, nullable=True)
    weight: Mapped[Optional[float]] = mapped_column(Numeric, nullable=True)
    exchange: Mapped[Optional[str]] = mapped_column(SAEnum(Exchange), nullable=True)
    effective_date: Mapped[Optional[date]] = mapped_column(Date, nullable=True)
