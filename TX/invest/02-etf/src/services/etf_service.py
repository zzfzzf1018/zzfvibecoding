"""ETF 数据聚合服务

组合多个 API 调用，为 UI 层提供统一的数据接口。
"""

import logging
import re
from typing import Optional

import pandas as pd
import streamlit as st

from src.api.etf_data import (
    get_all_etf_list,
    get_etf_detailed_info,
    get_etf_hist_data,
    get_etf_holdings,
    get_index_valuation_hist,
    get_etf_dividend_info,
)
from src.services.valuation_service import (
    calc_percentiles_for_windows,
    get_current_pe_pb_from_latest,
)

logger = logging.getLogger(__name__)

# ============================================================
# 常见指数名称 → 指数代码映射
# ============================================================
INDEX_NAME_TO_CODE: dict[str, str] = {
    "沪深300": "000300",
    "上证50": "000016",
    "中证500": "000905",
    "中证1000": "000852",
    "中证2000": "932000",
    "中证A50": "930050",
    "中证A500": "000510",
    "中证A100": "000903",
    "创业板": "399006",
    "创业板指": "399006",
    "创业板50": "399673",
    "科创50": "000688",
    "科创100": "000698",
    "科创创业50": "931643",
    "科创综指": "000680",
    "深证100": "399330",
    "深证成指": "399001",
    "中证红利": "000922",
    "红利低波": "H30269",
    "中证银行": "399986",
    "中证证券": "399975",
    "证券公司": "399975",
    "中证军工": "399967",
    "中证白酒": "399997",
    "中证医疗": "399989",
    "中证医药": "000933",
    "中证消费": "000932",
    "中证新能源": "399808",
    "中证新能源车": "930997",
    "中证半导体": "990001",
    "国证半导体": "980017",
    "中华半导体": "990001",
    "国证芯片": "980017",
    "中证全指半导体": "H30184",
    "国证2000": "399303",
    "中证全指": "000985",
    "恒生科技": "HSTECH",
    "恒生互联网": "HSIII",
    "恒生医疗": "HSHCI",
    "恒生指数": "HSI",
    "恒生国企": "HSCEI",
    "恒生红利": "HSHDY",
    "中证煤炭": "399998",
    "中证有色": "000823",
    "中证钢铁": "930606",
    "中证电力": "399986",
    "中证畜牧": "930707",
    "中证农业": "000949",
    "中证传媒": "399971",
    "中证游戏": "930901",
    "中证计算机": "930651",
    "中证软件": "930601",
    "中证大数据": "930902",
    "中证云计算": "930851",
    "中证人工智能": "930713",
    "中证通信": "000998",
    "中证5G": "931079",
    "中证光伏": "931151",
    "中证稀土": "930598",
    "中证基建": "930608",
    "中证地产": "931775",
    "中证旅游": "930633",
    "中证物流": "930716",
    "中证汽车": "930607",
    "中证家电": "930697",
    "中证环保": "000827",
    "中证国防": "399973",
    "中证央企": "000926",
    "中证国企": "000955",
    "中证红利质量": "931157",
    "标普500": "SPX",
    "纳斯达克100": "NDX",
    "纳指100": "NDX",
    "纳指科技": "NDXT",
    "道琼斯": "DJI",
    "日经225": "N225",
    "日经": "N225",
    "德国DAX": "GDAXI",
    "法国CAC40": "FCHI",
    "黄金": "AU9999",
    "上海金": "SHAU",
    "SGE黄金": "AU9999",
    "中证短融": "H11014",
    "中证转债": "000832",
    "同业存单AAA": "931059",
    "中债-新综合": "CBA00101",
    "中证REITs": "932045",
    "深红利": "399324",
    "上证红利": "000015",
    "中证央企红利": "000825",
}


def _extract_index_name_from_etf_name(etf_name: str) -> Optional[str]:
    """从 ETF 名称中提取跟踪的指数名称。

    例如:
    "华泰柏瑞沪深300ETF" → "沪深300"
    "易方达创业板ETF" → "创业板指"
    "华夏上证50ETF" → "上证50"
    """
    # 去掉常见前缀（基金公司名）
    cleaned = etf_name
    # 去掉末尾的 "ETF"
    cleaned = re.sub(r"ETF$", "", cleaned, flags=re.IGNORECASE)
    # 常见公司名模式
    prefixes = [
        "华泰柏瑞", "易方达", "华夏", "南方", "嘉实", "博时", "广发",
        "富国", "招商", "天弘", "国泰", "华安", "工银瑞信", "工银",
        "鹏华", "景顺长城", "银华", "万家", "汇添富", "海富通",
        "国联安", "大成", "建信", "平安", "华宝", "兴证全球",
    ]
    for prefix in prefixes:
        if cleaned.startswith(prefix):
            candidate = cleaned[len(prefix):]
            if candidate:
                cleaned = candidate
                break

    cleaned = cleaned.strip()
    if cleaned:
        logger.info(f"从 ETF 名称 '{etf_name}' 提取指数名: '{cleaned}'")
        return cleaned
    return None


def _resolve_index_code(etf_name: str, basic_info: dict) -> Optional[str]:
    """解析 ETF 的跟踪指数代码。

    优先级：
    1. 从基本信息中查找 跟踪标的/业绩基准 等字段
    2. 从 ETF 名称映射已知指数
    """
    # 策略1: 从雪球基本信息中找
    search_keys = [
        "跟踪标的", "跟踪指数", "业绩比较基准", "业绩基准",
        "标的指数", "基准指数", "跟踪标的指数代码",
        "指数代码", "标的代码", "跟踪的指数", "比较基准",
    ]
    for key, val in basic_info.items():
        val_str = str(val).strip()
        if not val_str or val_str in ("--", "nan", "None"):
            continue

        # 如果 key 直接包含上述关键词
        for sk in search_keys:
            if sk in str(key):
                # 尝试提取数字代码
                nums = re.findall(r"\d{6}", val_str)
                if nums:
                    logger.info(f"从基本信息字段 '{key}={val_str}' 提取指数代码: {nums[0]}")
                    return nums[0]
                # 可能是指数名称
                if len(val_str) <= 30:
                    logger.info(f"从基本信息字段 '{key}={val_str}' 识别指数名称")
                    return val_str

        # 如果 value 包含 "指数" 关键词
        if "指数" in val_str:
            nums = re.findall(r"\d{6}", val_str)
            if nums:
                return nums[0]

    # 策略2: 从 ETF 名称推断
    index_name = _extract_index_name_from_etf_name(etf_name)
    if index_name:
        # 先尝试精确匹配映射表
        if index_name in INDEX_NAME_TO_CODE:
            logger.info(f"指数名称 '{index_name}' 映射到代码: {INDEX_NAME_TO_CODE[index_name]}")
            return INDEX_NAME_TO_CODE[index_name]

        # 尝试模糊匹配映射表
        for known_name, known_code in INDEX_NAME_TO_CODE.items():
            if known_name in index_name or index_name in known_name:
                logger.info(f"指数名称 '{index_name}' 模糊匹配 '{known_name}' → {known_code}")
                return known_code

    return None


@st.cache_data(ttl=3600)
def load_etf_list() -> pd.DataFrame:
    """加载全量 ETF 列表（带缓存）。"""
    logger.info("加载全量 ETF 列表...")
    df = get_all_etf_list()
    if df.empty:
        logger.warning("ETF 列表为空")
        st.warning("⚠️ 无法加载 ETF 列表，请检查网络连接后刷新页面。")
    return df


def search_etf_by_keyword(keyword: str) -> pd.DataFrame:
    """根据关键字（代码或名称）模糊搜索 ETF。"""
    df = load_etf_list()
    if df.empty:
        return df

    keyword = keyword.strip()
    if not keyword:
        return df.head(20)

    if "代码" not in df.columns or "名称" not in df.columns:
        logger.error(f"ETF 列表列名不匹配，实际列: {list(df.columns)}")
        st.error(f"ETF 数据列名异常，缺少 代码/名称 列。实际列: {list(df.columns)}")
        return pd.DataFrame()

    code_col = "代码"
    name_col = "名称"

    exact_code = df[df[code_col].astype(str).str.strip() == keyword]
    fuzzy_name = df[df[name_col].astype(str).str.contains(keyword, case=False, na=False)]
    fuzzy_code = df[df[code_col].astype(str).str.contains(keyword, case=False, na=False)]

    result = pd.concat([exact_code, fuzzy_name, fuzzy_code]).drop_duplicates(subset=[code_col])

    if len(result) == 0:
        logger.info(f"搜索 '{keyword}' 无结果")
        st.info(f"未找到与 '{keyword}' 相关的 ETF")
    else:
        logger.info(f"搜索 '{keyword}' 找到 {len(result)} 条结果")

    return result


def get_etf_detail(code: str) -> dict:
    """获取单只 ETF 的完整详情。"""
    errors = []
    result = {}
    code = str(code).strip()

    # 获取 ETF 名称（从实时行情列表）
    etf_name = ""
    try:
        spot_df = load_etf_list()
        if not spot_df.empty:
            etf_row = spot_df[spot_df["代码"].astype(str).str.strip() == code]
            if not etf_row.empty:
                etf_name = str(etf_row.iloc[0]["名称"])
    except Exception as e:
        logger.warning(f"获取 ETF 名称失败: {e}")

    # 1. 基础信息
    try:
        basic_info = get_etf_detailed_info(code)
        if not basic_info or len(basic_info) <= 1:
            errors.append(f"未获取到 {code} 的详细信息")
        # 确保基本信息中包含代码和名称
        if "基金代码" not in basic_info:
            basic_info["基金代码"] = code
        if etf_name and "基金简称" not in basic_info and "名称" not in basic_info:
            basic_info["基金简称"] = etf_name
        result["basic_info"] = basic_info
    except Exception as e:
        logger.error(f"获取详细信息失败: {e}", exc_info=True)
        errors.append(f"获取详细信息失败: {e}")
        result["basic_info"] = {}

    # 2. K线数据
    try:
        result["kline_data"] = get_etf_hist_data(symbol=code)
    except Exception as e:
        logger.error(f"获取K线数据失败: {e}", exc_info=True)
        errors.append(f"获取K线数据失败: {e}")
        result["kline_data"] = pd.DataFrame()

    # 3. 持仓数据
    try:
        result["holdings"] = get_etf_holdings(code)
    except Exception as e:
        logger.error(f"获取持仓数据失败: {e}", exc_info=True)
        errors.append(f"获取持仓数据失败: {e}")
        result["holdings"] = pd.DataFrame()

    # 4. 估值数据（PE/PB）
    try:
        basic_info = result.get("basic_info", {})
        index_code = _resolve_index_code(etf_name or "", basic_info)

        if index_code:
            try:
                hist_valuation = get_index_valuation_hist(index_code)
            except Exception as e:
                logger.warning(f"获取指数 {index_code} 估值数据失败: {e}")
                hist_valuation = pd.DataFrame()
                errors.append(f"获取指数 {index_code} 的 PE/PB 历史数据失败: {e}")

            if hist_valuation.empty:
                errors.append(f"指数 {index_code} 无 PE/PB 历史数据")
                result["valuation"] = {
                    "current": {"pe": None, "pb": None, "date": None},
                    "percentiles": {"pe": {}, "pb": {}},
                    "hist_data": pd.DataFrame(),
                    "index_code": index_code,
                }
            else:
                current_vals = get_current_pe_pb_from_latest(hist_valuation)
                percentiles = calc_percentiles_for_windows(
                    hist_valuation,
                    current_pe=current_vals.get("pe"),
                    current_pb=current_vals.get("pb"),
                )
                result["valuation"] = {
                    "current": current_vals,
                    "percentiles": percentiles,
                    "hist_data": hist_valuation,
                    "index_code": index_code,
                }
        else:
            errors.append(f"未找到该 ETF('{etf_name}')的跟踪指数，无法获取 PE/PB 估值数据")
            result["valuation"] = {
                "current": {"pe": None, "pb": None, "date": None},
                "percentiles": {"pe": {}, "pb": {}},
                "hist_data": pd.DataFrame(),
                "index_code": None,
            }
    except Exception as e:
        logger.error(f"获取估值数据失败: {e}", exc_info=True)
        errors.append(f"获取估值数据失败: {e}")
        result["valuation"] = {
            "current": {"pe": None, "pb": None, "date": None},
            "percentiles": {"pe": {}, "pb": {}},
            "hist_data": pd.DataFrame(),
        }

    # 5. 分红数据
    try:
        result["dividends"] = get_etf_dividend_info(code)
    except Exception as e:
        logger.error(f"获取分红数据失败: {e}", exc_info=True)
        errors.append(f"获取分红数据失败: {e}")
        result["dividends"] = pd.DataFrame()

    result["errors"] = errors
    return result
