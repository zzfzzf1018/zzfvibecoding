"""检索服务（FR-01/FR-02）。

依赖 repositories（不依赖任何具体数据源）。详见软件详细设计说明书 §3.1。
"""
from __future__ import annotations

from app.datasource.interfaces import EtfBasicSource
from app.models.schemas import EtfBasic, ETFSearchResult
from app.repositories.etf_repo import EtfRepo
from app.services._etf_helper import resolve_etf


class SearchService:
    def __init__(
        self,
        etf_repo: EtfRepo,
        basic_sources: list[EtfBasicSource] | None = None,
    ) -> None:
        self._etf_repo = etf_repo
        self._basic_sources = basic_sources or []

    def search(self, keyword: str, page: int = 1, page_size: int = 50) -> list[ETFSearchResult]:
        """FR-01: 编号精确/名称模糊检索；空关键字返回空；分页。

        编号检索若库中无命中，则经 basic_sources 单条解析补全（无需先全量 -Seed 也可查）；
        名称检索仅命中本地库（需先 -Seed 落库）。
        """
        if not keyword:
            return []
        if keyword.isdigit():
            results = self._etf_repo.search_by_code(keyword, page, page_size)
            if results or not self._basic_sources:
                return results
            try:
                etf = resolve_etf(self._etf_repo, self._basic_sources, keyword)
            except Exception:  # noqa: BLE001 - 解析失败则回退为空结果
                return results
            return [
                ETFSearchResult(
                    code=etf.code,
                    name=etf.name,
                    type=etf.type,
                    track_index=etf.track_index,
                    track_index_code=etf.track_index_code,
                    latest_price=None,
                    update_time=etf.update_time,
                )
            ]
        return self._etf_repo.search_by_name(keyword, page, page_size)

    def get_basic(self, code: str) -> EtfBasic:
        """FR-02: 取 ETF 基本信息；缺失或缺少跟踪指数时经 basic_sources 补全落库。"""
        return resolve_etf(self._etf_repo, self._basic_sources, code)
