"""ETF 基础信息仓储（数据访问，骨架/未实现）。

SQL 只允许出现在本层；services 调用方法而非拼 SQL。详见 docs/AI开发约束.md §5。
"""
from __future__ import annotations

from typing import Callable

from sqlalchemy.orm import Session

from app.models.schemas import EtfBasic, ETFSearchResult


class EtfRepo:
    def __init__(self, session_factory: Callable[[], Session]) -> None:
        self._sf = session_factory

    def upsert_basic(self, etf: EtfBasic) -> None:
        # TODO(SRS DR-01): INSERT ... ON CONFLICT(code) DO UPDATE
        raise NotImplementedError("EtfRepo.upsert_basic 待实现")

    def get_basic(self, code: str) -> EtfBasic | None:
        # TODO(FR-02): 按 code 查询 etf_basic
        raise NotImplementedError("EtfRepo.get_basic 待实现")

    def search_by_code(self, keyword: str, page: int, size: int) -> list[ETFSearchResult]:
        # TODO(FR-01): code 前缀/精确匹配 + 分页
        raise NotImplementedError("EtfRepo.search_by_code 待实现")

    def search_by_name(self, keyword: str, page: int, size: int) -> list[ETFSearchResult]:
        # TODO(FR-01): 名称/简称/跟踪指数 模糊匹配(ILIKE/子串) + 分页
        raise NotImplementedError("EtfRepo.search_by_name 待实现")


__all__ = ["EtfRepo"]
