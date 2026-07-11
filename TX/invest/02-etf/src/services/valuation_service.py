"""估值计算服务

负责 PE/PB 历史分位数计算等业务逻辑。
"""

import logging
from datetime import datetime
from typing import Optional

import pandas as pd

from src.config import PERCENTILE_WINDOWS

logger = logging.getLogger(__name__)


def calc_percentile(series: pd.Series, current_value: float) -> float:
    """计算当前值在历史序列中的分位数。
    
    Args:
        series: 历史数据序列
        current_value: 当前值
    
    Returns:
        分位数（0-100），表示当前值低于历史百分之多少的数据
        值越低说明当前估值越低/越便宜
    """
    if series.empty or pd.isna(current_value):
        return float("nan")
    clean = series.dropna()
    if clean.empty:
        return float("nan")
    percentile = (clean < current_value).sum() / len(clean) * 100
    return round(percentile, 2)


def calc_percentiles_for_windows(
    df: pd.DataFrame,
    current_pe: Optional[float] = None,
    current_pb: Optional[float] = None,
) -> dict:
    """计算 PE/PB 在多个历史窗口的分位数。
    
    Args:
        df: 包含 date, pe, pb 三列的数据
        current_pe: 当前 PE
        current_pb: 当前 PB
    
    Returns:
        {
            "pe": {"3年": 45.2, "5年": 60.1, ...},
            "pb": {"3年": 30.5, "5年": 42.3, ...},
        }
    """
    if df is None or df.empty:
        logger.warning("估值数据为空，无法计算分位数")
        return {"pe": {}, "pb": {}}

    if "date" not in df.columns:
        logger.error("估值数据缺少 date 列")
        return {"pe": {}, "pb": {}}

    # 确保 date 是日期类型
    df = df.copy()
    df["date"] = pd.to_datetime(df["date"], errors="coerce")
    df = df.sort_values("date")

    latest_date = df["date"].max()
    if pd.isna(latest_date):
        logger.error("无法确定最新数据日期")
        return {"pe": {}, "pb": {}}

    result = {"pe": {}, "pb": {}}

    for label, years in PERCENTILE_WINDOWS.items():
        cutoff_date = latest_date - pd.DateOffset(years=years)
        window_df = df[df["date"] >= cutoff_date]

        if "pe" in df.columns and current_pe is not None:
            result["pe"][label] = calc_percentile(window_df["pe"], current_pe)

        if "pb" in df.columns and current_pb is not None:
            result["pb"][label] = calc_percentile(window_df["pb"], current_pb)

    logger.info(f"PE 分位数: {result['pe']}")
    logger.info(f"PB 分位数: {result['pb']}")
    return result


def get_current_pe_pb_from_latest(df: pd.DataFrame) -> dict:
    """从估值数据中提取最新 PE 和 PB。
    
    Returns:
        {"pe": 12.5, "pb": 1.3, "date": "2024-01-15"}
    """
    if df is None or df.empty:
        return {"pe": None, "pb": None, "date": None}

    if "date" not in df.columns:
        return {"pe": None, "pb": None, "date": None}

    df = df.copy()
    df["date"] = pd.to_datetime(df["date"], errors="coerce")
    df = df.sort_values("date")

    latest = df.iloc[-1]
    result = {
        "pe": float(latest.get("pe", None)) if "pe" in df.columns and pd.notna(latest.get("pe")) else None,
        "pb": float(latest.get("pb", None)) if "pb" in df.columns and pd.notna(latest.get("pb")) else None,
        "date": str(latest["date"].date()) if pd.notna(latest.get("date")) else None,
    }
    return result
