"""成分股服务（FR-05）。

支持按权重排序、行业筛选、Top N、行业分布聚合。详见软件详细设计说明书 §3.4。
"""
from __future__ import annotations

from app.datasource.interfaces import EtfBasicSource
from app.models.schemas import ConstituentStock, IndustryWeight
from app.repositories.constituent_repo import ConstituentRepo
from app.repositories.etf_repo import EtfRepo
from app.services._etf_helper import resolve_etf


class ConstituentService:
    def __init__(
        self,
        etf_repo: EtfRepo,
        constituent_repo: ConstituentRepo,
        basic_sources: list[EtfBasicSource] | None = None,
    ) -> None:
        self._etf_repo = etf_repo
        self._constituent_repo = constituent_repo
        self._basic_sources = basic_sources or []

    def get_constituents(
        self, code: str, top_n: int | None = None, industry: str | None = None
    ) -> list[ConstituentStock]:
        """FR-05: 取成分股；按权重降序；支持行业过滤与 Top N。"""
        etf = resolve_etf(self._etf_repo, self._basic_sources, code)
        idx = etf.track_index_code
        if not idx:
            return []
        rows = self._constituent_repo.get_constituents(idx)
        if industry:
            rows = [r for r in rows if r.sw_l1 == industry]
        rows = sorted(rows, key=lambda r: (r.weight or 0), reverse=True)
        if top_n:
            rows = rows[:top_n]
        return rows

    def industry_distribution(self, code: str) -> list[IndustryWeight]:
        """FR-05: 按申万一级行业汇总权重。"""
        rows = self.get_constituents(code)
        agg: dict[str, float] = {}
        for r in rows:
            key = r.sw_l1 or "未知"
            agg[key] = agg.get(key, 0.0) + (r.weight or 0.0)
        return [
            IndustryWeight(industry=k, weight=round(v, 4))
            for k, v in sorted(agg.items(), key=lambda kv: kv[1], reverse=True)
        ]
