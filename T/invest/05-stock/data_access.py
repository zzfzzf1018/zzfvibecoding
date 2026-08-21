import akshare as ak
import pandas as pd
import numpy as np
from datetime import date, timedelta
from typing import List, Optional, Tuple
from sqlalchemy import create_engine, MetaData, Table, Column, String, Float, Date, Integer, text
from sqlalchemy.orm import sessionmaker
from models import Stock, BalanceSheet, IncomeStatement, CashFlowStatement, FinancialStatements


class DataSource:
    def search_stocks(self, query: str) -> List[Stock]:
        raise NotImplementedError

    def get_stock_info(self, code: str) -> Optional[Stock]:
        raise NotImplementedError

    def get_financial_statements(self, code: str) -> Optional[FinancialStatements]:
        raise NotImplementedError


class AkShareDataSource(DataSource):
    def __init__(self):
        self.stock_info_cache: List[Stock] = []

    def _ensure_stock_list_cached(self):
        if not self.stock_info_cache:
            try:
                stock_df = ak.stock_info_a_code_name()
                for _, row in stock_df.iterrows():
                    stock = Stock(code=row['code'], name=row['name'])
                    self.stock_info_cache.append(stock)
            except Exception:
                pass

    def search_stocks(self, query: str) -> List[Stock]:
        self._ensure_stock_list_cached()
        query_upper = query.upper()
        results = []
        for stock in self.stock_info_cache:
            if (query_upper in stock.code.upper() or 
                query_upper in stock.name.upper()):
                results.append(stock)
        return results[:20]

    def get_stock_info(self, code: str) -> Optional[Stock]:
        try:
            code = self._normalize_code(code)
            stock_zh_a_spot_df = ak.stock_zh_a_spot_em()
            mask = stock_zh_a_spot_df['代码'] == code
            if mask.any():
                row = stock_zh_a_spot_df[mask].iloc[0]
                stock = Stock(
                    code=code,
                    name=row.get('名称', ''),
                    price=row.get('最新价'),
                    pe_ttm=row.get('市盈率'),
                    pb=row.get('市净率')
                )
                return stock

            stock_info_df = ak.stock_zh_a_latest_quote()
            mask = stock_info_df['stock'] == code
            if mask.any():
                row = stock_info_df[mask].iloc[0]
                stock = Stock(
                    code=code,
                    name=row.get('name', ''),
                    price=row.get('price')
                )
                return stock
        except Exception:
            pass
        return None

    def _normalize_code(self, code: str) -> str:
        code = code.strip()
        if len(code) == 6:
            return code
        if code.startswith('sh') or code.startswith('sz'):
            return code[2:]
        if code.startswith('SH') or code.startswith('SZ'):
            return code[2:]
        return code

    def get_financial_statements(self, code: str) -> Optional[FinancialStatements]:
        try:
            code = self._normalize_code(code)
            financials = FinancialStatements(stock_code=code)

            try:
                balance_df = ak.stock_financial_report_sina(stock=code, symbol='资产负债表')
                if not balance_df.empty:
                    for _, row in balance_df.iterrows():
                        try:
                            report_date = pd.to_datetime(row.get('报告期')).date()
                            bs = BalanceSheet(
                                report_date=report_date,
                                total_assets=row.get('资产总计'),
                                total_liabilities=row.get('负债合计'),
                                total_equity=row.get('所有者权益合计'),
                                cash_and_equivalents=row.get('货币资金')
                            )
                            financials.balance_sheets.append(bs)
                        except Exception:
                            continue
            except Exception:
                pass

            try:
                income_df = ak.stock_financial_report_sina(stock=code, symbol='利润表')
                if not income_df.empty:
                    for _, row in income_df.iterrows():
                        try:
                            report_date = pd.to_datetime(row.get('报告期')).date()
                            inc = IncomeStatement(
                                report_date=report_date,
                                revenue=row.get('营业总收入'),
                                operating_profit=row.get('营业利润'),
                                net_profit=row.get('净利润'),
                                net_profit_attributable=row.get('归属于母公司所有者的净利润'),
                                eps=row.get('基本每股收益')
                            )
                            financials.income_statements.append(inc)
                        except Exception:
                            continue
            except Exception:
                pass

            try:
                cash_df = ak.stock_financial_report_sina(stock=code, symbol='现金流量表')
                if not cash_df.empty:
                    for _, row in cash_df.iterrows():
                        try:
                            report_date = pd.to_datetime(row.get('报告期')).date()
                            cf = CashFlowStatement(
                                report_date=report_date,
                                operating_cash_flow=row.get('经营活动产生的现金流量净额'),
                                investing_cash_flow=row.get('投资活动产生的现金流量净额'),
                                financing_cash_flow=row.get('筹资活动产生的现金流量净额'),
                                net_cash_flow=row.get('现金及现金等价物净增加额')
                            )
                            financials.cash_flow_statements.append(cf)
                        except Exception:
                            continue
            except Exception:
                pass

            if not financials.balance_sheets and not financials.income_statements:
                try:
                    analysis_df = ak.stock_financial_analysis_indicator(code=code)
                    if not analysis_df.empty:
                        for _, row in analysis_df.iterrows():
                            try:
                                report_date = pd.to_datetime(row.get('日期')).date()
                                bs = BalanceSheet(
                                    report_date=report_date,
                                    total_assets=row.get('总资产'),
                                    total_liabilities=row.get('总负债'),
                                    total_equity=row.get('净资产')
                                )
                                financials.balance_sheets.append(bs)

                                inc = IncomeStatement(
                                    report_date=report_date,
                                    revenue=row.get('营业收入'),
                                    operating_profit=row.get('营业利润'),
                                    net_profit=row.get('净利润'),
                                    net_profit_attributable=row.get('归属母公司股东的净利润'),
                                    eps=row.get('每股收益')
                                )
                                financials.income_statements.append(inc)
                            except Exception:
                                continue
                except Exception:
                    pass

            return financials
        except Exception:
            return None


class SQLiteCache:
    def __init__(self, db_path: str = 'stock_data.db'):
        self.engine = create_engine(f'sqlite:///{db_path}')
        self.Session = sessionmaker(bind=self.engine)
        self._create_tables()

    def _create_tables(self):
        metadata = MetaData()

        Table('stocks', metadata,
              Column('code', String(10), primary_key=True),
              Column('name', String(100)),
              Column('industry', String(100)),
              Column('sector', String(100)),
              Column('list_date', Date),
              Column('total_shares', Float),
              Column('circulating_shares', Float),
              Column('market_cap', Float),
              Column('price', Float),
              Column('pe_ttm', Float),
              Column('pe_dynamic', Float),
              Column('pe_static', Float),
              Column('pb', Float),
              Column('update_time', Date))

        Table('balance_sheets', metadata,
              Column('id', Integer, primary_key=True, autoincrement=True),
              Column('stock_code', String(10)),
              Column('report_date', Date),
              Column('total_assets', Float),
              Column('total_liabilities', Float),
              Column('total_equity', Float),
              Column('cash_and_equivalents', Float),
              Column('short_term_debt', Float),
              Column('long_term_debt', Float),
              Column('inventory', Float))

        Table('income_statements', metadata,
              Column('id', Integer, primary_key=True, autoincrement=True),
              Column('stock_code', String(10)),
              Column('report_date', Date),
              Column('revenue', Float),
              Column('operating_profit', Float),
              Column('net_profit', Float),
              Column('net_profit_attributable', Float),
              Column('eps', Float))

        Table('cash_flow_statements', metadata,
              Column('id', Integer, primary_key=True, autoincrement=True),
              Column('stock_code', String(10)),
              Column('report_date', Date),
              Column('operating_cash_flow', Float),
              Column('investing_cash_flow', Float),
              Column('financing_cash_flow', Float),
              Column('net_cash_flow', Float))

        metadata.create_all(self.engine)

    def save_stock(self, stock: Stock):
        session = self.Session()
        try:
            stock_fields = ['code', 'name', 'industry', 'sector', 'list_date', 
                           'total_shares', 'circulating_shares', 'market_cap', 
                           'price', 'pe_ttm', 'pe_dynamic', 'pe_static', 'pb']
            stock_dict = {k: v for k, v in stock.__dict__.items() if k in stock_fields and v is not None}
            stock_dict['update_time'] = date.today()
            session.execute(
                text(f"INSERT OR REPLACE INTO stocks ({', '.join(stock_dict.keys())}) VALUES ({', '.join([':' + k for k in stock_dict.keys()])})"),
                stock_dict
            )
            session.commit()
        finally:
            session.close()

    def get_stock(self, code: str) -> Optional[Stock]:
        session = self.Session()
        try:
            result = session.execute(
                text("SELECT * FROM stocks WHERE code = :code AND update_time >= :date"),
                {"code": code, "date": date.today() - timedelta(days=7)}
            ).fetchone()
            if result:
                stock_fields = ['code', 'name', 'industry', 'sector', 'list_date', 
                               'total_shares', 'circulating_shares', 'market_cap', 
                               'price', 'pe_ttm', 'pe_dynamic', 'pe_static', 'pb']
                mapping = result._mapping
                filtered = {k: mapping[k] for k in stock_fields if k in mapping and mapping[k] is not None}
                return Stock(**filtered)
        finally:
            session.close()
        return None

    def save_financials(self, financials: FinancialStatements):
        session = self.Session()
        try:
            bs_fields = ['report_date', 'total_assets', 'total_liabilities', 'total_equity',
                         'cash_and_equivalents', 'short_term_debt', 'long_term_debt', 'inventory']
            for bs in financials.balance_sheets:
                bs_dict = {k: v for k, v in bs.__dict__.items() if k in bs_fields and v is not None}
                bs_dict['stock_code'] = financials.stock_code
                session.execute(
                    text(f"INSERT OR REPLACE INTO balance_sheets ({', '.join(bs_dict.keys())}) VALUES ({', '.join([':' + k for k in bs_dict.keys()])})"),
                    bs_dict
                )

            inc_fields = ['report_date', 'revenue', 'operating_profit', 'net_profit',
                          'net_profit_attributable', 'eps']
            for inc in financials.income_statements:
                inc_dict = {k: v for k, v in inc.__dict__.items() if k in inc_fields and v is not None}
                inc_dict['stock_code'] = financials.stock_code
                session.execute(
                    text(f"INSERT OR REPLACE INTO income_statements ({', '.join(inc_dict.keys())}) VALUES ({', '.join([':' + k for k in inc_dict.keys()])})"),
                    inc_dict
                )

            cf_fields = ['report_date', 'operating_cash_flow', 'investing_cash_flow',
                         'financing_cash_flow', 'net_cash_flow']
            for cf in financials.cash_flow_statements:
                cf_dict = {k: v for k, v in cf.__dict__.items() if k in cf_fields and v is not None}
                cf_dict['stock_code'] = financials.stock_code
                session.execute(
                    text(f"INSERT OR REPLACE INTO cash_flow_statements ({', '.join(cf_dict.keys())}) VALUES ({', '.join([':' + k for k in cf_dict.keys()])})"),
                    cf_dict
                )
            session.commit()
        finally:
            session.close()

    def get_financials(self, code: str) -> Optional[FinancialStatements]:
        session = self.Session()
        try:
            bs_results = session.execute(
                text("SELECT * FROM balance_sheets WHERE stock_code = :code"),
                {"code": code}
            ).fetchall()
            inc_results = session.execute(
                text("SELECT * FROM income_statements WHERE stock_code = :code"),
                {"code": code}
            ).fetchall()
            cf_results = session.execute(
                text("SELECT * FROM cash_flow_statements WHERE stock_code = :code"),
                {"code": code}
            ).fetchall()

            if bs_results or inc_results or cf_results:
                financials = FinancialStatements(stock_code=code)
                
                bs_fields = ['report_date', 'total_assets', 'total_liabilities', 'total_equity',
                             'cash_and_equivalents', 'short_term_debt', 'long_term_debt', 'inventory']
                for r in bs_results:
                    mapping = r._mapping
                    filtered = {k: mapping[k] for k in bs_fields if k in mapping and mapping[k] is not None}
                    financials.balance_sheets.append(BalanceSheet(**filtered))

                inc_fields = ['report_date', 'revenue', 'operating_profit', 'net_profit',
                              'net_profit_attributable', 'eps']
                for r in inc_results:
                    mapping = r._mapping
                    filtered = {k: mapping[k] for k in inc_fields if k in mapping and mapping[k] is not None}
                    financials.income_statements.append(IncomeStatement(**filtered))

                cf_fields = ['report_date', 'operating_cash_flow', 'investing_cash_flow',
                             'financing_cash_flow', 'net_cash_flow']
                for r in cf_results:
                    mapping = r._mapping
                    filtered = {k: mapping[k] for k in cf_fields if k in mapping and mapping[k] is not None}
                    financials.cash_flow_statements.append(CashFlowStatement(**filtered))

                return financials
        finally:
            session.close()
        return None


class CachedDataSource(DataSource):
    def __init__(self, primary_source: DataSource, cache: SQLiteCache):
        self.primary_source = primary_source
        self.cache = cache

    def search_stocks(self, query: str) -> List[Stock]:
        return self.primary_source.search_stocks(query)

    def get_stock_info(self, code: str) -> Optional[Stock]:
        cached = self.cache.get_stock(code)
        if cached:
            return cached
        stock = self.primary_source.get_stock_info(code)
        if stock:
            self.cache.save_stock(stock)
        return stock

    def get_financial_statements(self, code: str) -> Optional[FinancialStatements]:
        cached = self.cache.get_financials(code)
        if cached and len(cached.income_statements) >= 4:
            return cached
        financials = self.primary_source.get_financial_statements(code)
        if financials:
            self.cache.save_financials(financials)
        return financials