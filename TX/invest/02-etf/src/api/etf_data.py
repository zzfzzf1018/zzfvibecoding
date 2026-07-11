"""ETF 数据获取层

封装 akshare 的所有数据获取调用。
所有函数都可能抛出异常，由上层 Service 层处理。
"""

import logging
from typing import Optional

import akshare as ak
import pandas as pd

from src.config import REQUEST_TIMEOUT

logger = logging.getLogger(__name__)


def _safe_fetch(func_name: str, fetch_fn, **log_params) -> pd.DataFrame:
    """安全的数据获取包装器，统一超时和日志。

    注意：fetch_fn 必须是无参可调用对象（lambda/函数），
    log_params 仅用于日志记录，不会传递给 fetch_fn。
    """
    logger.info(f"正在获取数据: {func_name}, 参数: {log_params}")
    try:
        df = fetch_fn()
        if df is None:
            logger.warning(f"{func_name} 返回了 None")
            return pd.DataFrame()
        if isinstance(df, pd.DataFrame) and df.empty:
            logger.warning(f"{func_name} 返回了空 DataFrame")
        logger.info(f"{func_name} 获取成功，共 {len(df)} 条记录")
        return df
    except Exception as e:
        logger.error(f"{func_name} 获取失败: {e}", exc_info=True)
        raise RuntimeError(f"获取 {func_name} 数据失败: {e}") from e


# ============================================================
# ETF 列表（搜索用）
# ============================================================

def get_all_etf_list() -> pd.DataFrame:
    """获取全量 ETF 列表（实时行情数据）。

    使用 fund_etf_spot_em 获取东方财富 ETF 实时行情，
    返回列：代码, 名称, 最新价, 涨跌幅, 成交量, 成交额, 最新份额, 总市值 等。
    """
    logger.info("获取全量 ETF 列表（fund_etf_spot_em）...")
    raw = ak.fund_etf_spot_em()
    if raw is None or raw.empty:
        logger.warning("全量 ETF 列表为空")
        return pd.DataFrame()

    # 确保代码列为字符串
    raw["代码"] = raw["代码"].astype(str).str.strip()
    logger.info(f"获取到 {len(raw)} 只 ETF")
    logger.info(f"列名: {list(raw.columns)}")
    return raw


# ============================================================
# ETF 详细信息（费率、规模、成立日期等）
# ============================================================

def get_etf_detailed_info(code: str) -> dict:
    """获取单只 ETF 的详细信息（基金管理人、规模、费率、成立日期等）。

    尝试多个数据源：
    1. fund_individual_basic_info_xq（雪球）- 基金管理人、成立日期、基金规模等
    2. 从全量 ETF 列表中获取基本行情数据

    Returns:
        dict，包含该 ETF 的详细信息字段
    """
    code_str = str(code).strip()
    info = {
        "基金代码": code_str,
    }

    # 数据源1: 雪球基金基本信息
    try:
        logger.info(f"获取 {code_str} 基本信息（雪球）...")
        df = ak.fund_individual_basic_info_xq(symbol=code_str)
        if df is not None and not df.empty:
            # 雪球返回的是两列表（字段名, 字段值），转为 dict
            if len(df.columns) >= 2:
                field_col = df.columns[0]
                value_col = df.columns[1]
                for _, row in df.iterrows():
                    key = str(row[field_col]).strip()
                    val = row[value_col]
                    if pd.notna(val) and str(val).strip():
                        info[key] = val
            logger.info(f"雪球基本信息获取成功，{len(df)} 个字段")
    except Exception as e:
        logger.warning(f"雪球接口获取失败: {e}")

    # 补充：从实时行情数据获取最新价、市值等
    try:
        spot_df = ak.fund_etf_spot_em()
        if spot_df is not None and not spot_df.empty:
            etf_row = spot_df[spot_df["代码"].astype(str).str.strip() == code_str]
            if not etf_row.empty:
                row = etf_row.iloc[0]
                for col in spot_df.columns:
                    val = row[col]
                    if pd.notna(val) and col not in info:
                        info[col] = val
    except Exception as e:
        logger.warning(f"补充实时行情数据失败: {e}")

    return info


# ============================================================
# ETF 历史数据 / K线
# ============================================================

def get_etf_hist_data(symbol: str, period: str = "daily",
                       start_date: Optional[str] = None,
                       end_date: Optional[str] = None) -> pd.DataFrame:
    """获取单只 ETF 历史净值/K线数据。

    Args:
        symbol: ETF 代码，如 "510050"
        period: 周期，"daily"/"weekly"/"monthly"
        start_date: 开始日期 "YYYYMMDD"
        end_date: 结束日期 "YYYYMMDD"
    """
    return _safe_fetch(
        "ETF 历史数据",
        lambda: ak.fund_etf_hist_em(
            symbol=symbol,
            period=period,
            start_date=start_date or "20000101",
            end_date=end_date or "20991231",
            adjust="qfq",  # 前复权
        ),
        symbol=symbol,
        period=period,
    )


# ============================================================
# 持仓数据
# ============================================================

def get_etf_holdings(code: str) -> Optional[pd.DataFrame]:
    """获取 ETF 前十大持仓（成分股）数据。

    尝试多个数据源接口，按优先级依次尝试。

    Returns:
        DataFrame 包含：股票代码、股票名称、持仓占比等
    """
    code_str = str(code)

    # 方法1: fund_portfolio_hold_em（东方财富基金持仓）
    try:
        logger.info(f"获取 {code_str} 持仓数据（fund_portfolio_hold_em）...")
        df = ak.fund_portfolio_hold_em(symbol=code_str, date="2024")
        if df is not None and not df.empty:
            logger.info(f"持仓数据获取成功，{len(df)} 条")
            return df
    except Exception as e:
        logger.warning(f"fund_portfolio_hold_em 失败: {e}")

    # 方法2: fund_etf_fund_daily_position_em
    try:
        logger.info(f"获取 {code_str} 持仓数据（fund_etf_fund_daily_position_em）...")
        df = ak.fund_etf_fund_daily_position_em(symbol=code_str, date="2024")
        if df is not None and not df.empty:
            logger.info(f"持仓数据获取成功，{len(df)} 条")
            return df
    except Exception as e:
        logger.warning(f"fund_etf_fund_daily_position_em 失败: {e}")

    logger.error(f"所有方法均无法获取 {code_str} 的持仓数据")
    return pd.DataFrame()


# ============================================================
# 估值数据（PE/PB）
# ============================================================

def get_index_valuation_hist(index_code: str) -> pd.DataFrame:
    """获取指数历史 PE/PB 估值数据。

    用于计算 PE/PB 历史分位数。

    Args:
        index_code: 指数代码，如 "000300"（沪深300）
    """
    logger.info(f"获取指数 {index_code} 历史 PE/PB 数据...")

    df_pe = pd.DataFrame()
    df_pb = pd.DataFrame()

    try:
        df_pe = ak.index_value_hist_funddb(symbol=index_code, indicator="市盈率")
    except Exception as e:
        logger.warning(f"获取 {index_code} 市盈率数据失败: {e}")

    try:
        df_pb = ak.index_value_hist_funddb(symbol=index_code, indicator="市净率")
    except Exception as e:
        logger.warning(f"获取 {index_code} 市净率数据失败: {e}")

    if df_pe.empty and df_pb.empty:
        logger.warning(f"无法获取指数 {index_code} 的 PE/PB 数据")
        return pd.DataFrame()

    # 合并 PE 和 PB
    result = pd.DataFrame()
    if not df_pe.empty:
        result["date"] = df_pe["日期"]
        result["pe"] = df_pe["指数市盈率"]
    if not df_pb.empty:
        if "date" not in result.columns:
            result["date"] = df_pb["日期"]
        result["pb"] = df_pb["指数市净率"]

    logger.info(f"获取到指数 {index_code} 估值数据，{len(result)} 条")
    return result


def get_etf_dividend_info(code: str) -> Optional[pd.DataFrame]:
    """获取 ETF 分红记录。

    Args:
        code: ETF 基金代码
    """
    code_str = str(code)
    logger.info(f"获取 ETF {code_str} 分红记录...")

    # 尝试 dividend_sina 接口
    try:
        df = ak.fund_etf_dividend_sina(code=code_str)
        if df is not None and not df.empty:
            logger.info(f"分红记录（新浪）获取成功，{len(df)} 条")
            return df
    except Exception as e:
        logger.warning(f"新浪分红接口失败: {e}")

    # 尝试 fund_etf_fund_dividend_em（东方财富）
    try:
        df = ak.fund_etf_fund_dividend_em(symbol=code_str)
        if df is not None and not df.empty:
            logger.info(f"分红记录（东方财富）获取成功，{len(df)} 条")
            return df
    except Exception as e:
        logger.warning(f"东方财富分红接口失败: {e}")

    logger.warning(f"未能获取 ETF {code_str} 的分红数据")
    return pd.DataFrame()
