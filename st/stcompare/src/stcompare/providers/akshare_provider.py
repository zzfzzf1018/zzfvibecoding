from __future__ import annotations

from datetime import datetime
import logging
import time

import akshare as ak
import pandas as pd

from stcompare.config import CompanyConfig, ProviderSettings
from stcompare.domain import FinancialStatementRecord, StatementType, normalize_record_data
from stcompare.providers.finance_provider import FinanceProvider


LOGGER = logging.getLogger(__name__)


class AkshareFinanceProvider(FinanceProvider):
    FUNCTION_MAP = {
        StatementType.BALANCE_SHEET: ak.stock_balance_sheet_by_report_em,
        StatementType.INCOME_STATEMENT: ak.stock_profit_sheet_by_report_em,
        StatementType.CASH_FLOW_STATEMENT: ak.stock_cash_flow_sheet_by_report_em,
    }

    def __init__(self, settings: ProviderSettings) -> None:
        super().__init__(settings)

    def fetch_records(self, company: CompanyConfig) -> list[FinancialStatementRecord]:
        records: list[FinancialStatementRecord] = []
        for statement_type, function in self.FUNCTION_MAP.items():
            frame = self._call_with_retry(company.symbol, statement_type.value, function)
            if frame.empty:
                LOGGER.warning("No rows returned for %s %s", company.symbol, statement_type.value)
                continue
            records.extend(self._frame_to_records(company, statement_type, frame))
            time.sleep(self.settings.request_pause_seconds)
        return records

    def _call_with_retry(self, symbol: str, statement_name: str, function) -> pd.DataFrame:
        last_error: Exception | None = None
        for attempt in range(1, self.settings.retry_attempts + 1):
            try:
                return function(symbol=symbol)
            except Exception as exc:  # noqa: BLE001
                last_error = exc
                LOGGER.warning(
                    "Fetch failed for %s %s on attempt %s/%s: %s",
                    symbol,
                    statement_name,
                    attempt,
                    self.settings.retry_attempts,
                    exc,
                )
                if attempt < self.settings.retry_attempts:
                    time.sleep(self.settings.retry_backoff_seconds * attempt)
        raise RuntimeError(f"Failed to fetch {statement_name} for {symbol}: {last_error}") from last_error

    def _frame_to_records(
        self,
        company: CompanyConfig,
        statement_type: StatementType,
        frame: pd.DataFrame,
    ) -> list[FinancialStatementRecord]:
        records: list[FinancialStatementRecord] = []
        fetched_at = datetime.now().isoformat(timespec="seconds")
        for row in frame.to_dict(orient="records"):
            raw_data = {str(key): value for key, value in row.items()}
            normalized_data, label_map = normalize_record_data(raw_data)
            report_date = self._extract_report_date(normalized_data)
            records.append(
                FinancialStatementRecord(
                    symbol=company.symbol,
                    company_name=company.name,
                    statement_type=statement_type,
                    report_date=report_date,
                    fetched_at=fetched_at,
                    source="akshare",
                    raw_data=raw_data,
                    normalized_data=normalized_data,
                    label_map=label_map,
                )
            )
        return records

    @staticmethod
    def _extract_report_date(normalized_data: dict[str, object]) -> str:
        for key in ("report_date", "reportdate", "end_date", "enddate"):
            value = normalized_data.get(key)
            if value:
                return str(value)
        raise ValueError("Could not identify report date column in fetched data")
