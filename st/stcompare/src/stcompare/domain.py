from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, datetime
from enum import Enum
import hashlib
import json
import math
import re
from typing import Any


class StatementType(str, Enum):
    BALANCE_SHEET = "balance_sheet"
    INCOME_STATEMENT = "income_statement"
    CASH_FLOW_STATEMENT = "cash_flow_statement"


def normalize_key(raw_key: str) -> str:
    lowered = str(raw_key).strip().lower()
    normalized = re.sub(r"\W+", "_", lowered, flags=re.UNICODE)
    normalized = re.sub(r"_+", "_", normalized).strip("_")
    return normalized or "unknown_field"


def coerce_value(value: Any) -> Any:
    if value is None:
        return None

    if isinstance(value, (int, float)):
        if isinstance(value, float) and math.isnan(value):
            return None
        return value

    if isinstance(value, (datetime, date)):
        return value.isoformat()

    text = str(value).strip()
    if text in {"", "--", "nan", "None", "null"}:
        return None

    normalized = text.replace(",", "")
    if normalized.endswith("%"):
        try:
            return float(normalized[:-1]) / 100.0
        except ValueError:
            return text

    try:
        if "." not in normalized and normalized.isdigit():
            return int(normalized)
        return float(normalized)
    except ValueError:
        return text


def normalize_record_data(raw_data: dict[str, Any]) -> tuple[dict[str, Any], dict[str, str]]:
    normalized_data: dict[str, Any] = {}
    label_map: dict[str, str] = {}

    for key, value in raw_data.items():
        normalized_key = normalize_key(key)
        normalized_data[normalized_key] = coerce_value(value)
        label_map[normalized_key] = str(key)

    return normalized_data, label_map


def compute_fingerprint(data: dict[str, Any]) -> str:
    payload = json.dumps(data, ensure_ascii=False, sort_keys=True, default=str)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


@dataclass(slots=True)
class FinancialStatementRecord:
    symbol: str
    company_name: str
    statement_type: StatementType
    report_date: str
    fetched_at: str
    source: str
    raw_data: dict[str, Any]
    normalized_data: dict[str, Any]
    label_map: dict[str, str]
    fingerprint: str = field(default="")

    def __post_init__(self) -> None:
        if not self.fingerprint:
            self.fingerprint = compute_fingerprint(self.normalized_data)


@dataclass(slots=True)
class DiffItem:
    key: str
    label: str
    previous_value: Any
    current_value: Any
    delta: float | None
    change_ratio: float | None
    change_kind: str


@dataclass(slots=True)
class AlertItem:
    name: str
    severity: str
    message: str
    key: str | None = None
    previous_value: float | None = None
    current_value: float | None = None
    delta: float | None = None
    change_ratio: float | None = None


@dataclass(slots=True)
class ComparisonResult:
    current: FinancialStatementRecord
    baseline: FinancialStatementRecord
    baseline_kind: str
    diffs: list[DiffItem]
    alerts: list[AlertItem]


@dataclass(slots=True)
class RunError:
    symbol: str
    company_name: str
    stage: str
    message: str


@dataclass(slots=True)
class RunSummary:
    started_at: str
    completed_at: str
    inserted_records: int
    skipped_records: int
    comparisons: list[ComparisonResult]
    errors: list[RunError]
    report_paths: list[str] = field(default_factory=list)
