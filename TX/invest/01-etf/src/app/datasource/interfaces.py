"""数据源端口（抽象）。

services 层只依赖本文件定义的 Protocol，不依赖任何具体实现。
具体实现（akshare/csindex/em）经 core/di.py 注入，满足依赖倒置（DIP）。
详见 docs/架构设计文档.md §4、软件详细设计说明书 §2.1。
"""
from __future__ import annotations

from datetime import date
from typing import Protocol, runtime_checkable

from app.models.schemas import (
    ConstituentStock,
    EtfBasic,
    IndexValuation,
    IndexValuationPoint,
)


@runtime_checkable
class EtfBasicSource(Protocol):
    name: str

    def list_etfs(self) -> list[EtfBasic]: ...

    def get_etf(self, code: str) -> EtfBasic | None: ...


@runtime_checkable
class ValuationSource(Protocol):
    name: str

    def get_latest(
        self, index_code: str, index_name: str | None = None
    ) -> IndexValuation | None: ...

    def get_history(
        self,
        index_code: str,
        start: date,
        end: date,
        index_name: str | None = None,
    ) -> list[IndexValuationPoint]: ...


@runtime_checkable
class ConstituentSource(Protocol):
    name: str

    def get_constituents(self, index_code: str) -> list[ConstituentStock]: ...


__all__ = ["EtfBasicSource", "ValuationSource", "ConstituentSource"]
