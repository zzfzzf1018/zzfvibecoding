"""依赖注入容器（core/di）。

唯一装配具体数据源/仓储/服务之处。新增数据源只需在此注册，services/api 零改动。
满足依赖倒置（DIP）与松耦合（见 docs/架构设计文档.md §4、AI开发约束 §5）。
"""
from __future__ import annotations

from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.core.config import settings
from app.datasource.akshare_src import (
    AkShareConstituentSource,
    AkShareEtfBasicSource,
    AkShareValuationSource,
)
from app.datasource.csindex_src import (
    CsIndexConstituentSource,
    CsIndexValuationSource,
)
from app.datasource.em_src import EmValuationSource
from app.repositories.constituent_repo import ConstituentRepo
from app.repositories.etf_repo import EtfRepo
from app.repositories.valuation_repo import ValuationRepo
from app.services.constituent_service import ConstituentService
from app.services.percentile_service import PercentileService
from app.services.search_service import SearchService
from app.services.valuation_service import ValuationService


class Container:
    def __init__(self) -> None:
        self.engine = create_engine(settings.database_url, future=True)
        self.Session = sessionmaker(bind=self.engine, future=True)

        # 仓储
        self.etf_repo = EtfRepo(self.Session)
        self.valuation_repo = ValuationRepo(self.Session)
        self.constituent_repo = ConstituentRepo(self.Session)

        # 数据源（主 -> 兜底 顺序）
        self.basic_sources = [AkShareEtfBasicSource()]
        self.valuation_sources = [
            AkShareValuationSource(),
            CsIndexValuationSource(),
            EmValuationSource(),
        ]
        self.constituent_sources = [
            AkShareConstituentSource(),
            CsIndexConstituentSource(),
        ]

        # 服务（仅依赖接口/仓储）
        self.search_service = SearchService(self.etf_repo)
        self.valuation_service = ValuationService(
            self.etf_repo, self.valuation_repo, self.valuation_sources
        )
        self.percentile_service = PercentileService(self.etf_repo, self.valuation_repo)
        self.constituent_service = ConstituentService(
            self.etf_repo, self.constituent_repo
        )


__all__ = ["Container"]
