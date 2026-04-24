from __future__ import annotations

from abc import ABC, abstractmethod

from stcompare.config import CompanyConfig, ProviderSettings
from stcompare.domain import FinancialStatementRecord


class FinanceProvider(ABC):
    def __init__(self, settings: ProviderSettings) -> None:
        self.settings = settings

    @abstractmethod
    def fetch_records(self, company: CompanyConfig) -> list[FinancialStatementRecord]:
        raise NotImplementedError
