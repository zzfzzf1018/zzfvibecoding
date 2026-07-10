"""API 依赖注入（提供 services 实例）。

通过单例 Container 获取；保持 api 层不直接构造业务对象。
"""
from __future__ import annotations

from app.core.di import Container
from app.services.constituent_service import ConstituentService
from app.services.percentile_service import PercentileService
from app.services.search_service import SearchService
from app.services.valuation_service import ValuationService

_container = Container()


def get_search_service() -> SearchService:
    return _container.search_service


def get_valuation_service() -> ValuationService:
    return _container.valuation_service


def get_percentile_service() -> PercentileService:
    return _container.percentile_service


def get_constituent_service() -> ConstituentService:
    return _container.constituent_service


__all__ = [
    "get_search_service",
    "get_valuation_service",
    "get_percentile_service",
    "get_constituent_service",
]
