"""成分股仓储（骨架/未实现）。

详见 SRS DR-04、软件详细设计说明书 §1。
"""
from __future__ import annotations

from typing import Callable

from sqlalchemy.orm import Session

from app.models.schemas import ConstituentStock


class ConstituentRepo:
    def __init__(self, session_factory: Callable[[], Session]) -> None:
        self._sf = session_factory

    def replace_constituents(self, index_code: str, rows: list[ConstituentStock]) -> None:
        # TODO(DR-04): 按 index_code 全量替换成分股
        raise NotImplementedError("ConstituentRepo.replace_constituents 待实现")

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        # TODO(FR-05): 取成分股
        raise NotImplementedError("ConstituentRepo.get_constituents 待实现")


__all__ = ["ConstituentRepo"]
