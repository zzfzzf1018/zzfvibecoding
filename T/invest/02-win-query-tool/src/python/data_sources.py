import sys
import requests

DATA_SOURCE_CONFIG = {
    'eastmoney': {
        'name': '东方财富',
        'description': '东方财富数据源，数据全面，更新及时',
        'stock_search_cn': 'stock_info_a_code_name',
        'stock_search_hk': 'stock_hk_spot_em',
        'finance_report_cn_balance': 'stock_balance_sheet_by_report_em',
        'finance_report_cn_income': 'stock_profit_sheet_by_report_em',
        'finance_report_cn_cash': 'stock_cash_flow_sheet_by_report_em',
        'finance_report_hk': 'stock_financial_hk_report_em',
        'stock_analysis_cn': 'stock_financial_analysis_indicator_em',
        'stock_analysis_hk': 'stock_hk_financial_indicator_em',
        'prospectus_cn': 'stock_ipo_info',
        'prospectus_hk': 'stock_ipo_hk_ths',
    },
    'sina': {
        'name': '新浪财经',
        'description': '新浪财经数据源，速度快，接口稳定',
        'stock_search_cn': 'stock_info_a_code_name',
        'stock_search_hk': 'stock_hk_spot_sina',
        'finance_report_cn_balance': 'stock_financial_report_sina',
        'finance_report_cn_income': 'stock_financial_report_sina',
        'finance_report_cn_cash': 'stock_financial_report_sina',
        'finance_report_hk': 'stock_financial_hk_report_em',
        'stock_analysis_cn': 'stock_financial_analysis_indicator',
        'stock_analysis_hk': 'stock_hk_financial_indicator_em',
        'prospectus_cn': 'stock_ipo_info',
        'prospectus_hk': 'stock_ipo_hk_ths',
    },
    'tencent': {
        'name': '腾讯财经',
        'description': '腾讯财经数据源，数据准确，覆盖面广',
        'stock_search_cn': 'stock_info_a_code_name',
        'stock_search_hk': 'stock_hk_spot',
        'finance_report_cn_balance': 'stock_balance_sheet_by_report_em',
        'finance_report_cn_income': 'stock_profit_sheet_by_report_em',
        'finance_report_cn_cash': 'stock_cash_flow_sheet_by_report_em',
        'finance_report_hk': 'stock_financial_hk_report_em',
        'stock_analysis_cn': 'stock_financial_analysis_indicator_em',
        'stock_analysis_hk': 'stock_hk_financial_indicator_em',
        'prospectus_cn': 'stock_ipo_info',
        'prospectus_hk': 'stock_ipo_hk_ths',
    },
}

_current_data_source = 'eastmoney'

def get_current_data_source():
    return _current_data_source

def set_data_source(source_id):
    global _current_data_source
    if source_id in DATA_SOURCE_CONFIG:
        _current_data_source = source_id
        return True
    return False

def get_data_source_config(source_id=None):
    if source_id is None:
        source_id = _current_data_source
    return DATA_SOURCE_CONFIG.get(source_id, {})

def get_all_data_sources():
    return [{
        'id': key,
        'name': config['name'],
        'description': config['description']
    } for key, config in DATA_SOURCE_CONFIG.items()]

class DataSourceFactory:
    def __init__(self):
        self.ak = None
        self._init_akshare()
    
    def _init_akshare(self):
        try:
            import akshare as ak
            session = requests.Session()
            session.headers.update({
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
            })
            session.timeout = 30
            ak.requests_session = session
            self.ak = ak
        except ImportError:
            self.ak = None
    
    def get_stock_search_cn(self):
        config = get_data_source_config()
        func_name = config.get('stock_search_cn', 'stock_info_a_code_name')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_stock_search_hk(self):
        config = get_data_source_config()
        func_name = config.get('stock_search_hk', 'stock_hk_spot_em')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_finance_report_cn(self, report_type):
        config = get_data_source_config()
        if report_type == 'balance':
            func_name = config.get('finance_report_cn_balance', 'stock_financial_analysis_indicator')
        elif report_type == 'income':
            func_name = config.get('finance_report_cn_income', 'stock_financial_report_sina')
        elif report_type == 'cash':
            func_name = config.get('finance_report_cn_cash', 'stock_financial_report_sina')
        else:
            func_name = config.get('finance_report_cn_balance', 'stock_financial_analysis_indicator')
        
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_finance_report_hk(self):
        config = get_data_source_config()
        func_name = config.get('finance_report_hk', 'stock_hk_financial_report')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_stock_analysis_cn(self):
        config = get_data_source_config()
        func_name = config.get('stock_analysis_cn', 'stock_financial_analysis_indicator_em')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_stock_analysis_hk(self):
        config = get_data_source_config()
        func_name = config.get('stock_analysis_hk', 'stock_hk_financial_indicator_em')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_prospectus_cn(self):
        config = get_data_source_config()
        func_name = config.get('prospectus_cn', 'stock_ipo_info')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def get_prospectus_hk(self):
        config = get_data_source_config()
        func_name = config.get('prospectus_hk', 'stock_ipo_hk_ths')
        if self.ak and hasattr(self.ak, func_name):
            return getattr(self.ak, func_name)
        return None
    
    def is_available(self):
        return self.ak is not None

data_source_factory = DataSourceFactory()