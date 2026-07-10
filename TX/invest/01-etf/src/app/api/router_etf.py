"""ETF 检索/基本信息路由（FR-01/FR-02）。

接口契约对齐 SRS IR-02。
"""
from __future__ import annotations

from fastapi import APIRouter, HTTPException, Query

from app.api.deps import get_search_service
from app.models.schemas import EtfBasic, ETFSearchResult

router = APIRouter(prefix="/api/etf", tags=["etf"])


@router.get("/search", response_model=list[ETFSearchResult])
def search(
    keyword: str = Query(..., min_length=1, max_length=20),
    page: int = Query(1, ge=1),
    page_size: int = Query(50, ge=1, le=200),
) -> list[ETFSearchResult]:
    svc = get_search_service()
    try:
        return svc.search(keyword, page, page_size)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")


@router.get("/{code}/basic", response_model=EtfBasic)
def basic(code: str) -> EtfBasic:
    svc = get_search_service()
    try:
        return svc.get_basic(code)
    except NotImplementedError:
        raise HTTPException(status_code=501, detail="功能未实现")
