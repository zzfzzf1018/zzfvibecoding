from __future__ import annotations

from contextlib import closing
import json
from pathlib import Path
import sqlite3

from stcompare.domain import FinancialStatementRecord, StatementType


class StatementRepository:
    def __init__(self, database_path: str | Path) -> None:
        self.database_path = Path(database_path)
        self.database_path.parent.mkdir(parents=True, exist_ok=True)

    def init_schema(self) -> None:
        with sqlite3.connect(self.database_path) as connection:
            connection.execute(
                """
                CREATE TABLE IF NOT EXISTS statement_snapshots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    symbol TEXT NOT NULL,
                    company_name TEXT NOT NULL,
                    statement_type TEXT NOT NULL,
                    report_date TEXT NOT NULL,
                    fetched_at TEXT NOT NULL,
                    source TEXT NOT NULL,
                    fingerprint TEXT NOT NULL,
                    raw_data_json TEXT NOT NULL,
                    normalized_data_json TEXT NOT NULL,
                    label_map_json TEXT NOT NULL,
                    UNIQUE(symbol, statement_type, report_date, fingerprint)
                )
                """
            )
            connection.execute(
                """
                CREATE INDEX IF NOT EXISTS idx_statement_lookup
                ON statement_snapshots(symbol, statement_type, report_date, fetched_at)
                """
            )
            connection.commit()

    def upsert_record(self, record: FinancialStatementRecord) -> bool:
        payload = (
            record.symbol,
            record.company_name,
            record.statement_type.value,
            record.report_date,
            record.fetched_at,
            record.source,
            record.fingerprint,
            json.dumps(record.raw_data, ensure_ascii=False, default=str),
            json.dumps(record.normalized_data, ensure_ascii=False, default=str),
            json.dumps(record.label_map, ensure_ascii=False, default=str),
        )
        with sqlite3.connect(self.database_path) as connection:
            cursor = connection.execute(
                """
                INSERT OR IGNORE INTO statement_snapshots(
                    symbol,
                    company_name,
                    statement_type,
                    report_date,
                    fetched_at,
                    source,
                    fingerprint,
                    raw_data_json,
                    normalized_data_json,
                    label_map_json
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                payload,
            )
            connection.commit()
            return cursor.rowcount > 0

    def get_comparison_baseline(self, record: FinancialStatementRecord) -> tuple[FinancialStatementRecord | None, str | None]:
        same_report = self._fetch_one(
            """
            SELECT *
            FROM statement_snapshots
            WHERE symbol = ?
              AND statement_type = ?
              AND report_date = ?
              AND fingerprint <> ?
            ORDER BY fetched_at DESC
            LIMIT 1
            """,
            (record.symbol, record.statement_type.value, record.report_date, record.fingerprint),
        )
        if same_report is not None:
            return same_report, "same_report_date"

        previous_period = self._fetch_one(
            """
            SELECT *
            FROM statement_snapshots
            WHERE symbol = ?
              AND statement_type = ?
              AND report_date < ?
            ORDER BY report_date DESC, fetched_at DESC
            LIMIT 1
            """,
            (record.symbol, record.statement_type.value, record.report_date),
        )
        if previous_period is not None:
            return previous_period, "previous_period"
        return None, None

    def _fetch_one(self, query: str, params: tuple) -> FinancialStatementRecord | None:
        with sqlite3.connect(self.database_path) as connection:
            connection.row_factory = sqlite3.Row
            with closing(connection.execute(query, params)) as cursor:
                row = cursor.fetchone()
                if row is None:
                    return None
                return self._row_to_record(row)

    @staticmethod
    def _row_to_record(row: sqlite3.Row) -> FinancialStatementRecord:
        return FinancialStatementRecord(
            symbol=row["symbol"],
            company_name=row["company_name"],
            statement_type=StatementType(row["statement_type"]),
            report_date=row["report_date"],
            fetched_at=row["fetched_at"],
            source=row["source"],
            raw_data=json.loads(row["raw_data_json"]),
            normalized_data=json.loads(row["normalized_data_json"]),
            label_map=json.loads(row["label_map_json"]),
            fingerprint=row["fingerprint"],
        )
