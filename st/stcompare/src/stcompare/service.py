from __future__ import annotations

from datetime import datetime
import logging

from stcompare.config import AppConfig, RuleConfig
from stcompare.diff import DiffEngine
from stcompare.domain import RunError, RunSummary
from stcompare.providers import AkshareFinanceProvider
from stcompare.reports import ReportGenerator
from stcompare.storage import StatementRepository


LOGGER = logging.getLogger(__name__)


class MonitorService:
    def __init__(self, app_config: AppConfig, rule_config: RuleConfig) -> None:
        self.app_config = app_config
        self.rule_config = rule_config
        self.provider = AkshareFinanceProvider(app_config.provider)
        self.repository = StatementRepository(app_config.app.database_path)
        self.diff_engine = DiffEngine(rule_config)
        self.report_generator = ReportGenerator(app_config.app.report_dir)
        self.repository.init_schema()

    def run_once(self) -> RunSummary:
        started_at = datetime.now().isoformat(timespec="seconds")
        inserted_records = 0
        skipped_records = 0
        comparisons = []
        errors = []

        for company in self.app_config.watchlist:
            if not company.enabled:
                continue
            try:
                records = self.provider.fetch_records(company)
            except Exception as exc:  # noqa: BLE001
                LOGGER.exception("Fetch failed for %s", company.symbol)
                errors.append(RunError(symbol=company.symbol, company_name=company.name, stage="fetch", message=str(exc)))
                continue

            records.sort(key=lambda item: (item.report_date, item.statement_type.value))
            for record in records:
                try:
                    inserted = self.repository.upsert_record(record)
                    if not inserted:
                        skipped_records += 1
                        continue
                    inserted_records += 1
                    baseline, baseline_kind = self.repository.get_comparison_baseline(record)
                    if baseline is None or baseline_kind is None:
                        continue
                    comparisons.append(self.diff_engine.compare(record, baseline, baseline_kind))
                except Exception as exc:  # noqa: BLE001
                    LOGGER.exception("Processing failed for %s %s", record.symbol, record.statement_type.value)
                    errors.append(
                        RunError(
                            symbol=record.symbol,
                            company_name=record.company_name,
                            stage="process",
                            message=f"{record.statement_type.value}: {exc}",
                        )
                    )

        completed_at = datetime.now().isoformat(timespec="seconds")
        summary = RunSummary(
            started_at=started_at,
            completed_at=completed_at,
            inserted_records=inserted_records,
            skipped_records=skipped_records,
            comparisons=comparisons,
            errors=errors,
        )
        summary.report_paths.extend(self.report_generator.generate(summary))
        return summary
