"""AkShare 数据源适配器（主源，骨架/未实现）。

实现 datasource.interfaces 的端口。具体取数逻辑待后续 AI 按
docs/数据源调研.md 填充（如 ak.fund_etf_category_sina / stock_index_value_name_em）。
本文件除端口实现外，严禁被 services/api 直接 import（见 docs/AI开发约束.md §5）。
"""
from __future__ import annotations

from datetime import date

from app.models.schemas import (
    ConstituentStock,
    EtfBasic,
    IndexValuation,
    IndexValuationPoint,
)


class AkShareEtfBasicSource:
    name = "akshare"

    def list_etfs(self) -> list[EtfBasic]:
        # TODO(SRS FR-02): 调用 ak.fund_etf_category_sina 等拉取 ETF 列表
        raise NotImplementedError("AkShareEtfBasicSource.list_etfs 待实现")

    def get_etf(self, code: str) -> EtfBasic | None:
        # TODO(SRS FR-02): 按 code 取单只 ETF 基本信息
        raise NotImplementedError("AkShareEtfBasicSource.get_etf 待实现")


class AkShareValuationSource:
    name = "akshare"

    def get_latest(self, index_code: str) -> IndexValuation | None:
        # TODO(SRS FR-03): 调用 ak.stock_index_value_name_em 取指数估值
        raise NotImplementedError("AkShareValuationSource.get_latest 待实现")

    def get_history(
        self, index_code: str, start: date, end: date
    ) -> list[IndexValuationPoint]:
        # TODO(SRS FR-06): 补足历史估值序列
        raise NotImplementedError("AkShareValuationSource.get_history 待实现")


class AkShareConstituentSource:
    name = "akshare"

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        # TODO(SRS FR-05): 调用 ak.index_stock_cons_csindex 取成分股
        raise NotImplementedError("AkShareConstituentSource.get_constituents 待实现")


__all__ = [
    "AkShareEtfBasicSource",
    "AkShareValuationSource",
    "AkShareConstituentSource",
]
