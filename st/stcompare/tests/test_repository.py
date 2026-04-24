from __future__ import annotations

from pathlib import Path

from stcompare.domain import FinancialStatementRecord, StatementType
from stcompare.storage import StatementRepository


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


def test_repository_ignores_exact_duplicate(tmp_path: Path) -> None:
    repository = StatementRepository(tmp_path / "test.db")
    repository.init_schema()
    record = _record("2024-12-31", "2026-04-24T10:00:00", 100.0)

    assert repository.upsert_record(record) is True
    assert repository.upsert_record(record) is False


def test_repository_prefers_same_report_date_then_previous_period(tmp_path: Path) -> None:
    repository = StatementRepository(tmp_path / "test.db")
    repository.init_schema()

    original = _record("2024-12-31", "2026-04-24T10:00:00", 100.0)
    amended = _record("2024-12-31", "2026-04-24T11:00:00", 150.0)
    next_period = _record("2025-03-31", "2026-04-24T12:00:00", 180.0)

    repository.upsert_record(original)
    repository.upsert_record(amended)
    baseline, baseline_kind = repository.get_comparison_baseline(amended)
    assert baseline is not None
    assert baseline_kind == "same_report_date"
    assert baseline.fingerprint == original.fingerprint

    repository.upsert_record(next_period)
    baseline, baseline_kind = repository.get_comparison_baseline(next_period)
    assert baseline is not None
    assert baseline_kind == "previous_period"
    assert baseline.report_date == "2024-12-31"
