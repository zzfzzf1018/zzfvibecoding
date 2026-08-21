from dataclasses import dataclass, field
from typing import Optional, Dict, List
from datetime import date


@dataclass
class Stock:
    code: str
    name: str
    industry: Optional[str] = None
    sector: Optional[str] = None
    list_date: Optional[date] = None
    total_shares: Optional[float] = None
    circulating_shares: Optional[float] = None
    market_cap: Optional[float] = None
    price: Optional[float] = None
    pe_ttm: Optional[float] = None
    pe_dynamic: Optional[float] = None
    pe_static: Optional[float] = None
    pb: Optional[float] = None


@dataclass
class BalanceSheet:
    report_date: date
    total_assets: Optional[float] = None
    total_liabilities: Optional[float] = None
    total_equity: Optional[float] = None
    cash_and_equivalents: Optional[float] = None
    short_term_debt: Optional[float] = None
    long_term_debt: Optional[float] = None
    inventory: Optional[float] = None


@dataclass
class IncomeStatement:
    report_date: date
    revenue: Optional[float] = None
    operating_profit: Optional[float] = None
    net_profit: Optional[float] = None
    net_profit_attributable: Optional[float] = None
    eps: Optional[float] = None


@dataclass
class CashFlowStatement:
    report_date: date
    operating_cash_flow: Optional[float] = None
    investing_cash_flow: Optional[float] = None
    financing_cash_flow: Optional[float] = None
    net_cash_flow: Optional[float] = None


@dataclass
class FinancialStatements:
    stock_code: str
    balance_sheets: List[BalanceSheet] = field(default_factory=list)
    income_statements: List[IncomeStatement] = field(default_factory=list)
    cash_flow_statements: List[CashFlowStatement] = field(default_factory=list)

    def get_latest_balance_sheet(self) -> Optional[BalanceSheet]:
        if not self.balance_sheets:
            return None
        return max(self.balance_sheets, key=lambda x: x.report_date)

    def get_latest_income_statement(self) -> Optional[IncomeStatement]:
        if not self.income_statements:
            return None
        return max(self.income_statements, key=lambda x: x.report_date)

    def get_ttm_net_profit(self) -> Optional[float]:
        sorted_income = sorted(self.income_statements, key=lambda x: x.report_date, reverse=True)
        if len(sorted_income) < 4:
            return None
        return sum(inc.net_profit_attributable or 0 for inc in sorted_income[:4])


@dataclass
class PEData:
    stock: Stock
    financials: FinancialStatements
    calculation_date: date = field(default_factory=date.today)


@dataclass
class StockPool:
    name: str
    stocks: List[Stock] = field(default_factory=list)
    financials_map: Dict[str, FinancialStatements] = field(default_factory=dict)

    def add_stock(self, stock: Stock, financials: FinancialStatements):
        if stock.code not in [s.code for s in self.stocks]:
            self.stocks.append(stock)
            self.financials_map[stock.code] = financials

    def remove_stock(self, stock_code: str):
        self.stocks = [s for s in self.stocks if s.code != stock_code]
        self.financials_map.pop(stock_code, None)

    def get_stock_codes(self) -> List[str]:
        return [s.code for s in self.stocks]