from __future__ import annotations

from stcompare.config import DerivedRule, DiffSettings, MetricRule, RuleConfig
from stcompare.diff import DiffEngine
from stcompare.domain import FinancialStatementRecord, StatementType


def _record(report_date: str, data: dict[str, float]) -> FinancialStatementRecord:
    return FinancialStatementRecord(
        symbol="SH600519",
        company_name="Kweichow Moutai",
        statement_type=StatementType.INCOME_STATEMENT,
        report_date=report_date,
        fetched_at="2026-04-24T10:00:00",
        source="test",
        raw_data=data,
        normalized_data=data,
        label_map={key: key for key in data},
    )


def test_diff_engine_emits_alerts_for_large_changes() -> None:
    config = RuleConfig(
        diff=DiffSettings(min_change_ratio=0.1, min_absolute_change=10, max_diff_rows=10),
        aliases={
            "revenue": ["revenue"],
            "net_profit": ["net_profit"],
            "operating_cash_flow": ["operating_cash_flow"],
            "total_assets": ["total_assets"],
            "total_liabilities": ["total_liabilities"],
        },
        rules=[
            MetricRule(
                name="revenue_large_move",
                key="revenue",
                severity="warning",
                message="Revenue changed sharply.",
                change_ratio_gte=0.2,
            ),
            MetricRule(
                name="net_profit_negative_turn",
                key="net_profit",
                severity="critical",
                message="Net profit turned negative.",
                change_ratio_gte=0.2,
                negative_turn=True,
            ),
        ],
        derived_rules=[
            DerivedRule(
                name="debt_ratio_spike",
                severity="warning",
                numerator_key="total_liabilities",
                denominator_key="total_assets",
                message="Debt ratio changed sharply.",
                delta_gte=0.05,
            )
        ],
    )
    baseline = _record(
        "2024-12-31",
        {
            "revenue": 100.0,
            "net_profit": 50.0,
            "operating_cash_flow": 30.0,
            "total_assets": 400.0,
            "total_liabilities": 100.0,
        },
    )
    current = _record(
        "2025-03-31",
        {
            "revenue": 140.0,
            "net_profit": -10.0,
            "operating_cash_flow": 35.0,
            "total_assets": 420.0,
            "total_liabilities": 180.0,
        },
    )

    result = DiffEngine(config).compare(current, baseline, "previous_period")

    assert len(result.diffs) >= 3
    assert {alert.name for alert in result.alerts} == {
        "revenue_large_move",
        "net_profit_negative_turn",
        "debt_ratio_spike",
    }
