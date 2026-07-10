"""历史分位服务（FR-04，骨架/未实现）。

算法：percentile_rank = count(v<=x)/N*100；最小样本 250（不足降级窗口）。
详见软件详细设计说明书 §3.3。
"""
from __future__ import annotations

from app.core.config import settings
from app.models.schemas import PercentileView
from app.repositories.etf_repo import EtfRepo
from app.repositories.valuation_repo import ValuationRepo

_WINDOWS = ("1y", "3y", "5y", "all")


class PercentileService:
    def __init__(self, etf_repo: EtfRepo, valuation_repo: ValuationRepo) -> None:
        self._etf_repo = etf_repo
        self._valuation_repo = valuation_repo
        self._min_samples = settings.min_samples

    def get_percentile(self, code: str, window: str = "5y") -> PercentileView:
        """FR-04: 计算 PE/PB 在指定窗口的历史分位；样本不足自动降级并标记。"""
        if window not in _WINDOWS:
            raise ValueError(f"window 必须为 {_WINDOWS}")
        raise NotImplementedError("PercentileService.get_percentile 待实现")


__all__ = ["PercentileService"]
