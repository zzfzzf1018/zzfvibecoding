from typing import List, Optional, Dict, Tuple
from models import Stock, FinancialStatements, StockPool, PEData
from data_access import DataSource
from calculators import CalculatorFactory, PECalculator, PBTCalculator
from datetime import date


class StockService:
    def __init__(self, data_source: DataSource):
        self.data_source = data_source

    def search_stocks(self, query: str) -> List[Stock]:
        return self.data_source.search_stocks(query)

    def get_stock_with_financials(self, code: str) -> Tuple[Optional[Stock], Optional[FinancialStatements]]:
        stock = self.data_source.get_stock_info(code)
        if not stock:
            return None, None
        financials = self.data_source.get_financial_statements(code)
        return stock, financials

    def calculate_pe(self, stock: Stock, financials: FinancialStatements, method: str) -> Optional[float]:
        calculator = CalculatorFactory.get_pe_calculator(method)
        if not calculator:
            return None
        pe_data = PEData(stock=stock, financials=financials)
        return calculator.calculate(pe_data)

    def calculate_pb(self, stock: Stock, financials: FinancialStatements, method: str) -> Optional[float]:
        calculator = CalculatorFactory.get_pb_calculator(method)
        if not calculator:
            return None
        pe_data = PEData(stock=stock, financials=financials)
        return calculator.calculate(pe_data)

    def get_all_pe_methods(self) -> Dict[str, str]:
        calculators = CalculatorFactory.get_pe_calculators()
        return {k: v.get_name() for k, v in calculators.items()}

    def get_all_pb_methods(self) -> Dict[str, str]:
        calculators = CalculatorFactory.get_pb_calculators()
        return {k: v.get_name() for k, v in calculators.items()}


class StockPoolService:
    def __init__(self, stock_service: StockService):
        self.stock_service = stock_service
        self.pools: Dict[str, StockPool] = {}

    def create_pool(self, name: str) -> StockPool:
        if name not in self.pools:
            self.pools[name] = StockPool(name=name)
        return self.pools[name]

    def get_pool(self, name: str) -> Optional[StockPool]:
        return self.pools.get(name)

    def list_pools(self) -> List[str]:
        return list(self.pools.keys())

    def add_stock_to_pool(self, pool_name: str, stock_code: str) -> bool:
        pool = self.pools.get(pool_name)
        if not pool:
            return False
        stock, financials = self.stock_service.get_stock_with_financials(stock_code)
        if stock and financials:
            pool.add_stock(stock, financials)
            return True
        return False

    def remove_stock_from_pool(self, pool_name: str, stock_code: str) -> bool:
        pool = self.pools.get(pool_name)
        if not pool:
            return False
        pool.remove_stock(stock_code)
        return True

    def calculate_pool_average_pe(self, pool_name: str, method: str) -> Optional[float]:
        pool = self.pools.get(pool_name)
        if not pool or not pool.stocks:
            return None

        pe_values = []
        for stock in pool.stocks:
            financials = pool.financials_map.get(stock.code)
            if financials:
                pe = self.stock_service.calculate_pe(stock, financials, method)
                if pe and pe > 0:
                    pe_values.append(pe)

        if not pe_values:
            return None
        return sum(pe_values) / len(pe_values)

    def calculate_pool_average_pb(self, pool_name: str, method: str) -> Optional[float]:
        pool = self.pools.get(pool_name)
        if not pool or not pool.stocks:
            return None

        pb_values = []
        for stock in pool.stocks:
            financials = pool.financials_map.get(stock.code)
            if financials:
                pb = self.stock_service.calculate_pb(stock, financials, method)
                if pb and pb > 0:
                    pb_values.append(pb)

        if not pb_values:
            return None
        return sum(pb_values) / len(pb_values)

    def get_pool_pe_details(self, pool_name: str, method: str) -> List[Tuple[str, str, Optional[float]]]:
        pool = self.pools.get(pool_name)
        if not pool:
            return []

        details = []
        for stock in pool.stocks:
            financials = pool.financials_map.get(stock.code)
            pe = None
            if financials:
                pe = self.stock_service.calculate_pe(stock, financials, method)
            details.append((stock.code, stock.name, pe))
        return details

    def get_pool_pb_details(self, pool_name: str, method: str) -> List[Tuple[str, str, Optional[float]]]:
        pool = self.pools.get(pool_name)
        if not pool:
            return []

        details = []
        for stock in pool.stocks:
            financials = pool.financials_map.get(stock.code)
            pb = None
            if financials:
                pb = self.stock_service.calculate_pb(stock, financials, method)
            details.append((stock.code, stock.name, pb))
        return details