"""对外数据模型（Pydantic）。

仅在此定义跨层传递的 DTO；禁止直接把 ORM 对象泄漏出 services 层。
字段定义与 SRS DR-01~05、软件详细设计说明书 §1 对齐。
"""
from __future__ import annotations

from datetime import date, datetime
from typing import Optional

from pydantic import BaseModel


class EtfBasic(BaseModel):
    code: str
    name: str
    short_name: Optional[str] = None
    type: Optional[str] = None
    track_index: Optional[str] = None
    track_index_code: Optional[str] = None
    fund_manager: Optional[str] = None
    custodian: Optional[str] = None
    fund_scale: Optional[float] = None
    shares: Optional[float] = None
    establish_date: Optional[date] = None
    manager: Optional[str] = None
    management_fee_rate: Optional[float] = None
    custody_fee_rate: Optional[float] = None
    tracking_error: Optional[float] = None
    exchange: Optional[str] = None
    update_time: Optional[datetime] = None


class IndexValuation(BaseModel):
    index_code: str
    pe_ttm: Optional[float] = None
    pb: Optional[float] = None
    dividend_yield: Optional[float] = None
    valuation_date: Optional[date] = None
    source: str


class IndexValuationPoint(BaseModel):
    date: date
    pe_ttm: Optional[float] = None
    pb: Optional[float] = None


class ConstituentStock(BaseModel):
    stock_code: str
    stock_name: Optional[str] = None
    sw_l1: Optional[str] = None
    weight: Optional[float] = None
    exchange: Optional[str] = None


class ValuationView(BaseModel):
    applicable: bool = True
    reason: Optional[str] = None
    pe_ttm: Optional[float] = None
    pb: Optional[float] = None
    dividend_yield: Optional[float] = None
    valuation_date: Optional[date] = None
    source: Optional[str] = None


class PercentileView(BaseModel):
    window: str
    sample_count: int
    pe_percentile: Optional[float] = None
    pb_percentile: Optional[float] = None
    pe_max: Optional[float] = None
    pe_min: Optional[float] = None
    pe_median: Optional[float] = None
    pb_max: Optional[float] = None
    pb_min: Optional[float] = None
    pb_median: Optional[float] = None
    degraded: bool = False


class ETFSearchResult(BaseModel):
    code: str
    name: Optional[str] = None
    type: Optional[str] = None
    track_index: Optional[str] = None
    track_index_code: Optional[str] = None
    latest_price: Optional[float] = None
    update_time: Optional[datetime] = None


class IndustryWeight(BaseModel):
    industry: str
    weight: float
