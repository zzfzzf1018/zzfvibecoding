from __future__ import annotations

import argparse
from pathlib import Path

from stcompare.config import load_app_config, load_rule_config
from stcompare.logging_utils import configure_logging
from stcompare.scheduler import start_scheduler
from stcompare.service import MonitorService


def build_parser() -> argparse.ArgumentParser:
    root_dir = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description="A-share financial statement monitor")
    parser.add_argument("command", choices=["run-once", "start-scheduler"])
    parser.add_argument("--config", default=str(root_dir / "config" / "watchlist.yaml"))
    parser.add_argument("--rules", default=str(root_dir / "config" / "rules.yaml"))
    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    app_config = load_app_config(args.config)
    rule_config = load_rule_config(args.rules)
    configure_logging(app_config.app.log_level)
    service = MonitorService(app_config, rule_config)

    if args.command == "run-once":
        summary = service.run_once()
        print(f"Inserted records: {summary.inserted_records}")
        print(f"Skipped duplicate records: {summary.skipped_records}")
        print(f"Comparisons: {len(summary.comparisons)}")
        print(f"Errors: {len(summary.errors)}")
        for path in summary.report_paths:
            print(f"Report: {path}")
        return

    start_scheduler(service, app_config)


if __name__ == "__main__":
    main()
