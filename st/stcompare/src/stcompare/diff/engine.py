from __future__ import annotations

import math
from typing import Any

from stcompare.config import RuleConfig
from stcompare.domain import AlertItem, ComparisonResult, DiffItem, FinancialStatementRecord, normalize_key


class DiffEngine:
    def __init__(self, rule_config: RuleConfig) -> None:
        self.rule_config = rule_config

    def compare(
        self,
        current: FinancialStatementRecord,
        baseline: FinancialStatementRecord,
        baseline_kind: str,
    ) -> ComparisonResult:
        diffs = self._build_diffs(current, baseline)
        alerts = self._build_alerts(current, baseline)
        return ComparisonResult(
            current=current,
            baseline=baseline,
            baseline_kind=baseline_kind,
            diffs=diffs,
            alerts=alerts,
        )

    def _build_diffs(
        self,
        current: FinancialStatementRecord,
        baseline: FinancialStatementRecord,
    ) -> list[DiffItem]:
        keys = set(current.normalized_data) | set(baseline.normalized_data)
        diffs: list[DiffItem] = []
        for key in keys:
            previous_value = baseline.normalized_data.get(key)
            current_value = current.normalized_data.get(key)
            if previous_value == current_value:
                continue

            label = current.label_map.get(key) or baseline.label_map.get(key) or key
            diff_item = self._build_diff_item(key, label, previous_value, current_value)
            if diff_item is None:
                continue
            diffs.append(diff_item)

        diffs.sort(
            key=lambda item: (
                0 if item.change_kind == "numeric_change" else 1,
                abs(item.change_ratio) if item.change_ratio is not None else abs(item.delta or 0),
            ),
            reverse=True,
        )
        return diffs[: self.rule_config.diff.max_diff_rows]

    def _build_diff_item(
        self,
        key: str,
        label: str,
        previous_value: Any,
        current_value: Any,
    ) -> DiffItem | None:
        if self._is_number(previous_value) and self._is_number(current_value):
            delta = float(current_value) - float(previous_value)
            change_ratio = None if float(previous_value) == 0 else delta / abs(float(previous_value))
            if not self._is_meaningful_numeric_change(delta, change_ratio):
                return None
            return DiffItem(
                key=key,
                label=label,
                previous_value=previous_value,
                current_value=current_value,
                delta=delta,
                change_ratio=change_ratio,
                change_kind="numeric_change",
            )

        change_kind = "text_change"
        if previous_value is None and current_value is not None:
            change_kind = "value_added"
        elif previous_value is not None and current_value is None:
            change_kind = "value_removed"

        return DiffItem(
            key=key,
            label=label,
            previous_value=previous_value,
            current_value=current_value,
            delta=None,
            change_ratio=None,
            change_kind=change_kind,
        )

    def _build_alerts(
        self,
        current: FinancialStatementRecord,
        baseline: FinancialStatementRecord,
    ) -> list[AlertItem]:
        alerts: list[AlertItem] = []
        for rule in self.rule_config.rules:
            previous_value = self._find_metric_value(baseline.normalized_data, rule.key)
            current_value = self._find_metric_value(current.normalized_data, rule.key)
            if previous_value is None or current_value is None:
                continue

            delta = current_value - previous_value
            change_ratio = None if previous_value == 0 else delta / abs(previous_value)
            negative_turn_hit = rule.negative_turn and previous_value > 0 and current_value < 0
            ratio_hit = change_ratio is not None and abs(change_ratio) >= rule.change_ratio_gte
            if not ratio_hit and not negative_turn_hit:
                continue
            alerts.append(
                AlertItem(
                    name=rule.name,
                    severity=rule.severity,
                    message=rule.message,
                    key=rule.key,
                    previous_value=previous_value,
                    current_value=current_value,
                    delta=delta,
                    change_ratio=change_ratio,
                )
            )

        for rule in self.rule_config.derived_rules:
            previous_value = self._compute_ratio(baseline.normalized_data, rule.numerator_key, rule.denominator_key)
            current_value = self._compute_ratio(current.normalized_data, rule.numerator_key, rule.denominator_key)
            if previous_value is None or current_value is None:
                continue
            delta = current_value - previous_value
            if abs(delta) < rule.delta_gte:
                continue
            alerts.append(
                AlertItem(
                    name=rule.name,
                    severity=rule.severity,
                    message=rule.message,
                    key=f"{rule.numerator_key}/{rule.denominator_key}",
                    previous_value=previous_value,
                    current_value=current_value,
                    delta=delta,
                    change_ratio=delta,
                )
            )

        return alerts

    def _find_metric_value(self, data: dict[str, Any], metric_key: str) -> float | None:
        candidates = [normalize_key(metric_key)]
        candidates.extend(normalize_key(alias) for alias in self.rule_config.aliases.get(metric_key, []))

        for candidate in candidates:
            if candidate in data and self._is_number(data[candidate]):
                return float(data[candidate])

        for data_key, value in data.items():
            if not self._is_number(value):
                continue
            if any(candidate in data_key or data_key in candidate for candidate in candidates):
                return float(value)
        return None

    def _compute_ratio(self, data: dict[str, Any], numerator_key: str, denominator_key: str) -> float | None:
        numerator = self._find_metric_value(data, numerator_key)
        denominator = self._find_metric_value(data, denominator_key)
        if numerator is None or denominator in {None, 0}:
            return None
        return numerator / denominator

    def _is_meaningful_numeric_change(self, delta: float, change_ratio: float | None) -> bool:
        if abs(delta) >= self.rule_config.diff.min_absolute_change:
            return True
        if change_ratio is not None and abs(change_ratio) >= self.rule_config.diff.min_change_ratio:
            return True
        return False

    @staticmethod
    def _is_number(value: Any) -> bool:
        return isinstance(value, (int, float)) and not isinstance(value, bool) and not math.isnan(float(value))
