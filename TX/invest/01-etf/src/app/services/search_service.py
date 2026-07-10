"""检索服务（FR-01，骨架/未实现）。

依赖 repositories（不依赖任何具体数据源）。详见软件详细设计说明书 §3.1。
"""
from __future__ import annotations

from app.models.schemas import ETFSearchResult
from app.repositories.etf_repo import EtfRepo


class SearchService:
    def __init__(self, etf_repo: EtfRepo) -> None:
        self._etf_repo = etf_repo

    def search(self, keyword: str, page: int = 1, page_size: int = 50) -> list[ETFSearchResult]:
        """FR-01: 编号精确/名称模糊检索；空关键字返回空；分页。"""
        raise NotImplementedError("SearchService.search 待实现")


__all__ = ["SearchService"]
