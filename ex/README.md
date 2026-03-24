# A-Share Quant Buy/Sell Analyzer

A Streamlit-based quant analysis app for China A-share stocks.

## Features

- Filter stocks by code/name and market cap
- Browse top active candidates by turnover
- View technical indicators: MA, MACD, RSI, Bollinger
- Extra factors: ATR volatility, volume ratio, 20-day relative strength
- Auto-generate buy/sell signal scores
- Multi-timeframe resonance filter (daily + weekly trend)
- Industry filter and industry strength ranking
- Batch universe ranking with score sorting
- Built-in strategy backtest (return, annualized return, drawdown, Sharpe, win rate)
- Backtest controls: position sizing, fee/slippage, fixed stop loss, fixed take profit, trailing stop, ATR stop
- Parameter grid optimization for stop/trailing/ATR/resonance combinations
- CSV export for ranking, backtest daily results, summary, and optimization table
- Portfolio backtest from ranked stocks (equal-weight)
- One-click daily report export (TXT)
- Visualize candlestick chart with signal points

## Quick Start

1. Create and activate Python environment (already configured in this workspace).
1. Install dependencies:

```bash
pip install -r requirements.txt
```

1. Run app:

```bash
streamlit run app.py
```

1. Open the local URL shown by Streamlit in your browser.

## Notes

- Data source: AKShare
- For educational/research purpose only; not investment advice
