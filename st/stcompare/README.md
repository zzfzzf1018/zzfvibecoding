# STCompare

STCompare is a local monitoring tool for A-share financial statements.

Features:
- periodically fetch balance sheet, income statement, and cash flow statement data
- store every snapshot locally in SQLite
- compare the latest statement against the most relevant historical version
- detect obvious differences with configurable thresholds
- generate Markdown and HTML reports
- print alerts to the console

## Quick Start

1. Create a Python virtual environment.
2. Install dependencies:

```bash
pip install -r requirements.txt
```

3. Review configuration files:
- `config/watchlist.yaml`
- `config/rules.yaml`

4. Run a single fetch and comparison cycle:

```bash
python src/main.py run-once
```

5. Start the built-in scheduler:

```bash
python src/main.py start-scheduler
```

## Output

- SQLite database: `data/stcompare.db`
- Reports: `reports/`

## Notes

- The default provider uses AkShare / Eastmoney public endpoints.
- Public endpoints can be unstable. The provider includes retry handling, and failed runs are captured in the report.
- The first run will usually only build the local baseline. Alerts become meaningful after at least one previous snapshot exists.
