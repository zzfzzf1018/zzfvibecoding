"""中国股市 ETF 查询工具 - 主入口

Streamlit 应用主文件，负责 UI 层逻辑。
"""

import logging
import sys
from pathlib import Path

# 将项目根目录加入 Python 路径，确保 src 包可导入
PROJECT_ROOT = Path(__file__).parent
sys.path.insert(0, str(PROJECT_ROOT))

import streamlit as st
import pandas as pd

from src.config import PAGE_TITLE, PAGE_LAYOUT, LOG_FORMAT, LOG_LEVEL
from src.services.etf_service import search_etf_by_keyword, get_etf_detail
from src.ui.components import (
    render_kline_chart,
    render_basic_info,
    render_holdings,
    render_valuation,
    render_dividends,
)

# ---------- 日志配置 ----------
logging.basicConfig(
    level=getattr(logging, LOG_LEVEL),
    format=LOG_FORMAT,
    stream=sys.stdout,
)
logger = logging.getLogger(__name__)

# ---------- 页面配置 ----------
st.set_page_config(
    page_title=PAGE_TITLE,
    page_icon="📈",
    layout=PAGE_LAYOUT,
    initial_sidebar_state="expanded",
)


def init_session_state() -> None:
    """初始化 session state。"""
    defaults = {
        "search_results": pd.DataFrame(),
        "selected_etf_code": None,
        "selected_etf_name": None,
        "etf_detail": None,
        "search_triggered": False,
    }
    for key, val in defaults.items():
        if key not in st.session_state:
            st.session_state[key] = val


def handle_search(keyword: str) -> None:
    """处理搜索逻辑。"""
    if not keyword.strip():
        st.warning("请输入 ETF 代码或名称关键字")
        return

    with st.spinner(f"正在搜索 '{keyword}' ..."):
        try:
            results = search_etf_by_keyword(keyword)
            st.session_state.search_results = results
            st.session_state.search_triggered = True

            if results.empty:
                st.session_state.selected_etf_code = None
                st.session_state.etf_detail = None
            logger.info(f"搜索 '{keyword}' 完成，找到 {len(results)} 条结果")
        except Exception as e:
            logger.error(f"搜索失败: {e}", exc_info=True)
            st.error(f"搜索失败: {e}")
            st.session_state.search_results = pd.DataFrame()
            st.session_state.search_triggered = False


def handle_select_etf(code: str, name: str) -> None:
    """处理 ETF 选择逻辑。"""
    st.session_state.selected_etf_code = code
    st.session_state.selected_etf_name = name

    with st.spinner(f"正在加载 {name}({code}) 的详细数据，请稍候..."):
        try:
            detail = get_etf_detail(code)
            st.session_state.etf_detail = detail
            logger.info(f"ETF {code} 详情加载完成")

            # 显示错误信息（不隐藏）
            errors = detail.get("errors", [])
            if errors:
                for err in errors:
                    st.warning(f"⚠️ {err}")
        except Exception as e:
            logger.error(f"获取 ETF 详情失败: {e}", exc_info=True)
            st.error(f"获取 ETF 详情失败: {e}")
            st.session_state.etf_detail = None


# ====================================================================
# 主界面
# ====================================================================

def main():
    init_session_state()

    # -------- 侧边栏：搜索区 --------
    with st.sidebar:
        st.title("🔍 ETF 查询")
        st.markdown("---")

        search_keyword = st.text_input(
            "输入 ETF 代码或名称",
            placeholder="例如: 510050 或 沪深300",
            key="search_input",
        )

        col1, col2 = st.columns(2)
        with col1:
            if st.button("搜索", type="primary", use_container_width=True):
                if search_keyword:
                    handle_search(search_keyword)
        with col2:
            if st.button("清空", use_container_width=True):
                st.session_state.search_results = pd.DataFrame()
                st.session_state.selected_etf_code = None
                st.session_state.etf_detail = None
                st.session_state.search_triggered = False
                st.rerun()

        # 显示搜索结果列表
        results = st.session_state.get("search_results")
        if results is not None and not results.empty:
            st.markdown("---")
            st.subheader(f"搜索结果 ({len(results)})")

            code_col = None
            name_col = None
            for col in results.columns:
                if "代码" in str(col):
                    code_col = col
                if "简称" in str(col) or "名称" in str(col):
                    name_col = col

            if code_col and name_col:
                for _, row in results.iterrows():
                    code = str(row[code_col])
                    name = str(row[name_col])
                    btn_label = f"{code} {name}"
                    if st.button(btn_label, key=f"etf_{code}", use_container_width=True):
                        handle_select_etf(code, name)
                        st.rerun()

    # -------- 主内容区 --------
    st.title(f"📈 {PAGE_TITLE}")

    if st.session_state.get("etf_detail") is None:
        # 首页引导
        st.markdown("""
        ### 功能说明

        本工具支持查询中国股市 ETF 的全面信息：

        | 功能模块 | 内容 |
        |---------|------|
        | 📋 **基本信息** | 发行方、规模、费率、成立日期等 |
        | 📊 **K线图** | 交互式 K线走势图（含成交量） |
        | 🏢 **成分股** | 前十大持仓及占比 |
        | 📈 **PE/PB 估值** | 当前市盈率/市净率 |
        | 📐 **历史分位数** | 3年/5年/10年/20年 PE/PB 分位数 |
        | 💰 **分红情况** | 历史分红记录 |

        👈 **请在左侧输入 ETF 代码或名称开始查询**
        """)
        return

    # 显示详情
    detail = st.session_state.etf_detail
    etf_name = st.session_state.selected_etf_name or ""
    etf_code = st.session_state.selected_etf_code or ""

    st.header(f"{etf_name}（{etf_code}）")

    # Tab 切换
    tab1, tab2, tab3, tab4 = st.tabs([
        "📋 基本信息 & K线图",
        "🏢 成分股持仓",
        "📈 PE/PB 估值分析",
        "💰 分红记录",
    ])

    # Tab 1: 基本信息 + K线
    with tab1:
        st.subheader("基本信息")
        basic_info = detail.get("basic_info", {})
        render_basic_info(basic_info)

        st.markdown("---")
        st.subheader("K线走势图")
        kline = detail.get("kline_data")
        if kline is not None and not kline.empty:
            kline_days = st.selectbox(
                "显示时间范围",
                options=[90, 180, 365, 730, 1825, 3650],
                index=2,
                format_func=lambda x: f"近{x}天" if x < 365 else f"近{x//365}年",
                key="kline_days",
            )
            render_kline_chart(kline.tail(kline_days), title=f"{etf_name}({etf_code}) K线图")
        else:
            st.info("暂无 K线数据")

    # Tab 2: 成分股
    with tab2:
        st.subheader("成分股 / 前十大持仓")
        holdings = detail.get("holdings")
        render_holdings(holdings)

    # Tab 3: PE/PB 估值
    with tab3:
        st.subheader("PE/PB 估值分析")
        valuation = detail.get("valuation", {})
        index_code = valuation.get("index_code", "N/A")
        if index_code and index_code != "N/A":
            st.caption(f"跟踪指数: {index_code}")
        render_valuation(valuation)

    # Tab 4: 分红
    with tab4:
        st.subheader("历史分红记录")
        dividends = detail.get("dividends")
        render_dividends(dividends)

    # 底部：错误汇总
    errors = detail.get("errors", [])
    if errors:
        with st.expander(f"⚠️ 数据获取错误/警告 ({len(errors)})", expanded=False):
            for err in errors:
                st.warning(err)


if __name__ == "__main__":
    main()
