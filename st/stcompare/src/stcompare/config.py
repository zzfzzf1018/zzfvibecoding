from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import yaml


@dataclass(slots=True)
class CompanyConfig:
    symbol: str
    name: str
    enabled: bool = True


@dataclass(slots=True)
class AppSettings:
    database_path: Path
    report_dir: Path
    timezone: str = "Asia/Shanghai"
    log_level: str = "INFO"


@dataclass(slots=True)
class ProviderSettings:
    name: str = "akshare"
    retry_attempts: int = 3
    retry_backoff_seconds: float = 2.0
    request_pause_seconds: float = 0.5


@dataclass(slots=True)
class ScheduleSettings:
    mode: str = "interval"
    minutes: int = 720
    cron: str | None = None
    max_instances: int = 1
    coalesce: bool = True


@dataclass(slots=True)
class AppConfig:
    root_dir: Path
    app: AppSettings
    provider: ProviderSettings
    schedule: ScheduleSettings
    watchlist: list[CompanyConfig]


@dataclass(slots=True)
class DiffSettings:
    min_change_ratio: float = 0.2
    min_absolute_change: float = 1_000_000
    max_diff_rows: int = 30


@dataclass(slots=True)
class MetricRule:
    name: str
    key: str
    severity: str
    message: str
    change_ratio_gte: float = 0.2
    negative_turn: bool = False


@dataclass(slots=True)
class DerivedRule:
    name: str
    severity: str
    numerator_key: str
    denominator_key: str
    message: str
    delta_gte: float = 0.08


@dataclass(slots=True)
class RuleConfig:
    diff: DiffSettings
    aliases: dict[str, list[str]]
    rules: list[MetricRule]
    derived_rules: list[DerivedRule]


def _read_yaml(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        return yaml.safe_load(handle) or {}


def _resolve_path(root_dir: Path, raw_path: str) -> Path:
    path = Path(raw_path)
    if path.is_absolute():
        return path
    return (root_dir / path).resolve()


def load_app_config(config_path: str | Path) -> AppConfig:
    path = Path(config_path).resolve()
    root_dir = path.parent.parent
    payload = _read_yaml(path)

    app_payload = payload.get("app", {})
    provider_payload = payload.get("provider", {})
    schedule_payload = payload.get("schedule", {})
    watchlist_payload = payload.get("watchlist", [])

    app = AppSettings(
        database_path=_resolve_path(root_dir, app_payload.get("database_path", "data/stcompare.db")),
        report_dir=_resolve_path(root_dir, app_payload.get("report_dir", "reports")),
        timezone=app_payload.get("timezone", "Asia/Shanghai"),
        log_level=str(app_payload.get("log_level", "INFO")).upper(),
    )
    provider = ProviderSettings(
        name=provider_payload.get("name", "akshare"),
        retry_attempts=int(provider_payload.get("retry_attempts", 3)),
        retry_backoff_seconds=float(provider_payload.get("retry_backoff_seconds", 2.0)),
        request_pause_seconds=float(provider_payload.get("request_pause_seconds", 0.5)),
    )
    schedule = ScheduleSettings(
        mode=schedule_payload.get("mode", "interval"),
        minutes=int(schedule_payload.get("minutes", 720)),
        cron=schedule_payload.get("cron"),
        max_instances=int(schedule_payload.get("max_instances", 1)),
        coalesce=bool(schedule_payload.get("coalesce", True)),
    )
    watchlist = [
        CompanyConfig(
            symbol=item["symbol"],
            name=item.get("name", item["symbol"]),
            enabled=bool(item.get("enabled", True)),
        )
        for item in watchlist_payload
    ]
    return AppConfig(root_dir=root_dir, app=app, provider=provider, schedule=schedule, watchlist=watchlist)


def load_rule_config(config_path: str | Path) -> RuleConfig:
    payload = _read_yaml(Path(config_path).resolve())

    diff_payload = payload.get("diff", {})
    rules_payload = payload.get("rules", [])
    derived_payload = payload.get("derived_rules", [])
    aliases_payload = payload.get("aliases", {})

    diff = DiffSettings(
        min_change_ratio=float(diff_payload.get("min_change_ratio", 0.2)),
        min_absolute_change=float(diff_payload.get("min_absolute_change", 1_000_000)),
        max_diff_rows=int(diff_payload.get("max_diff_rows", 30)),
    )
    rules = [
        MetricRule(
            name=item["name"],
            key=item["key"],
            severity=item.get("severity", "warning"),
            message=item.get("message", item["name"]),
            change_ratio_gte=float(item.get("change_ratio_gte", 0.2)),
            negative_turn=bool(item.get("negative_turn", False)),
        )
        for item in rules_payload
    ]
    derived_rules = [
        DerivedRule(
            name=item["name"],
            severity=item.get("severity", "warning"),
            numerator_key=item["numerator_key"],
            denominator_key=item["denominator_key"],
            message=item.get("message", item["name"]),
            delta_gte=float(item.get("delta_gte", 0.08)),
        )
        for item in derived_payload
    ]
    aliases = {str(key): [str(value) for value in values] for key, values in aliases_payload.items()}
    return RuleConfig(diff=diff, aliases=aliases, rules=rules, derived_rules=derived_rules)
