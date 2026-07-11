"""UI 组件

封装图表、表格等可复用的展示组件。
"""

import logging

import pandas as pd
import plotly.graph_objects as go
from plotly.subplots import make_subplots
import streamlit as st

logger = logging.getLogger(__name__)


def render_kline_chart(df: pd.DataFrame, title: str = "K线走势"):
    """渲染 K线图（使用 Plotly 交互式图表）。
    
    如果数据中包含 OHLC 字段则绘制 K线，否则使用折线图。
    """
    if df is None or df.empty:
        st.info("暂无 K线数据")
        return

    # 尝试找到日期、开高低收列
    date_col = None
    open_col, high_col, low_col, close_col = None, None, None, None
    volume_col = None

    for col in df.columns:
        col_lower = col.lower()
        if "日期" in col or col_lower in ("date", "trade_date"):
            date_col = col
        elif "开盘" in col or col_lower == "open":
            open_col = col
        elif "最高" in col or col_lower == "high":
            high_col = col
        elif "最低" in col or col_lower == "low":
            low_col = col
        elif "收盘" in col or col_lower == "close":
            close_col = col
        elif "成交量" in col or col_lower == "volume":
            volume_col = col

    if date_col is None:
        logger.error(f"K线数据缺少日期列，实际列: {list(df.columns)}")
        st.error("K线数据格式异常，缺少日期列")
        return

    df = df.copy()
    df[date_col] = pd.to_datetime(df[date_col], errors="coerce")
    df = df.sort_values(date_col)

    has_ohlc = all(c is not None for c in [open_col, high_col, low_col, close_col])

    if has_ohlc:
        # K线图 + 成交量
        fig = make_subplots(
            rows=2, cols=1,
            shared_xaxes=True,
            vertical_spacing=0.03,
            row_heights=[0.7, 0.3],
        )

        # K线
        fig.add_trace(
            go.Candlestick(
                x=df[date_col],
                open=df[open_col],
                high=df[high_col],
                low=df[low_col],
                close=df[close_col],
                name="K线",
            ),
            row=1, col=1,
        )

        # 成交量
        if volume_col is not None:
            colors = ["red" if close > open else "green" for close, open in zip(df[close_col], df[open_col])]
            fig.add_trace(
                go.Bar(
                    x=df[date_col],
                    y=df[volume_col],
                    name="成交量",
                    marker_color=colors,
                    opacity=0.5,
                ),
                row=2, col=1,
            )

        fig.update_layout(
            title=title,
            xaxis_rangeslider_visible=False,
            template="plotly_white",
            height=600,
        )
        fig.update_yaxes(title_text="价格", row=1, col=1)
        fig.update_yaxes(title_text="成交量", row=2, col=1)
    else:
        # 折线图（使用单位净值或其他价格列）
        price_col = None
        for col in df.columns:
            col_lower = col.lower()
            if "净值" in col or "价格" in col or col_lower in ("close", "nav", "price"):
                price_col = col
                break

        if price_col is None:
            # 使用第一个数值列
            numeric_cols = df.select_dtypes(include=["number"]).columns
            if len(numeric_cols) > 0:
                price_col = numeric_cols[0]

        if price_col is None:
            st.warning("K线数据中未找到可用价格列")
            return

        fig = go.Figure()
        fig.add_trace(
            go.Scatter(
                x=df[date_col],
                y=df[price_col],
                mode="lines",
                name=price_col,
                line=dict(color="#1677ff", width=2),
                fill="tozeroy",
                fillcolor="rgba(22,119,255,0.05)",
            )
        )

        fig.update_layout(
            title=title,
            template="plotly_white",
            height=500,
            hovermode="x unified",
        )
        fig.update_yaxes(title_text=price_col)

    st.plotly_chart(fig, use_container_width=True)


def render_pe_pb_percentile_table(percentiles: dict) -> None:
    """渲染 PE/PB 分位数表格。"""
    pe_data = percentiles.get("pe", {})
    pb_data = percentiles.get("pb", {})

    if not pe_data and not pb_data:
        st.info("暂无 PE/PB 分位数数据")
        return

    windows = sorted(set(list(pe_data.keys()) + list(pb_data.keys())),
                     key=lambda x: int(x.replace("年", "")))

    rows = []
    for w in windows:
        pe_val = pe_data.get(w, None)
        pb_val = pb_data.get(w, None)
        rows.append({
            "时间窗口": w,
            "PE 分位数": f"{pe_val:.1f}%" if pe_val is not None else "N/A",
            "PB 分位数": f"{pb_val:.1f}%" if pb_val is not None else "N/A",
            "PE 分位数值": pe_val if pe_val is not None else float("nan"),
            "PB 分位数值": pb_val if pb_val is not None else float("nan"),
        })

    result_df = pd.DataFrame(rows)

    # 颜色标注
    def color_percentile(val):
        try:
            v = float(val.replace("%", ""))
            if v < 20:
                return "background-color: #52c41a; color: white"  # 低估 - 绿色
            elif v < 50:
                return "background-color: #faad14; color: white"  # 中等偏低估 - 黄色
            elif v < 80:
                return "background-color: #ff7a45; color: white"  # 中等偏高估 - 橙色
            else:
                return "background-color: #ff4d4f; color: white"  # 高估 - 红色
        except (ValueError, TypeError):
            return ""

    st.dataframe(
        result_df[["时间窗口", "PE 分位数", "PB 分位数"]],
        use_container_width=True,
        hide_index=True,
    )

    # 说明
    st.caption("""
    分位数含义：当前 PE/PB 处于历史的百分比位置。
    - 🟢 < 20%：低估区间
    - 🟡 20%-50%：中等偏低估
    - 🟠 50%-80%：中等偏高估  
    - 🔴 > 80%：高估区间
    """)


def render_basic_info(info: dict) -> None:
    """渲染 ETF 基本信息卡片。"""
    if not info:
        st.info("暂无基本信息")
        return

    # 关键字段映射
    key_fields = [
        ("基金代码", ["基金代码", "代码", "code"]),
        ("基金简称", ["基金简称", "名称", "name"]),
        ("基金类型", ["基金类型", "类型", "type"]),
        ("基金经理", ["基金经理", "经理"]),
        ("基金管理人", ["基金管理人", "管理公司", "基金公司"]),
        ("成立日期", ["成立日期", "成立时间"]),
        ("基金规模", ["基金规模", "规模", "基金规模（亿元）"]),
        ("管理费率", ["管理费率", "管理费"]),
        ("托管费率", ["托管费率", "托管费"]),
        ("跟踪标的", ["跟踪标的指数", "跟踪标的", "跟踪指数"]),
    ]

    cols = st.columns(3)
    idx = 0
    for display_name, possible_keys in key_fields:
        value = None
        for key in possible_keys:
            # 在 info dict 中做模糊匹配
            for k, v in info.items():
                if key in k and pd.notna(v) and str(v).strip():
                    value = v
                    break
            if value is not None:
                break

        with cols[idx % 3]:
            st.metric(label=display_name, value=str(value) if value else "N/A")
        idx += 1

    # 显示所有其他字段
    shown_keys = set()
    for _, pks in key_fields:
        for pk in pks:
            shown_keys.add(pk)

    with st.expander("查看全部信息字段"):
        for k, v in info.items():
            if k not in shown_keys and pd.notna(v):
                st.text(f"{k}: {v}")


def render_holdings(df: pd.DataFrame) -> None:
    """渲染成分股持仓表格。"""
    if df is None or df.empty:
        st.info("暂无持仓/成分股数据")
        return

    # 常见列名映射
    col_map = {
        "股票代码": ["股票代码", "代码", "stock_code", "symbol"],
        "股票名称": ["股票名称", "名称", "stock_name", "name"],
        "占净值比例": ["占净值比例", "持仓占比", "占比", "weight", "ratio", "比例"],
        "持仓市值": ["持仓市值", "市值", "market_value"],
        "持股数": ["持股数", "股数", "shares"],
    }

    display_cols = {}
    for display_name, possible_keys in col_map.items():
        for pk in possible_keys:
            found = [c for c in df.columns if pk in str(c)]
            if found:
                display_cols[display_name] = found[0]
                break

    if not display_cols:
        # 直接用原始列
        st.dataframe(df, use_container_width=True, hide_index=True)
        return

    show_df = df[[v for v in display_cols.values()]].copy()
    show_df.columns = list(display_cols.keys())

    # 如果有占比列，格式化
    if "占净值比例" in display_cols:
        try:
            show_df["占净值比例"] = show_df["占净值比例"].apply(
                lambda x: f"{float(x):.2f}%" if pd.notna(x) else "N/A"
            )
        except Exception:
            pass

    st.dataframe(show_df, use_container_width=True, hide_index=True)


def render_valuation(valuation: dict) -> None:
    """渲染 PE/PB 估值信息。"""
    current = valuation.get("current", {})

    pe = current.get("pe")
    pb = current.get("pb")
    val_date = current.get("date", "N/A")

    st.caption(f"估值数据日期: {val_date}")

    col1, col2 = st.columns(2)
    with col1:
        st.metric(
            label="当前 PE（市盈率）",
            value=f"{pe:.2f}" if pe is not None else "N/A",
        )
    with col2:
        st.metric(
            label="当前 PB（市净率）",
            value=f"{pb:.2f}" if pb is not None else "N/A",
        )

    percentiles = valuation.get("percentiles", {"pe": {}, "pb": {}})
    st.subheader("PE/PB 历史分位数")
    render_pe_pb_percentile_table(percentiles)

    # PE/PB 走势图
    hist_data = valuation.get("hist_data")
    if hist_data is not None and not hist_data.empty:
        render_pe_pb_history_chart(hist_data)


def render_pe_pb_history_chart(df: pd.DataFrame) -> None:
    """渲染 PE/PB 历史走势图。"""
    if df is None or df.empty:
        return

    date_col = None
    for col in df.columns:
        if "日期" in str(col) or "date" in str(col).lower():
            date_col = col
            break

    if date_col is None:
        logger.warning("PE/PB 历史数据缺少日期列")
        return

    df = df.copy()
    df[date_col] = pd.to_datetime(df[date_col], errors="coerce")
    df = df.sort_values(date_col)

    has_pe = any("pe" in str(c).lower() or "市盈率" in str(c) for c in df.columns)
    has_pb = any("pb" in str(c).lower() or "市净率" in str(c) for c in df.columns)

    if not has_pe and not has_pb:
        return

    rows = 0
    if has_pe:
        rows += 1
    if has_pb:
        rows += 1

    fig = make_subplots(rows=rows, cols=1, shared_xaxes=True, vertical_spacing=0.08)

    current_row = 1
    if has_pe:
        pe_col = [c for c in df.columns if "pe" in str(c).lower() or "市盈率" in str(c)][0]
        fig.add_trace(
            go.Scatter(
                x=df[date_col], y=df[pe_col], mode="lines",
                name="PE", line=dict(color="#1677ff", width=2),
            ),
            row=current_row, col=1,
        )
        fig.update_yaxes(title_text="PE（市盈率）", row=current_row, col=1)
        current_row += 1

    if has_pb:
        pb_col = [c for c in df.columns if "pb" in str(c).lower() or "市净率" in str(c)][0]
        fig.add_trace(
            go.Scatter(
                x=df[date_col], y=df[pb_col], mode="lines",
                name="PB", line=dict(color="#52c41a", width=2),
            ),
            row=current_row, col=1,
        )
        fig.update_yaxes(title_text="PB（市净率）", row=current_row, col=1)

    fig.update_layout(
        title="PE/PB 历史走势",
        template="plotly_white",
        height=200 * rows,
        hovermode="x unified",
    )

    st.plotly_chart(fig, use_container_width=True)


def render_dividends(df: pd.DataFrame) -> None:
    """渲染分红记录。"""
    if df is None or df.empty:
        st.info("暂无分红记录")
        return

    # 查找关键列
    key_cols = {}
    for display, keywords in [
        ("除息日", ["除息日", "除权日", "ex_date"]),
        ("分红方案", ["分红方案", "方案", "dividend_plan"]),
        ("每份分红", ["每份分红", "每份派息", "per_dividend", "每份收益"]),
        ("权益登记日", ["权益登记日", "登记日"]),
    ]:
        for kw in keywords:
            found = [c for c in df.columns if kw in str(c)]
            if found:
                key_cols[display] = found[0]
                break

    if key_cols:
        show_df = df[[v for v in key_cols.values()]].copy()
        show_df.columns = list(key_cols.keys())
        st.dataframe(show_df, use_container_width=True, hide_index=True)
    else:
        st.dataframe(df, use_container_width=True, hide_index=True)
