import datetime as dt
from dataclasses import dataclass, replace

import akshare as ak
import numpy as np
import pandas as pd
import plotly.graph_objects as go
import streamlit as st
from plotly.subplots import make_subplots


st.set_page_config(page_title="A-Share Quant Analyzer", layout="wide")


@dataclass
class SignalResult:
    action: str
    score: int
    reasons: list[str]


@dataclass
class BacktestResult:
    total_return: float
    annual_return: float
    max_drawdown: float
    sharpe: float
    trade_count: int
    win_rate: float


@dataclass
class BacktestConfig:
    position_size: float
    fee_rate: float
    slippage_rate: float
    enable_fixed_stop: bool
    fixed_stop_loss: float
    enable_fixed_take_profit: bool
    fixed_take_profit: float
    enable_trailing_stop: bool
    trailing_stop: float
    enable_atr_stop: bool
    atr_stop_mult: float
    require_resonance: bool


def _safe_str(value: object) -> str:
    if value is None:
        return ""
    return str(value).strip()


@st.cache_data(ttl=60 * 30)
def get_spot_data() -> pd.DataFrame:
    spot = ak.stock_zh_a_spot_em()
    spot = spot.copy()

    keep_cols = {
        "代码": "code",
        "名称": "name",
        "最新价": "price",
        "涨跌幅": "pct_change",
        "成交额": "turnover",
        "总市值": "market_cap",
        "市盈率-动态": "pe_ttm",
    }
    spot = spot[[c for c in keep_cols if c in spot.columns]].rename(columns=keep_cols)

    for col in ["price", "pct_change", "turnover", "market_cap", "pe_ttm"]:
        if col in spot.columns:
            spot[col] = pd.to_numeric(spot[col], errors="coerce")

    return spot.dropna(subset=["code", "name"])


@st.cache_data(ttl=60 * 30)
def get_hist_data(code: str, start_date: str, end_date: str) -> pd.DataFrame:
    raw = ak.stock_zh_a_hist(
        symbol=code,
        period="daily",
        start_date=start_date,
        end_date=end_date,
        adjust="qfq",
    )
    raw = raw.copy()
    rename_map = {
        "日期": "date",
        "开盘": "open",
        "收盘": "close",
        "最高": "high",
        "最低": "low",
        "成交量": "volume",
        "成交额": "amount",
        "涨跌幅": "pct_change",
    }
    raw = raw[[c for c in rename_map if c in raw.columns]].rename(columns=rename_map)
    raw["date"] = pd.to_datetime(raw["date"])
    for c in ["open", "close", "high", "low", "volume", "amount", "pct_change"]:
        raw[c] = pd.to_numeric(raw[c], errors="coerce")
    raw = raw.dropna(subset=["date", "close"]).sort_values("date").reset_index(drop=True)
    return raw


@st.cache_data(ttl=60 * 60)
def get_industry_boards() -> pd.DataFrame:
    try:
        board = ak.stock_board_industry_name_em().copy()
    except Exception:
        return pd.DataFrame()

    keep_cols = {
        "板块名称": "industry",
        "涨跌幅": "pct_change",
        "总市值": "market_cap",
        "换手率": "turnover_rate",
        "上涨家数": "up_count",
        "下跌家数": "down_count",
        "领涨股票": "leader",
    }
    board = board[[c for c in keep_cols if c in board.columns]].rename(columns=keep_cols)
    for col in ["pct_change", "market_cap", "turnover_rate", "up_count", "down_count"]:
        if col in board.columns:
            board[col] = pd.to_numeric(board[col], errors="coerce")

    if "industry" in board.columns:
        board["industry"] = board["industry"].astype(str).str.strip()

    return board.dropna(subset=["industry"]).reset_index(drop=True)


@st.cache_data(ttl=60 * 60)
def get_industry_constituents(industry: str) -> pd.DataFrame:
    try:
        cons = ak.stock_board_industry_cons_em(symbol=industry).copy()
    except Exception:
        return pd.DataFrame(columns=["code", "name"])

    keep_cols = {"代码": "code", "名称": "name"}
    cons = cons[[c for c in keep_cols if c in cons.columns]].rename(columns=keep_cols)
    if "code" in cons.columns:
        cons["code"] = cons["code"].astype(str).str.zfill(6)
    return cons.dropna(subset=["code"]).reset_index(drop=True)


def get_industry_code_set(selected_industries: list[str]) -> set[str]:
    code_set: set[str] = set()
    for ind in selected_industries:
        cons = get_industry_constituents(ind)
        if not cons.empty:
            code_set.update(cons["code"].astype(str).tolist())
    return code_set


def add_indicators(df: pd.DataFrame) -> pd.DataFrame:
    out = df.copy()

    for w in [5, 10, 20, 60]:
        out[f"ma{w}"] = out["close"].rolling(w).mean()

    ema12 = out["close"].ewm(span=12, adjust=False).mean()
    ema26 = out["close"].ewm(span=26, adjust=False).mean()
    out["dif"] = ema12 - ema26
    out["dea"] = out["dif"].ewm(span=9, adjust=False).mean()
    out["macd"] = (out["dif"] - out["dea"]) * 2

    delta = out["close"].diff()
    up = np.where(delta > 0, delta, 0.0)
    down = np.where(delta < 0, -delta, 0.0)
    roll_up = pd.Series(up, index=out.index).rolling(14).mean()
    roll_down = pd.Series(down, index=out.index).rolling(14).mean()
    rs = roll_up / roll_down.replace(0, np.nan)
    out["rsi14"] = 100 - (100 / (1 + rs))

    mid = out["close"].rolling(20).mean()
    std = out["close"].rolling(20).std()
    out["boll_mid"] = mid
    out["boll_up"] = mid + 2 * std
    out["boll_low"] = mid - 2 * std

    prev_close = out["close"].shift(1)
    tr1 = out["high"] - out["low"]
    tr2 = (out["high"] - prev_close).abs()
    tr3 = (out["low"] - prev_close).abs()
    out["atr14"] = pd.concat([tr1, tr2, tr3], axis=1).max(axis=1).rolling(14).mean()
    out["atr_pct"] = out["atr14"] / out["close"] * 100

    out["vol_ma20"] = out["volume"].rolling(20).mean()
    out["vol_ratio"] = out["volume"] / out["vol_ma20"].replace(0, np.nan)

    out["high20"] = out["high"].shift(1).rolling(20).max()
    out["breakout20"] = out["close"] > out["high20"]
    out["rs20"] = out["close"] / out["close"].shift(20) - 1

    out["buy_point"] = (
        (out["close"] > out["ma20"])
        & (out["close"].shift(1) <= out["ma20"].shift(1))
        & (out["dif"] > out["dea"])
        & (out["dif"].shift(1) <= out["dea"].shift(1))
        & (out["rsi14"] < 70)
    )

    out["sell_point"] = (
        ((out["close"] < out["ma20"]) & (out["close"].shift(1) >= out["ma20"].shift(1)))
        | ((out["dif"] < out["dea"]) & (out["dif"].shift(1) >= out["dea"].shift(1)))
        | (out["rsi14"] > 80)
    )

    return out


def attach_weekly_context(df: pd.DataFrame) -> pd.DataFrame:
    week = (
        df.set_index("date")
        .resample("W-FRI")
        .agg(
            {
                "open": "first",
                "high": "max",
                "low": "min",
                "close": "last",
                "volume": "sum",
                "amount": "sum",
            }
        )
        .dropna(subset=["close"])
        .reset_index()
    )

    week["w_ma10"] = week["close"].rolling(10).mean()
    w_ema12 = week["close"].ewm(span=12, adjust=False).mean()
    w_ema26 = week["close"].ewm(span=26, adjust=False).mean()
    week["w_dif"] = w_ema12 - w_ema26
    week["w_dea"] = week["w_dif"].ewm(span=9, adjust=False).mean()
    week["weekly_up"] = (week["close"] > week["w_ma10"]) & (week["w_dif"] > week["w_dea"])

    merged = pd.merge_asof(
        df.sort_values("date"),
        week[["date", "weekly_up"]].sort_values("date"),
        on="date",
        direction="backward",
    )
    merged["weekly_up"] = merged["weekly_up"].fillna(False)
    return merged


def score_signal(latest: pd.Series) -> SignalResult:
    score = 0
    reasons: list[str] = []

    if latest["close"] > latest["ma20"]:
        score += 1
        reasons.append("close > MA20")
    if latest["ma5"] > latest["ma10"] > latest["ma20"]:
        score += 1
        reasons.append("MA trend up")
    if latest["dif"] > latest["dea"]:
        score += 1
        reasons.append("MACD golden area")
    if 45 <= latest["rsi14"] <= 70:
        score += 1
        reasons.append("RSI healthy")
    if latest["close"] <= latest["boll_low"] * 1.02:
        score += 1
        reasons.append("Near lower Bollinger (value zone)")
    if pd.notna(latest.get("vol_ratio")) and latest["vol_ratio"] >= 1.2 and latest["pct_change"] > 0:
        score += 1
        reasons.append("Volume expansion on green day")
    if pd.notna(latest.get("breakout20")) and bool(latest["breakout20"]):
        score += 1
        reasons.append("20-day breakout")
    if pd.notna(latest.get("rs20")) and latest["rs20"] > 0.05:
        score += 1
        reasons.append("Positive 20-day relative strength")
    if pd.notna(latest.get("atr_pct")) and latest["atr_pct"] > 7:
        score -= 1
        reasons.append("High volatility risk (ATR%)")
    if bool(latest.get("weekly_up", False)):
        score += 1
        reasons.append("Daily + weekly resonance")

    if latest.get("buy_point", False):
        score += 2
        reasons.append("New buy trigger")
    if latest.get("sell_point", False):
        score -= 2
        reasons.append("Sell warning")

    if score >= 5:
        action = "Buy Candidate"
    elif score <= 1:
        action = "Sell / Reduce"
    else:
        action = "Watch"

    return SignalResult(action=action, score=score, reasons=reasons)


def build_kline_figure(df: pd.DataFrame, code: str, name: str) -> go.Figure:
    fig = make_subplots(
        rows=3,
        cols=1,
        shared_xaxes=True,
        vertical_spacing=0.05,
        row_heights=[0.55, 0.25, 0.20],
        subplot_titles=(f"{code} {name} - Price", "MACD", "RSI"),
    )

    fig.add_trace(
        go.Candlestick(
            x=df["date"],
            open=df["open"],
            high=df["high"],
            low=df["low"],
            close=df["close"],
            name="Kline",
        ),
        row=1,
        col=1,
    )

    for col, nm in [("ma5", "MA5"), ("ma10", "MA10"), ("ma20", "MA20"), ("ma60", "MA60")]:
        fig.add_trace(
            go.Scatter(x=df["date"], y=df[col], mode="lines", name=nm),
            row=1,
            col=1,
        )

    buy_df = df[df["buy_point"]]
    sell_df = df[df["sell_point"]]
    fig.add_trace(
        go.Scatter(
            x=buy_df["date"],
            y=buy_df["close"],
            mode="markers",
            marker=dict(symbol="triangle-up", size=11, color="#2ca02c"),
            name="Buy",
        ),
        row=1,
        col=1,
    )
    fig.add_trace(
        go.Scatter(
            x=sell_df["date"],
            y=sell_df["close"],
            mode="markers",
            marker=dict(symbol="triangle-down", size=11, color="#d62728"),
            name="Sell",
        ),
        row=1,
        col=1,
    )

    colors = np.where(df["macd"] >= 0, "#d62728", "#2ca02c")
    fig.add_trace(
        go.Bar(x=df["date"], y=df["macd"], marker_color=colors, name="MACD Hist"),
        row=2,
        col=1,
    )
    fig.add_trace(
        go.Scatter(x=df["date"], y=df["dif"], mode="lines", name="DIF"),
        row=2,
        col=1,
    )
    fig.add_trace(
        go.Scatter(x=df["date"], y=df["dea"], mode="lines", name="DEA"),
        row=2,
        col=1,
    )

    fig.add_trace(
        go.Scatter(x=df["date"], y=df["rsi14"], mode="lines", name="RSI14", line=dict(color="#1f77b4")),
        row=3,
        col=1,
    )
    fig.add_hline(y=70, line_dash="dash", line_color="gray", row=3, col=1)
    fig.add_hline(y=30, line_dash="dash", line_color="gray", row=3, col=1)

    fig.update_layout(height=900, showlegend=True, xaxis_rangeslider_visible=False)
    return fig


def run_backtest(df: pd.DataFrame, cfg: BacktestConfig) -> tuple[pd.DataFrame, BacktestResult]:
    bt = df.copy()
    bt["ret"] = bt["close"].pct_change().fillna(0.0)

    positions: list[float] = []
    position = 0.0
    entry_price = np.nan
    highest_price = np.nan
    trades: list[float] = []

    for row in bt.itertuples(index=False):
        close_price = float(getattr(row, "close"))
        atr14 = getattr(row, "atr14")
        buy_flag = bool(getattr(row, "buy_point"))
        sell_flag = bool(getattr(row, "sell_point"))
        weekly_up = bool(getattr(row, "weekly_up", False))

        if position > 0:
            highest_price = close_price if np.isnan(highest_price) else max(highest_price, close_price)

            exit_by_fixed_stop = cfg.enable_fixed_stop and close_price <= entry_price * (1 - cfg.fixed_stop_loss)
            exit_by_fixed_tp = cfg.enable_fixed_take_profit and close_price >= entry_price * (1 + cfg.fixed_take_profit)
            exit_by_trailing = cfg.enable_trailing_stop and close_price <= highest_price * (1 - cfg.trailing_stop)
            exit_by_atr = (
                cfg.enable_atr_stop
                and pd.notna(atr14)
                and close_price <= highest_price - cfg.atr_stop_mult * float(atr14)
            )

            if sell_flag or exit_by_fixed_stop or exit_by_fixed_tp or exit_by_trailing or exit_by_atr:
                sell_price_eff = close_price * (1 - cfg.fee_rate - cfg.slippage_rate)
                buy_price_eff = entry_price * (1 + cfg.fee_rate + cfg.slippage_rate)
                trades.append(sell_price_eff / buy_price_eff - 1)

                position = 0.0
                entry_price = np.nan
                highest_price = np.nan

        if position == 0:
            can_buy = buy_flag and (weekly_up or (not cfg.require_resonance))
            if can_buy:
                position = cfg.position_size
                entry_price = close_price
                highest_price = close_price

        positions.append(position)

    bt["position_raw"] = positions
    bt["position"] = bt["position_raw"].shift(1).fillna(0.0)

    turnover = bt["position_raw"].diff().abs().fillna(bt["position_raw"].abs())
    trade_cost = turnover * (cfg.fee_rate + cfg.slippage_rate)
    bt["strategy_ret"] = bt["position"] * bt["ret"] - trade_cost

    bt["strategy_nav"] = (1 + bt["strategy_ret"]).cumprod()
    bt["benchmark_nav"] = (1 + bt["ret"]).cumprod()

    years = max(len(bt) / 252, 1 / 252)
    total_return = bt["strategy_nav"].iloc[-1] - 1
    annual_return = bt["strategy_nav"].iloc[-1] ** (1 / years) - 1

    nav_peak = bt["strategy_nav"].cummax()
    drawdown = bt["strategy_nav"] / nav_peak - 1
    max_drawdown = float(drawdown.min())

    ret_std = bt["strategy_ret"].std()
    sharpe = float(np.sqrt(252) * bt["strategy_ret"].mean() / ret_std) if ret_std and ret_std > 0 else 0.0

    trade_count = len(trades)
    win_rate = float(sum(1 for x in trades if x > 0) / trade_count) if trade_count > 0 else 0.0

    return bt, BacktestResult(
        total_return=float(total_return),
        annual_return=float(annual_return),
        max_drawdown=max_drawdown,
        sharpe=sharpe,
        trade_count=trade_count,
        win_rate=win_rate,
    )


def build_universe_ranking(candidates: pd.DataFrame, start_str: str, end_str: str, limit: int) -> pd.DataFrame:
    rows: list[dict[str, object]] = []
    pool = candidates.head(limit)
    for r in pool.itertuples(index=False):
        try:
            hist = get_hist_data(str(r.code), start_str, end_str)
            if hist.empty or len(hist) < 70:
                continue
            ana = add_indicators(hist)
            ana = attach_weekly_context(ana)
            latest = ana.iloc[-1]
            sig = score_signal(latest)
            rows.append(
                {
                    "code": r.code,
                    "name": r.name,
                    "signal": sig.action,
                    "score": sig.score,
                    "close": float(latest["close"]),
                    "pct_change": float(latest["pct_change"]),
                    "rsi14": float(latest["rsi14"]),
                    "atr_pct": float(latest["atr_pct"]) if pd.notna(latest["atr_pct"]) else np.nan,
                    "vol_ratio": float(latest["vol_ratio"]) if pd.notna(latest["vol_ratio"]) else np.nan,
                    "rs20": float(latest["rs20"]) if pd.notna(latest["rs20"]) else np.nan,
                    "buy_point": bool(latest["buy_point"]),
                    "sell_point": bool(latest["sell_point"]),
                    "weekly_up": bool(latest["weekly_up"]),
                }
            )
        except Exception:
            continue

    if not rows:
        return pd.DataFrame()

    rank = pd.DataFrame(rows)
    rank = rank.sort_values(["score", "rs20", "pct_change"], ascending=[False, False, False]).reset_index(drop=True)
    return rank


def optimize_backtest_parameters(df: pd.DataFrame, base_cfg: BacktestConfig, max_trials: int = 60) -> pd.DataFrame:
    stop_grid = [0.05, 0.08, 0.10]
    trailing_grid = [0.06, 0.08, 0.12]
    atr_mult_grid = [1.5, 2.0, 2.5]
    resonance_grid = [False, True]

    rows: list[dict[str, object]] = []
    trial = 0
    for stop_loss in stop_grid:
        for trailing in trailing_grid:
            for atr_mult in atr_mult_grid:
                for req_res in resonance_grid:
                    trial += 1
                    if trial > max_trials:
                        break

                    cfg = replace(
                        base_cfg,
                        fixed_stop_loss=stop_loss,
                        trailing_stop=trailing,
                        atr_stop_mult=atr_mult,
                        require_resonance=req_res,
                    )
                    _, res = run_backtest(df, cfg)
                    score = res.annual_return - 0.5 * abs(res.max_drawdown)
                    rows.append(
                        {
                            "score": score,
                            "annual_return": res.annual_return,
                            "total_return": res.total_return,
                            "max_drawdown": res.max_drawdown,
                            "sharpe": res.sharpe,
                            "win_rate": res.win_rate,
                            "trades": res.trade_count,
                            "fixed_stop_loss": stop_loss,
                            "trailing_stop": trailing,
                            "atr_stop_mult": atr_mult,
                            "require_resonance": req_res,
                        }
                    )
                if trial > max_trials:
                    break
            if trial > max_trials:
                break
        if trial > max_trials:
            break

    if not rows:
        return pd.DataFrame()

    return pd.DataFrame(rows).sort_values("score", ascending=False).reset_index(drop=True)


def to_csv_bytes(df: pd.DataFrame) -> bytes:
    return df.to_csv(index=False).encode("utf-8-sig")


def run_portfolio_backtest(ranking_df: pd.DataFrame, start_str: str, end_str: str, top_n: int) -> tuple[pd.DataFrame, pd.DataFrame]:
    if ranking_df.empty:
        return pd.DataFrame(), pd.DataFrame()

    selected = ranking_df.sort_values(["score", "rs20"], ascending=[False, False]).head(top_n).copy()
    ret_frames: list[pd.DataFrame] = []

    for r in selected.itertuples(index=False):
        try:
            hist = get_hist_data(str(r.code), start_str, end_str)
            if hist.empty or len(hist) < 30:
                continue
            one = hist[["date", "close"]].copy()
            one[f"ret_{r.code}"] = one["close"].pct_change()
            one = one[["date", f"ret_{r.code}"]]
            ret_frames.append(one)
        except Exception:
            continue

    if not ret_frames:
        return pd.DataFrame(), selected

    merged = ret_frames[0]
    for rf in ret_frames[1:]:
        merged = merged.merge(rf, on="date", how="outer")

    merged = merged.sort_values("date").reset_index(drop=True)
    ret_cols = [c for c in merged.columns if c.startswith("ret_")]
    merged["portfolio_ret"] = merged[ret_cols].mean(axis=1, skipna=True).fillna(0.0)
    merged["portfolio_nav"] = (1 + merged["portfolio_ret"]).cumprod()
    return merged, selected


def build_daily_report(
    code: str,
    name: str,
    signal: SignalResult,
    latest: pd.Series,
    bt_result: BacktestResult,
    cfg: BacktestConfig,
) -> str:
    lines = [
        "A-Share Quant Daily Report",
        f"Generated At: {dt.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"Stock: {code} {name}",
        "",
        "Signal",
        f"Action: {signal.action}",
        f"Score: {signal.score}",
        f"Latest Close: {float(latest['close']):.2f}",
        f"RSI14: {float(latest['rsi14']):.2f}",
        f"ATR%: {float(latest['atr_pct']):.2f}" if pd.notna(latest["atr_pct"]) else "ATR%: N/A",
        f"Volume Ratio: {float(latest['vol_ratio']):.2f}" if pd.notna(latest["vol_ratio"]) else "Volume Ratio: N/A",
        f"20D RS: {float(latest['rs20']) * 100:.2f}%" if pd.notna(latest["rs20"]) else "20D RS: N/A",
        f"Weekly Trend: {'Up' if bool(latest.get('weekly_up', False)) else 'Down'}",
        "",
        "Reasons",
    ]
    if signal.reasons:
        for r in signal.reasons:
            lines.append(f"- {r}")
    else:
        lines.append("- No strong reason")

    lines.extend(
        [
            "",
            "Backtest Summary",
            f"Total Return: {bt_result.total_return * 100:.2f}%",
            f"Annual Return: {bt_result.annual_return * 100:.2f}%",
            f"Max Drawdown: {bt_result.max_drawdown * 100:.2f}%",
            f"Sharpe: {bt_result.sharpe:.2f}",
            f"Trade Count: {bt_result.trade_count}",
            f"Win Rate: {bt_result.win_rate * 100:.2f}%",
            "",
            "Risk Setup",
            f"Position Size: {cfg.position_size * 100:.0f}%",
            f"Fee Rate: {cfg.fee_rate * 10000:.1f} bps",
            f"Slippage Rate: {cfg.slippage_rate * 10000:.1f} bps",
            f"Fixed Stop Loss Enabled: {cfg.enable_fixed_stop}",
            f"Fixed Take Profit Enabled: {cfg.enable_fixed_take_profit}",
            f"Trailing Stop Enabled: {cfg.enable_trailing_stop}",
            f"ATR Stop Enabled: {cfg.enable_atr_stop}",
            f"Resonance Required: {cfg.require_resonance}",
            "",
            "Disclaimer: For research and education only, not investment advice.",
        ]
    )
    return "\n".join(lines)


def main() -> None:
    st.title("A-Share Quant Buy/Sell Analyzer")
    st.caption("Data source: AKShare (for education and research only, not investment advice)")

    with st.sidebar:
        st.header("Filters")
        keyword = st.text_input("Stock keyword (code or name)", "")
        min_cap = st.number_input("Min market cap (CNY)", min_value=0.0, value=0.0, step=1e9, format="%.0f")
        max_candidates = st.slider("Max candidates shown", min_value=20, max_value=300, value=100, step=10)
        years = st.slider("Analysis window (years)", min_value=1, max_value=5, value=2)
        rank_limit = st.slider("Universe ranking size", min_value=10, max_value=120, value=40, step=10)
        resonance_only = st.checkbox("Ranking: resonance only (daily + weekly)", value=False)

        industry_board = get_industry_boards()
        industry_options = sorted(industry_board["industry"].tolist()) if not industry_board.empty else []
        selected_industries = st.multiselect(
            "Industry filter (optional)",
            options=industry_options,
            default=[],
        )

        st.header("Backtest & Risk")
        position_size_pct = st.slider("Position size (%)", min_value=10, max_value=100, value=100, step=10)
        fee_bps = st.number_input("Fee (bps each trade)", min_value=0.0, value=3.0, step=0.5)
        slippage_bps = st.number_input("Slippage (bps each trade)", min_value=0.0, value=2.0, step=0.5)

        enable_fixed_stop = st.checkbox("Enable fixed stop loss", value=True)
        fixed_stop_pct = st.slider("Fixed stop loss (%)", min_value=1.0, max_value=20.0, value=8.0, step=0.5)

        enable_fixed_tp = st.checkbox("Enable fixed take profit", value=False)
        fixed_tp_pct = st.slider("Fixed take profit (%)", min_value=2.0, max_value=50.0, value=20.0, step=1.0)

        enable_trailing = st.checkbox("Enable trailing stop", value=True)
        trailing_stop_pct = st.slider("Trailing stop (%)", min_value=2.0, max_value=20.0, value=8.0, step=0.5)

        enable_atr_stop = st.checkbox("Enable ATR stop", value=False)
        atr_mult = st.slider("ATR stop multiplier", min_value=1.0, max_value=5.0, value=2.0, step=0.5)
        require_resonance_entry = st.checkbox("Entry requires daily+weekly resonance", value=False)

    spot = get_spot_data()
    view = spot.copy()

    if keyword:
        key = _safe_str(keyword).lower()
        view = view[
            view["code"].astype(str).str.lower().str.contains(key)
            | view["name"].astype(str).str.lower().str.contains(key)
        ]

    if min_cap > 0 and "market_cap" in view.columns:
        view = view[view["market_cap"] >= min_cap]

    if selected_industries:
        ind_set = get_industry_code_set(selected_industries)
        if ind_set:
            view = view[view["code"].astype(str).isin(ind_set)]
        else:
            view = view.iloc[0:0]

    view = view.sort_values("turnover", ascending=False).head(max_candidates)

    if view.empty:
        st.warning("No stocks matched your filters.")
        return

    st.subheader("Candidate Stocks")
    st.dataframe(
        view[["code", "name", "price", "pct_change", "turnover", "market_cap"]],
        use_container_width=True,
        hide_index=True,
    )

    stock_options = [f"{r.code} | {r.name}" for r in view.itertuples(index=False)]

    end = dt.date.today()
    start = end - dt.timedelta(days=365 * years)
    start_str = start.strftime("%Y%m%d")
    end_str = end.strftime("%Y%m%d")

    st.subheader("Universe Ranking")
    if st.button("Run Ranking", use_container_width=False):
        with st.spinner("Ranking candidates..."):
            st.session_state["ranking_df"] = build_universe_ranking(view, start_str, end_str, rank_limit)

    ranking_df = st.session_state.get("ranking_df")
    if isinstance(ranking_df, pd.DataFrame) and not ranking_df.empty:
        if resonance_only:
            ranking_df = ranking_df[ranking_df["weekly_up"]]
        st.dataframe(
            ranking_df.head(30),
            use_container_width=True,
            hide_index=True,
        )
        st.download_button(
            label="Download ranking CSV",
            data=to_csv_bytes(ranking_df),
            file_name="universe_ranking.csv",
            mime="text/csv",
        )
    elif st.button("Show Hint", use_container_width=False):
        st.info("Click 'Run Ranking' to score the candidate universe.")

    if not industry_board.empty:
        st.subheader("Industry Strength Ranking")
        board_show = industry_board.sort_values("pct_change", ascending=False).head(20)
        st.dataframe(board_show, use_container_width=True, hide_index=True)

    selected = st.selectbox("Select one stock", options=stock_options)
    code = selected.split("|")[0].strip()
    name = selected.split("|")[1].strip()

    hist = get_hist_data(code, start_str, end_str)
    if hist.empty or len(hist) < 70:
        st.error("Not enough data for indicator analysis.")
        return

    ana = add_indicators(hist)
    ana = attach_weekly_context(ana)
    latest = ana.iloc[-1]
    signal = score_signal(latest)

    c1, c2, c3, c4 = st.columns(4)
    c1.metric("Signal", signal.action)
    c2.metric("Score", signal.score)
    c3.metric("Latest close", f"{latest['close']:.2f}")
    c4.metric("RSI14", f"{latest['rsi14']:.2f}")

    c5, c6, c7 = st.columns(3)
    c5.metric("ATR%", f"{latest['atr_pct']:.2f}%" if pd.notna(latest["atr_pct"]) else "N/A")
    c6.metric("Volume Ratio", f"{latest['vol_ratio']:.2f}" if pd.notna(latest["vol_ratio"]) else "N/A")
    c7.metric("20D RS", f"{latest['rs20'] * 100:.2f}%" if pd.notna(latest["rs20"]) else "N/A")

    c8, c9 = st.columns(2)
    c8.metric("Weekly Trend", "Up" if bool(latest["weekly_up"]) else "Down")
    c9.metric("Resonance", "Yes" if bool(latest["weekly_up"] and latest["buy_point"]) else "No")

    st.write("Signal reasons:")
    if signal.reasons:
        for reason in signal.reasons:
            st.write(f"- {reason}")
    else:
        st.write("- No strong signal")

    fig = build_kline_figure(ana, code, name)
    st.plotly_chart(fig, use_container_width=True)

    st.subheader("Recent Buy/Sell Points")
    recent = ana[["date", "close", "buy_point", "sell_point"]].tail(30).copy()
    recent["date"] = recent["date"].dt.strftime("%Y-%m-%d")
    st.dataframe(recent, use_container_width=True, hide_index=True)

    st.subheader("Strategy Backtest")
    cfg = BacktestConfig(
        position_size=position_size_pct / 100,
        fee_rate=fee_bps / 10000,
        slippage_rate=slippage_bps / 10000,
        enable_fixed_stop=enable_fixed_stop,
        fixed_stop_loss=fixed_stop_pct / 100,
        enable_fixed_take_profit=enable_fixed_tp,
        fixed_take_profit=fixed_tp_pct / 100,
        enable_trailing_stop=enable_trailing,
        trailing_stop=trailing_stop_pct / 100,
        enable_atr_stop=enable_atr_stop,
        atr_stop_mult=atr_mult,
        require_resonance=require_resonance_entry,
    )
    bt, bt_result = run_backtest(ana, cfg)
    b1, b2, b3, b4, b5, b6 = st.columns(6)
    b1.metric("Total Return", f"{bt_result.total_return * 100:.2f}%")
    b2.metric("Annual Return", f"{bt_result.annual_return * 100:.2f}%")
    b3.metric("Max Drawdown", f"{bt_result.max_drawdown * 100:.2f}%")
    b4.metric("Sharpe", f"{bt_result.sharpe:.2f}")
    b5.metric("Trades", str(bt_result.trade_count))
    b6.metric("Win Rate", f"{bt_result.win_rate * 100:.2f}%")

    nav_view = bt[["date", "strategy_nav", "benchmark_nav"]].copy()
    nav_view = nav_view.rename(columns={"strategy_nav": "Strategy", "benchmark_nav": "Benchmark"})
    nav_view = nav_view.set_index("date")
    st.line_chart(nav_view)

    cexp1, cexp2 = st.columns(2)
    with cexp1:
        st.download_button(
            label="Download backtest daily CSV",
            data=to_csv_bytes(bt),
            file_name=f"backtest_{code}.csv",
            mime="text/csv",
        )
    with cexp2:
        summary_df = pd.DataFrame(
            [
                {
                    "code": code,
                    "name": name,
                    "signal": signal.action,
                    "score": signal.score,
                    "total_return": bt_result.total_return,
                    "annual_return": bt_result.annual_return,
                    "max_drawdown": bt_result.max_drawdown,
                    "sharpe": bt_result.sharpe,
                    "trade_count": bt_result.trade_count,
                    "win_rate": bt_result.win_rate,
                }
            ]
        )
        st.download_button(
            label="Download summary CSV",
            data=to_csv_bytes(summary_df),
            file_name=f"summary_{code}.csv",
            mime="text/csv",
        )

    st.subheader("Portfolio Backtest")
    top_n_portfolio = st.slider("Portfolio top N from ranking", min_value=3, max_value=20, value=8, step=1)
    can_run_portfolio = isinstance(ranking_df, pd.DataFrame) and not ranking_df.empty
    if st.button("Run Portfolio Backtest", use_container_width=False):
        if not can_run_portfolio:
            st.warning("Run universe ranking first, then run portfolio backtest.")
        else:
            with st.spinner("Building equal-weight portfolio..."):
                pnav, psel = run_portfolio_backtest(ranking_df, start_str, end_str, top_n=top_n_portfolio)
                st.session_state["portfolio_nav"] = pnav
                st.session_state["portfolio_selected"] = psel

    pnav = st.session_state.get("portfolio_nav")
    psel = st.session_state.get("portfolio_selected")
    if isinstance(pnav, pd.DataFrame) and not pnav.empty:
        pshow = pnav[["date", "portfolio_nav"]].copy().set_index("date")
        st.line_chart(pshow)
        total_ret = float(pnav["portfolio_nav"].iloc[-1] - 1)
        st.metric("Portfolio Total Return", f"{total_ret * 100:.2f}%")
        st.download_button(
            label="Download portfolio CSV",
            data=to_csv_bytes(pnav),
            file_name="portfolio_backtest.csv",
            mime="text/csv",
        )
    if isinstance(psel, pd.DataFrame) and not psel.empty:
        st.caption("Portfolio Constituents")
        st.dataframe(psel[["code", "name", "score", "signal"]], use_container_width=True, hide_index=True)

    st.subheader("Daily Report")
    report_text = build_daily_report(code, name, signal, latest, bt_result, cfg)
    st.text_area("Report Preview", value=report_text, height=300)
    st.download_button(
        label="Download daily report (txt)",
        data=report_text.encode("utf-8"),
        file_name=f"daily_report_{code}.txt",
        mime="text/plain",
    )

    st.subheader("Parameter Grid Optimization")
    max_trials = st.slider("Optimization max trials", min_value=10, max_value=80, value=54, step=2)
    if st.button("Run Optimization", use_container_width=False):
        with st.spinner("Running parameter grid search..."):
            st.session_state["opt_df"] = optimize_backtest_parameters(ana, cfg, max_trials=max_trials)

    opt_df = st.session_state.get("opt_df")
    if isinstance(opt_df, pd.DataFrame) and not opt_df.empty:
        show_opt = opt_df.copy()
        for col in ["score", "annual_return", "total_return", "max_drawdown", "sharpe", "win_rate"]:
            if col in show_opt.columns:
                show_opt[col] = show_opt[col].astype(float)
        st.dataframe(show_opt.head(20), use_container_width=True, hide_index=True)
        best = show_opt.iloc[0]
        st.info(
            "Best params: "
            f"fixed_stop_loss={best['fixed_stop_loss']:.2f}, "
            f"trailing_stop={best['trailing_stop']:.2f}, "
            f"atr_stop_mult={best['atr_stop_mult']:.1f}, "
            f"require_resonance={bool(best['require_resonance'])}"
        )
        st.download_button(
            label="Download optimization CSV",
            data=to_csv_bytes(show_opt),
            file_name=f"optimization_{code}.csv",
            mime="text/csv",
        )


if __name__ == "__main__":
    main()
