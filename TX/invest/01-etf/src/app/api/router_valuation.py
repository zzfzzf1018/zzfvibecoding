"""估值/分位/成分股路由（FR-03~FR-06，骨架）。

接口契约对齐 SRS IR-02。未实现返回 501。
"""
from __future__ import annotations

from datetime import date

from fastapi import APIRouter, HTTPException, Query

from app.api.deps import (
    get_constituent_service,
    get_percentile_service,
    get_valuation_service,
)
from app.models.schemas import (
    ConstituentStock,
    IndexValuationPoint,
    IndustryWeight,
    MonthlyPercentileView,
    ValuationView,
)

router = APIRouter(prefix="/api/etf", tags=["valuation"])


@router.get("/{code}/valuation", response_model=ValuationView)
def valuation(code: str) -> ValuationView:
    svc = get_valuation_service()
    try:
        return svc.get_valuation(code)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")


@router.get("/{code}/percentile", response_model=MonthlyPercentileView)
def percentile(
    code: str,
    months: int = Query(12, ge=1, le=36),
    window_years: int = Query(5, ge=1, le=20),
) -> MonthlyPercentileView:
    """FR-04：返回最近 `months` 个月、每月 PE/PB 的历史分位（滚动 `window_years` 年）。"""
    svc = get_percentile_service()
    try:
        return svc.get_monthly_percentile(code, months=months, window_years=window_years)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc))


@router.get("/{code}/constituents", response_model=list[ConstituentStock])
def constituents(
    code: str,
    top_n: int | None = Query(None, ge=1),
    industry: str | None = Query(None),
) -> list[ConstituentStock]:
    svc = get_constituent_service()
    try:
        return svc.get_constituents(code, top_n=top_n, industry=industry)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")


@router.get("/{code}/constituents/industry", response_model=list[IndustryWeight])
def industry_distribution(code: str) -> list[IndustryWeight]:
    svc = get_constituent_service()
    try:
        return svc.industry_distribution(code)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")


@router.get("/{code}/valuation/history", response_model=list[IndexValuationPoint])
def valuation_history(
    code: str,
    start: date | None = Query(None),
    end: date | None = Query(None),
) -> list[IndexValuationPoint]:
    svc = get_valuation_service()
    try:
        return svc.get_history(code, start, end)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")
