from abc import ABC, abstractmethod
from typing import Optional, Dict
from models import Stock, FinancialStatements, PEData
from datetime import date


class PECalculator(ABC):
    @abstractmethod
    def calculate(self, data: PEData) -> Optional[float]:
        pass

    @abstractmethod
    def get_name(self) -> str:
        pass


class PBTCalculator(ABC):
    @abstractmethod
    def calculate(self, data: PEData) -> Optional[float]:
        pass

    @abstractmethod
    def get_name(self) -> str:
        pass


class TTMPECalculator(PECalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        ttm_profit = financials.get_ttm_net_profit()
        if ttm_profit and ttm_profit > 0 and stock.market_cap:
            return stock.market_cap / ttm_profit
        if stock.pe_ttm:
            return stock.pe_ttm
        return None

    def get_name(self) -> str:
        return "TTM PE"


class StaticPECalculator(PECalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        latest_income = financials.get_latest_income_statement()
        if latest_income and latest_income.net_profit_attributable and latest_income.net_profit_attributable > 0 and stock.market_cap:
            return stock.market_cap / latest_income.net_profit_attributable
        if stock.pe_static:
            return stock.pe_static
        return None

    def get_name(self) -> str:
        return "静态PE"


class DynamicPECalculator(PECalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        ttm_profit = financials.get_ttm_net_profit()
        if ttm_profit and ttm_profit > 0 and stock.price and stock.total_shares:
            eps_ttm = ttm_profit / stock.total_shares
            if eps_ttm > 0:
                return stock.price / eps_ttm
        if stock.pe_dynamic:
            return stock.pe_dynamic
        return None

    def get_name(self) -> str:
        return "动态PE"


class CashAdjustedPECalculator(PECalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        latest_bs = financials.get_latest_balance_sheet()
        ttm_profit = financials.get_ttm_net_profit()
        if (ttm_profit and ttm_profit > 0 and 
            stock.market_cap and latest_bs and latest_bs.cash_and_equivalents):
            adjusted_market_cap = stock.market_cap - latest_bs.cash_and_equivalents
            if adjusted_market_cap > 0:
                return adjusted_market_cap / ttm_profit
        return None

    def get_name(self) -> str:
        return "扣除现金PE"


class DebtIncludedPECalculator(PECalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        latest_bs = financials.get_latest_balance_sheet()
        ttm_profit = financials.get_ttm_net_profit()
        if (ttm_profit and ttm_profit > 0 and 
            stock.market_cap and latest_bs and latest_bs.total_liabilities):
            enterprise_value = stock.market_cap + latest_bs.total_liabilities
            if latest_bs.cash_and_equivalents:
                enterprise_value -= latest_bs.cash_and_equivalents
            if enterprise_value > 0:
                return enterprise_value / ttm_profit
        return None

    def get_name(self) -> str:
        return "算上负债PE"


class BasicPBCalculator(PBTCalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        latest_bs = financials.get_latest_balance_sheet()
        if stock.market_cap and latest_bs and latest_bs.total_equity:
            return stock.market_cap / latest_bs.total_equity
        if stock.pb:
            return stock.pb
        return None

    def get_name(self) -> str:
        return "基本PB"


class TangiblePBCalculator(PBTCalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        latest_bs = financials.get_latest_balance_sheet()
        if stock.market_cap and latest_bs and latest_bs.total_equity:
            tangible_equity = latest_bs.total_equity
            if tangible_equity > 0:
                return stock.market_cap / tangible_equity
        return None

    def get_name(self) -> str:
        return "有形PB"


class CashAdjustedPBCalculator(PBTCalculator):
    def calculate(self, data: PEData) -> Optional[float]:
        stock = data.stock
        financials = data.financials
        latest_bs = financials.get_latest_balance_sheet()
        if (stock.market_cap and latest_bs and 
            latest_bs.total_equity and latest_bs.cash_and_equivalents):
            adjusted_equity = latest_bs.total_equity + latest_bs.cash_and_equivalents
            if adjusted_equity > 0:
                return stock.market_cap / adjusted_equity
        return None

    def get_name(self) -> str:
        return "扣除现金PB"


class CalculatorFactory:
    @staticmethod
    def get_pe_calculators() -> Dict[str, PECalculator]:
        return {
            "ttm": TTMPECalculator(),
            "static": StaticPECalculator(),
            "dynamic": DynamicPECalculator(),
            "cash": CashAdjustedPECalculator(),
            "debt": DebtIncludedPECalculator(),
        }

    @staticmethod
    def get_pb_calculators() -> Dict[str, PBTCalculator]:
        return {
            "basic": BasicPBCalculator(),
            "tangible": TangiblePBCalculator(),
            "cash": CashAdjustedPBCalculator(),
        }

    @staticmethod
    def get_pe_calculator(key: str) -> Optional[PECalculator]:
        return CalculatorFactory.get_pe_calculators().get(key)

    @staticmethod
    def get_pb_calculator(key: str) -> Optional[PBTCalculator]:
        return CalculatorFactory.get_pb_calculators().get(key)