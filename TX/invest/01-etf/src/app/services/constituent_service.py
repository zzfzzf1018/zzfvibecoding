"""成分股服务（FR-05，骨架/未实现）。

支持按权重排序、行业筛选、Top N、行业分布聚合。详见软件详细设计说明书 §3.4。
"""
from __future__ import annotations

from app.models.schemas import ConstituentStock, IndustryWeight
from app.repositories.constituent_repo import ConstituentRepo
from app.repositories.etf_repo import EtfRepo


class ConstituentService:
    def __init__(self, etf_repo: EtfRepo, constituent_repo: ConstituentRepo) -> None:
        self._etf_repo = etf_repo
        self._constituent_repo = constituent_repo

    def get_constituents(
        self, code: str, top_n: int | None = None, industry: str | None = None
    ) -> list[ConstituentStock]:
        """FR-05: 取成分股；按权重降序；支持行业过滤与 Top N。"""
        raise NotImplementedError("ConstituentService.get_constituents 待实现")

    def industry_distribution(self, code: str) -> list[IndustryWeight]:
        """FR-05: 按申万一级行业汇总权重。"""
        raise NotImplementedError("ConstituentService.industry_distribution 待实现")


__all__ = ["ConstituentService"]
