from __future__ import annotations

from pathlib import Path

from stcompare.domain import ComparisonResult, FinancialStatementRecord, RunSummary, StatementType
from stcompare.reports import ReportGenerator


def _record(report_date: str, fetched_at: str, revenue: float) -> FinancialStatementRecord:
    payload = {"report_date": report_date, "revenue": revenue}
    return FinancialStatementRecord(
        symbol="SH600519",
        company_name="Kweichow Moutai",
        statement_type=StatementType.INCOME_STATEMENT,
        report_date=report_date,
        fetched_at=fetched_at,
        source="test",
        raw_data=payload,
        normalized_data=payload,
        label_map={key: key for key in payload},
    )


def test_report_generator_creates_markdown_and_html(tmp_path: Path) -> None:
    baseline = _record("2024-12-31", "2026-04-24T10:00:00", 100.0)
    current = _record("2025-03-31", "2026-04-24T11:00:00", 150.0)
    comparison = ComparisonResult(current=current, baseline=baseline, baseline_kind="previous_period", diffs=[], alerts=[])
    summary = RunSummary(
        started_at="2026-04-24T10:00:00",
        completed_at="2026-04-24T10:05:00",
        inserted_records=2,
        skipped_records=0,
        comparisons=[comparison],
        errors=[],
    )

    paths = ReportGenerator(tmp_path).generate(summary)

    assert len(paths) == 2
    for path in paths:
        content = Path(path).read_text(encoding="utf-8")
        assert "Kweichow Moutai" in content
