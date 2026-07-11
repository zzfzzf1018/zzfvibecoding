"""历史分位服务（FR-04）。

算法：percentile_rank = count(v<=x)/N*100；最小样本 250（不足降级窗口）。
详见软件详细设计说明书 §3.3。
"""
from __future__ import annotations

from datetime import date, timedelta

from app.core.config import settings
from app.core.errors import SourceUnavailable
from app.core.logging import logger
from app.datasource.interfaces import EtfBasicSource, ValuationSource
from app.models.schemas import (
    MonthlyPercentilePoint,
    MonthlyPercentileView,
    PercentileView,
)
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


def _recent_months(today: date, months: int) -> list[tuple[int, int]]:
    """返回最近 `months` 个自然月（含当月）的 (year, month)，按时间升序。"""
    result: list[tuple[int, int]] = []
    year, month = today.year, today.month
    for _ in range(months):
        result.append((year, month))
        month -= 1
        if month == 0:
            month = 12
            year -= 1
    result.reverse()
    return result


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
        valuation_sources: list[ValuationSource] | None = None,
    ) -> None:
        self._etf_repo = etf_repo
        self._valuation_repo = valuation_repo
        self._basic_sources = basic_sources or []
        self._valuation_sources = valuation_sources or []
        self._min_samples = settings.min_samples

    def _ensure_history(self, idx: str, index_name: str | None, window_years: int) -> None:
        """历史不足时按需从 valuation_sources 拉取全量并落库（保证分位实际出数）。"""
        existing = self._valuation_repo.get_history(idx)
        # 已积累足够覆盖窗口（约 window_years*240 个交易日）则跳过，避免重复拉取
        if len(existing) >= window_years * 200:
            return
        end = date.today()
        start = end - timedelta(days=window_years * 400)
        for src in self._valuation_sources:
            try:
                pts = src.get_history(idx, start=start, end=end, index_name=index_name)
            except Exception as exc:  # noqa: BLE001
                continue
            if pts:
                for p in pts:
                    self._valuation_repo.append_history(idx, p)
                logger.info("历史分位：自 %s 补充 %s 条估值历史", src.name, len(pts))
                break

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

    def get_monthly_percentile(
        self, code: str, months: int = 12, window_years: int = 5
    ) -> MonthlyPercentileView:
        """FR-04（按月）：返回最近 `months` 个月、每月的 PE/PB 历史分位序列。

        口径（面向普通用户，与主流基金 App 一致）：某月分位 = 该月「月末」PE/PB
        在「该月末往前 `window_years` 年」滚动窗口内的分位；上市/数据不足
        `window_years` 年则用现有全部历史兜底并置 `degraded=True`；该月无数据则分位为 None。
        """
        etf = resolve_etf(self._etf_repo, self._basic_sources, code)
        idx = etf.track_index_code or etf.track_index
        if not idx:
            return MonthlyPercentileView(
                window_years=window_years, months=months, sample_count=0, series=[]
            )

        # 历史不足时按需拉取并落库（保证分位实际出数；可选主流宽基指数之外优雅降级）
        self._ensure_history(idx, etf.track_index, window_years)

        hist = sorted(self._valuation_repo.get_history(idx), key=lambda h: h.date)
        if not hist:
            # 无历史属正常业务（指数不支持历史源/尚未采集），返回空序列由前端提示，而非抛错
            return MonthlyPercentileView(
                window_years=window_years, months=months, sample_count=0, series=[]
            )

        window_days = window_years * 365
        target_months = _recent_months(date.today(), months)  # 升序 [(year, month), ...]

        series: list[MonthlyPercentilePoint] = []
        for year, month in target_months:
            month_recs = [h for h in hist if h.date.year == year and h.date.month == month]
            label = f"{year:04d}-{month:02d}"
            if not month_recs:
                series.append(MonthlyPercentilePoint(month=label))
                continue

            month_end = max(month_recs, key=lambda h: h.date)
            cutoff = month_end.date - timedelta(days=window_days)
            window_recs = [h for h in hist if cutoff < h.date <= month_end.date]
            pe_win = [h.pe_ttm for h in window_recs if h.pe_ttm is not None]
            pb_win = [h.pb for h in window_recs if h.pb is not None]
            pe_pct = (
                _percentile_rank(pe_win, month_end.pe_ttm)
                if (month_end.pe_ttm is not None and pe_win)
                else None
            )
            pb_pct = (
                _percentile_rank(pb_win, month_end.pb)
                if (month_end.pb is not None and pb_win)
                else None
            )
            series.append(
                MonthlyPercentilePoint(
                    month=label,
                    pe=month_end.pe_ttm,
                    pe_percentile=pe_pct,
                    pb=month_end.pb,
                    pb_percentile=pb_pct,
                )
            )

        # 可用历史起点晚于「今天往前 window_years 年」→ 基准窗口不足，标记降级
        degraded = hist[0].date > (date.today() - timedelta(days=window_days))
        return MonthlyPercentileView(
            window_years=window_years,
            months=months,
            sample_count=len(hist),
            degraded=degraded,
            series=series,
        )
