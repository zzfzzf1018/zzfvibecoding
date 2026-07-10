"""历史分位服务（FR-04）。

算法：percentile_rank = count(v<=x)/N*100；最小样本 250（不足降级窗口）。
详见软件详细设计说明书 §3.3。
"""
from __future__ import annotations

from datetime import date, timedelta

from app.core.config import settings
from app.core.errors import SourceUnavailable
from app.datasource.interfaces import EtfBasicSource
from app.models.schemas import PercentileView
from app.repositories.etf_repo import EtfRepo
from app.repositories.valuation_repo import ValuationRepo
from app.services._etf_helper import resolve_etf

_WINDOWS = ("1y", "3y", "5y", "all")


def _window_start(window: str) -> date | None:
    today = date.today()
    if window == "1y":
        return today - timedelta(days=365)
    if window == "3y":
        return today - timedelta(days=3 * 365)
    if window == "5y":
        return today - timedelta(days=5 * 365)
    return None  # all


def _percentile_rank(series: list[float], x: float) -> float:
    if not series:
        return 0.0
    cnt = sum(1 for v in series if v <= x)
    return round(cnt / len(series) * 100, 1)


def _median(s: list[float]) -> float | None:
    if not s:
        return None
    s = sorted(s)
    n = len(s)
    mid = n // 2
    return s[mid] if n % 2 else round((s[mid - 1] + s[mid]) / 2, 4)


class PercentileService:
    def __init__(
        self,
        etf_repo: EtfRepo,
        valuation_repo: ValuationRepo,
        basic_sources: list[EtfBasicSource] | None = None,
    ) -> None:
        self._etf_repo = etf_repo
        self._valuation_repo = valuation_repo
        self._basic_sources = basic_sources or []
        self._min_samples = settings.min_samples

    def get_percentile(self, code: str, window: str = "5y") -> PercentileView:
        """FR-04: 计算 PE/PB 在指定窗口的历史分位；样本不足自动降级并标记。"""
        if window not in _WINDOWS:
            raise ValueError(f"window 必须为 {_WINDOWS}")

        etf = resolve_etf(self._etf_repo, self._basic_sources, code)
        idx = etf.track_index_code
        if not idx:
            return PercentileView(window=window, sample_count=0, degraded=False)

        hist = self._valuation_repo.get_history(idx, start=_window_start(window))
        degraded = False
        if len(hist) < self._min_samples:
            hist = self._valuation_repo.get_history(idx)  # 退回全历史
            degraded = True

        if not hist:
            raise SourceUnavailable(f"指数 {idx} 暂无历史估值数据")

        cur = self._valuation_repo.get_latest(idx)
        pe_series = [h.pe_ttm for h in hist if h.pe_ttm is not None]
        pb_series = [h.pb for h in hist if h.pb is not None]
        pe_pct = (
            _percentile_rank(pe_series, cur.pe_ttm)
            if (cur and cur.pe_ttm is not None)
            else None
        )
        pb_pct = (
            _percentile_rank(pb_series, cur.pb)
            if (cur and cur.pb is not None)
            else None
        )
        return PercentileView(
            window=window,
            sample_count=len(hist),
            pe_percentile=pe_pct,
            pb_percentile=pb_pct,
            pe_max=max(pe_series) if pe_series else None,
            pe_min=min(pe_series) if pe_series else None,
            pe_median=_median(pe_series),
            pb_max=max(pb_series) if pb_series else None,
            pb_min=min(pb_series) if pb_series else None,
            pb_median=_median(pb_series),
            degraded=degraded,
        )
