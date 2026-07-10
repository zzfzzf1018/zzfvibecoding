from flask import Flask, jsonify, request
from flask_cors import CORS
import pandas as pd
import numpy as np
import os
import time
import sys
import requests

app = Flask(__name__)
CORS(app)

from data_sources import (
    data_source_factory,
    get_all_data_sources,
    get_current_data_source,
    set_data_source
)

akshare_available = data_source_factory.is_available()

from cache import set_cache, get_cache, clear_cache, clear_expired_cache, get_cache_stats

def retry_with_backoff(func, max_retries=2, delay=1):
    last_exception = None
    for i in range(max_retries):
        try:
            return func()
        except Exception as e:
            last_exception = e
            if i == max_retries - 1:
                print(f"Final retry {i+1}/{max_retries} failed: {e}", file=sys.stderr)
                return None
            wait_time = delay * (2 ** i)
            print(f"Retry {i+1}/{max_retries} after {wait_time}s: {e}", file=sys.stderr)
            time.sleep(wait_time)
    return None

@app.route('/api/health', methods=['GET'])
def health_check():
    return jsonify({'success': True, 'akshare_available': akshare_available})

@app.route('/api/data_source/list', methods=['GET'])
def list_data_sources():
    sources = get_all_data_sources()
    current = get_current_data_source()
    return jsonify({'success': True, 'data': sources, 'current': current})

@app.route('/api/data_source/switch', methods=['POST'])
def switch_data_source():
    data = request.get_json()
    source_id = data.get('source_id', '')
    
    if set_data_source(source_id):
        return jsonify({'success': True, 'message': f'已切换到 {source_id} 数据源'})
    else:
        return jsonify({'success': False, 'error': '无效的数据源ID'})

@app.route('/api/cache/stats', methods=['GET'])
def cache_stats():
    return jsonify({'success': True, 'data': get_cache_stats()})

@app.route('/api/cache/clear', methods=['POST'])
def cache_clear():
    clear_cache()
    return jsonify({'success': True, 'message': '缓存已清除'})

@app.route('/api/cache/clear_expired', methods=['POST'])
def cache_clear_expired():
    clear_expired_cache()
    return jsonify({'success': True, 'message': '过期缓存已清除'})

COMMON_STOCKS = [
    {'code': '600519', 'name': '贵州茅台', 'market': 'cn', 'full_code': '600519.SH'},
    {'code': '000858', 'name': '五粮液', 'market': 'cn', 'full_code': '000858.SZ'},
    {'code': '601318', 'name': '中国平安', 'market': 'cn', 'full_code': '601318.SH'},
    {'code': '000001', 'name': '平安银行', 'market': 'cn', 'full_code': '000001.SZ'},
    {'code': '600036', 'name': '招商银行', 'market': 'cn', 'full_code': '600036.SH'},
    {'code': '000001', 'name': '上证指数', 'market': 'cn', 'full_code': '000001.SH'},
    {'code': '00700', 'name': '腾讯控股', 'market': 'hk', 'full_code': '00700.HK'},
    {'code': '00005', 'name': '汇丰控股', 'market': 'hk', 'full_code': '00005.HK'},
    {'code': '03888', 'name': '香港交易所', 'market': 'hk', 'full_code': '03888.HK'},
    {'code': '01398', 'name': '工商银行', 'market': 'hk', 'full_code': '01398.HK'},
    {'code': '688981', 'name': '中芯国际', 'market': 'cn', 'full_code': '688981.SH'},
    {'code': '601899', 'name': '紫金矿业', 'market': 'cn', 'full_code': '601899.SH'},
    {'code': '000063', 'name': '中兴通讯', 'market': 'cn', 'full_code': '000063.SZ'},
    {'code': '002594', 'name': '比亚迪', 'market': 'cn', 'full_code': '002594.SZ'},
    {'code': '000651', 'name': '格力电器', 'market': 'cn', 'full_code': '000651.SZ'},
]

def search_common_stocks(keyword, market):
    results = []
    for stock in COMMON_STOCKS:
        if (market == 'all' or stock['market'] == market) and \
           (keyword.lower() in stock['code'].lower() or keyword.lower() in stock['name'].lower()):
            results.append(stock)
    return results

@app.route('/api/stock/search', methods=['GET'])
def search_stock():
    keyword = request.args.get('keyword', '')
    market = request.args.get('market', 'all')
    
    if not akshare_available:
        return jsonify({'success': False, 'error': 'akshare未安装，请先安装依赖'})
    
    cache_params = {'keyword': keyword, 'market': market, 'source': get_current_data_source()}
    cached_data = get_cache('stock_search', cache_params)
    
    try:
        results = []
        use_fallback = False
        
        if market == 'all' or market == 'cn':
            try:
                stock_search_func = data_source_factory.get_stock_search_cn()
                if stock_search_func:
                    stock_info = retry_with_backoff(lambda: stock_search_func())
                    if stock_info is not None and not stock_info.empty:
                        matched = stock_info[stock_info['code'].str.contains(keyword) | stock_info['name'].str.contains(keyword)]
                        for _, row in matched.head(20).iterrows():
                            results.append({
                                'code': row['code'],
                                'name': row['name'],
                                'market': 'cn',
                                'full_code': f"{row['code']}.SH" if row['code'].startswith('6') else f"{row['code']}.SZ"
                            })
            except Exception as e:
                print(f"CN stock search failed, using fallback: {e}", file=sys.stderr)
                use_fallback = True
        
        if market == 'all' or market == 'hk':
            try:
                hk_search_func = data_source_factory.get_stock_search_hk()
                if hk_search_func:
                    hk_stocks = retry_with_backoff(lambda: hk_search_func())
                    if hk_stocks is not None and not hk_stocks.empty:
                        matched = hk_stocks[hk_stocks['代码'].astype(str).str.contains(keyword) | hk_stocks['名称'].str.contains(keyword)]
                        for _, row in matched.head(20).iterrows():
                            results.append({
                                'code': str(row['代码']),
                                'name': row['名称'],
                                'market': 'hk',
                                'full_code': f"{row['代码']}.HK"
                            })
            except Exception as e:
                print(f"HK stock search failed, using fallback: {e}", file=sys.stderr)
                use_fallback = True
        
        if not results:
            common_results = search_common_stocks(keyword, market)
            if common_results:
                results = common_results
                use_fallback = True
        
        if results:
            set_cache('stock_search', cache_params, results)
            return jsonify({
                'success': True, 
                'data': results[:30], 
                'cached': False,
                'warning': '当前网络不稳定，显示常用股票数据' if use_fallback else None
            })
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data[:30], 'cached': True})
        
        return jsonify({'success': False, 'error': '未找到匹配的股票'})
    
    except Exception as e:
        error_msg = f'网络请求失败，请检查网络连接或稍后重试'
        print(f"Stock search error: {e}", file=sys.stderr)
        
        common_results = search_common_stocks(keyword, market)
        if common_results:
            return jsonify({
                'success': True, 
                'data': common_results[:30], 
                'cached': False,
                'warning': '网络异常，显示常用股票数据'
            })
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data[:30], 'cached': True, 'warning': '网络异常，显示缓存数据'})
        
        return jsonify({'success': False, 'error': error_msg})

headers = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    'Accept': 'application/json, text/javascript, */*; q=0.01',
    'Accept-Language': 'zh-CN,zh;q=0.9,en;q=0.8',
}

def fetch_eastmoney_stock_data(code, exchange='SH'):
    secid = f"1.{code}" if exchange == 'SH' else f"0.{code}"
    url = f"http://push2.eastmoney.com/api/qt/stock/get?secid={secid}&fields=f57,f58,f100,f101,f103,f104,f105,f106,f107,f108,f109,f116,f117,f118,f119,f120,f121,f122,f123,f124,f125,f126,f127,f128,f129,f130,f131,f132,f133,f134,f135,f136,f137,f138,f139,f140,f141,f142,f143,f144,f145,f146,f147,f148,f149,f150,f151,f152,f153,f154,f155,f156,f157,f158,f159,f160,f161"
    try:
        response = requests.get(url, timeout=30, headers=headers)
        response.raise_for_status()
        return response.json()
    except Exception as e:
        print(f"Eastmoney API error: {e}", file=sys.stderr)
        return None

def fetch_eastmoney_finance(code, exchange='SH', report_type='balance'):
    secid = f"1.{code}" if exchange == 'SH' else f"0.{code}"
    type_map = {
        'balance': 'F1003',
        'income': 'F1004', 
        'cash': 'F1005'
    }
    report_type_code = type_map.get(report_type, 'F1003')
    
    url = f"http://push2.eastmoney.com/api/qt/stock/get?secid={secid}&fields={report_type_code}"
    try:
        response = requests.get(url, timeout=30)
        response.raise_for_status()
        return response.json()
    except Exception as e:
        print(f"Eastmoney finance API error: {e}", file=sys.stderr)
        return None

@app.route('/api/stock/finance_report', methods=['GET'])
def get_finance_report():
    symbol = request.args.get('symbol', '')
    report_type = request.args.get('type', 'balance')
    
    cache_params = {'symbol': symbol, 'type': report_type, 'source': get_current_data_source()}
    cached_data = get_cache('finance_report', cache_params)
    
    try:
        if symbol.endswith('.SH'):
            code = symbol[:-3]
            exchange = 'SH'
        elif symbol.endswith('.SZ'):
            code = symbol[:-3]
            exchange = 'SZ'
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            exchange = 'HK'
        else:
            return jsonify({'success': False, 'error': '无效的股票代码格式'})
        
        eastmoney_data = fetch_eastmoney_stock_data(code, exchange)
        if eastmoney_data and eastmoney_data.get('rc') == 0:
            data = eastmoney_data.get('data', {})
            
            report_data = []
            if report_type == 'balance':
                report_data = [
                    {'指标': '总股本(万股)', '数值': data.get('f103', '')},
                    {'指标': '流通股本(万股)', '数值': data.get('f104', '')},
                    {'指标': '净资产(万元)', '数值': data.get('f105', '')},
                    {'指标': '每股收益(元)', '数值': data.get('f108', '')},
                    {'指标': '每股净资产(元)', '数值': data.get('f160', '')},
                    {'指标': '市净率', '数值': data.get('f109', '')},
                    {'指标': '总市值(元)', '数值': data.get('f116', '')},
                    {'指标': '流通市值(元)', '数值': data.get('f117', '')},
                ]
            elif report_type == 'income':
                report_data = [
                    {'指标': '每股收益(元)', '数值': data.get('f108', '')},
                    {'指标': '市盈率', '数值': data.get('f107', '')},
                    {'指标': '市净率', '数值': data.get('f109', '')},
                    {'指标': '总市值(元)', '数值': data.get('f116', '')},
                    {'指标': '流通市值(元)', '数值': data.get('f117', '')},
                    {'指标': '换手率', '数值': data.get('f118', '')},
                ]
            elif report_type == 'cash':
                report_data = [
                    {'指标': '总股本(万股)', '数值': data.get('f103', '')},
                    {'指标': '流通股本(万股)', '数值': data.get('f104', '')},
                    {'指标': '总市值(元)', '数值': data.get('f116', '')},
                    {'指标': '流通市值(元)', '数值': data.get('f117', '')},
                ]
            
            df = pd.DataFrame(report_data)
            if not df.empty:
                df = df.dropna(axis=1, how='all')
                df = df.replace({np.nan: None})
                
                columns = df.columns.tolist()
                data_values = df.values.tolist()
                
                result = {'columns': columns, 'data': data_values}
                set_cache('finance_report', cache_params, result)
                return jsonify({'success': True, **result, 'cached': False})
        
        if akshare_available:
            df = None
            code = symbol[:-3]
            
            if symbol.endswith('.SH') or symbol.endswith('.SZ'):
                apis_to_try = []
                stock_code = f"sh{code}" if symbol.endswith('.SH') else f"sz{code}"
                
                if report_type == 'balance':
                    apis_to_try = [
                        lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='资产负债表'),
                    ]
                elif report_type == 'income':
                    apis_to_try = [
                        lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='利润表'),
                    ]
                elif report_type == 'cash':
                    apis_to_try = [
                        lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='现金流量表'),
                    ]
                
                for api_func in apis_to_try:
                    try:
                        df = retry_with_backoff(api_func)
                        if df is not None and not df.empty:
                            break
                    except Exception as e:
                        print(f"akshare API failed: {e}", file=sys.stderr)
            
            if df is not None and not df.empty:
                df = df.dropna(axis=1, how='all')
                df = df.replace({np.nan: None})
                
                columns = df.columns.tolist()
                data_values = df.values.tolist()
                
                result = {'columns': columns, 'data': data_values}
                set_cache('finance_report', cache_params, result)
                return jsonify({'success': True, **result, 'cached': False})
        
        if cached_data:
            return jsonify({'success': True, **cached_data, 'cached': True, 'warning': '当前数据源无数据，显示缓存数据'})
        
        return jsonify({'success': False, 'error': '未获取到财务数据，请检查股票代码是否正确'})
    
    except Exception as e:
        print(f"Finance report error: {e}", file=sys.stderr)
        
        if cached_data:
            return jsonify({'success': True, **cached_data, 'cached': True, 'warning': '网络异常，显示缓存数据'})
        
        return jsonify({'success': False, 'error': '网络请求失败，请检查网络连接或稍后重试'})

@app.route('/api/stock/analysis', methods=['GET'])
def get_stock_analysis():
    symbol = request.args.get('symbol', '')
    
    cache_params = {'symbol': symbol, 'source': get_current_data_source()}
    cached_data = get_cache('stock_analysis', cache_params)
    
    try:
        analysis_data = {
            'valuation': {
                'pe': None,
                'pb': None,
                'ps': None,
                'dividend_yield': None
            },
            'financial_ratios': {}
        }
        
        if symbol.endswith('.SH'):
            code = symbol[:-3]
            exchange = 'SH'
        elif symbol.endswith('.SZ'):
            code = symbol[:-3]
            exchange = 'SZ'
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            exchange = 'HK'
        else:
            return jsonify({'success': False, 'error': '无效的股票代码格式'})
        
        eastmoney_data = fetch_eastmoney_stock_data(code, exchange)
        if eastmoney_data and eastmoney_data.get('rc') == 0:
            data = eastmoney_data.get('data', {})
            
            pe = data.get('f107')
            pb = data.get('f109')
            eps = data.get('f108')
            nav = data.get('f160')
            
            analysis_data['valuation'] = {
                'pe': round(float(pe), 2) if pe else None,
                'pb': round(float(pb), 2) if pb else None,
                'ps': None,
                'dividend_yield': None
            }
            
            analysis_data['financial_ratios'] = {
                '每股收益': round(float(eps), 4) if eps else None,
                '每股净资产': round(float(nav), 4) if nav else None,
                '总股本(万股)': data.get('f103'),
                '流通股本(万股)': data.get('f104'),
                '总市值(元)': data.get('f116'),
                '流通市值(元)': data.get('f117'),
            }
            
            has_data = analysis_data['valuation']['pe'] is not None or \
                       analysis_data['valuation']['pb'] is not None or \
                       any(v is not None for v in analysis_data['financial_ratios'].values())
            
            if has_data:
                set_cache('stock_analysis', cache_params, analysis_data)
                return jsonify({'success': True, 'data': analysis_data, 'cached': False})
        
        if akshare_available and (symbol.endswith('.SH') or symbol.endswith('.SZ')):
            code = symbol[:-3]
            stock_code = f"sh{code}" if symbol.endswith('.SH') else f"sz{code}"
            
            try:
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='资产负债表'))
                if df is not None and not df.empty:
                    if len(df) > 0:
                        analysis_data['financial_ratios']['货币资金'] = df.iloc[0].get('货币资金')
                        analysis_data['financial_ratios']['总资产'] = df.iloc[0].get('资产总计')
                        analysis_data['financial_ratios']['总负债'] = df.iloc[0].get('负债合计')
                        analysis_data['financial_ratios']['所有者权益合计'] = df.iloc[0].get('所有者权益(或股东权益)合计')
                        analysis_data['financial_ratios']['总股本(万股)'] = df.iloc[0].get('实收资本(或股本)')
                    
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='利润表'))
                if df is not None and not df.empty:
                    if len(df) > 0:
                        analysis_data['financial_ratios']['营业收入'] = df.iloc[0].get('营业收入')
                        analysis_data['financial_ratios']['净利润'] = df.iloc[0].get('净利润')
                        analysis_data['financial_ratios']['基本每股收益'] = df.iloc[0].get('基本每股收益')
                        
                        revenue = df.iloc[0].get('营业收入')
                        profit = df.iloc[0].get('净利润')
                        if revenue and profit and float(revenue) > 0:
                            analysis_data['financial_ratios']['净利率'] = round(float(profit) / float(revenue) * 100, 2)
                    
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='现金流量表'))
                if df is not None and not df.empty:
                    if len(df) > 0:
                        analysis_data['financial_ratios']['经营活动产生的现金流量净额'] = df.iloc[0].get('经营活动产生的现金流量净额')
            
            except Exception as e:
                print(f"akshare analysis API failed: {e}", file=sys.stderr)
        
        if symbol.endswith('.HK') and akshare_available:
            code = symbol[:-3]
            
            try:
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_hk_analysis_indicator_em(symbol=code))
                if df is not None and not df.empty:
                    if len(df) > 0:
                        analysis_data['financial_ratios']['SECURITY_NAME_ABBR'] = df.iloc[0].get('SECURITY_NAME_ABBR')
                        analysis_data['financial_ratios']['CURRENCY'] = df.iloc[0].get('CURRENCY')
            except Exception as e:
                print(f"HK Analysis API failed: {e}", file=sys.stderr)
        
        has_data = analysis_data['valuation']['pe'] is not None or \
                   analysis_data['valuation']['pb'] is not None or \
                   len(analysis_data['financial_ratios']) > 0
        
        if has_data:
            set_cache('stock_analysis', cache_params, analysis_data)
            return jsonify({'success': True, 'data': analysis_data, 'cached': False})
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True, 'warning': '当前数据源无数据，显示缓存数据'})
        
        return jsonify({'success': False, 'error': '未获取到分析数据，请检查股票代码是否正确'})
    
    except Exception as e:
        import traceback
        print(f"Stock analysis error: {e}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True, 'warning': '网络异常，显示缓存数据'})
        
        return jsonify({'success': False, 'error': f'网络请求失败: {str(e)}'})

@app.route('/api/stock/download_report', methods=['GET'])
def download_report():
    symbol = request.args.get('symbol', '')
    report_type = request.args.get('type', 'balance')
    
    if not akshare_available:
        return jsonify({'success': False, 'error': 'akshare未安装，请先安装依赖'})
    
    try:
        df = None
        
        if symbol.endswith('.SH') or symbol.endswith('.SZ'):
            code = symbol[:-3]
            
            stock_code = f"sh{code}" if symbol.endswith('.SH') else f"sz{code}"
            
            if report_type == 'balance':
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='资产负债表'))
            elif report_type == 'income':
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='利润表'))
            elif report_type == 'cash':
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='现金流量表'))
            else:
                df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_report_sina(stock=stock_code, symbol='资产负债表'))
        
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            
            finance_report_func = data_source_factory.get_finance_report_hk()
            if finance_report_func:
                if report_type == 'balance':
                    df = retry_with_backoff(lambda: finance_report_func(symbol=f"{code}.HK", indicator='资产负债表'))
                elif report_type == 'income':
                    df = retry_with_backoff(lambda: finance_report_func(symbol=f"{code}.HK", indicator='综合收益表'))
                elif report_type == 'cash':
                    df = retry_with_backoff(lambda: finance_report_func(symbol=f"{code}.HK", indicator='现金流量表'))
                else:
                    df = retry_with_backoff(lambda: finance_report_func(symbol=f"{code}.HK", indicator='资产负债表'))
            else:
                if report_type == 'balance':
                    df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_hk_report_em(symbol=f"{code}.HK"))
                elif report_type == 'income':
                    df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_hk_report_em(symbol=f"{code}.HK"))
                elif report_type == 'cash':
                    df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_hk_report_em(symbol=f"{code}.HK"))
                else:
                    df = retry_with_backoff(lambda: data_source_factory.ak.stock_financial_hk_report_em(symbol=f"{code}.HK"))
        
        else:
            return jsonify({'success': False, 'error': 'Invalid symbol format'})
        
        if df is None or df.empty:
            return jsonify({'success': False, 'error': '未获取到数据，无法下载'})
        
        filename = f"{symbol}_{report_type}_{pd.Timestamp.now().strftime('%Y%m%d')}.xlsx"
        filepath = os.path.join(os.path.dirname(__file__), 'downloads', filename)
        
        os.makedirs(os.path.dirname(filepath), exist_ok=True)
        df.to_excel(filepath, index=False)
        
        return jsonify({'success': True, 'filepath': filepath, 'filename': filename})
    
    except Exception as e:
        error_msg = f'网络请求失败: {str(e)}'
        print(f"Download report error: {e}", file=sys.stderr)
        return jsonify({'success': False, 'error': error_msg})

@app.route('/api/stock/prospectus', methods=['GET'])
def get_prospectus():
    symbol = request.args.get('symbol', '')
    
    if not akshare_available:
        return jsonify({'success': False, 'error': 'akshare未安装，请先安装依赖'})
    
    cache_params = {'symbol': symbol, 'source': get_current_data_source()}
    cached_data = get_cache('prospectus', cache_params)
    
    try:
        code = symbol[:-3] if symbol.endswith('.SH') or symbol.endswith('.SZ') or symbol.endswith('.HK') else symbol
        
        apis_to_try = [
            lambda: data_source_factory.ak.stock_ipo_info(stock=code),
            lambda: data_source_factory.ak.stock_ipo_declare_em(),
            lambda: data_source_factory.ak.stock_ipo_summary_cninfo(),
        ]
        
        results = []
        
        for api_func in apis_to_try:
            try:
                prospectus_data = retry_with_backoff(api_func)
                if prospectus_data is not None and not prospectus_data.empty:
                    if 'item' in prospectus_data.columns:
                        ipo_info = {}
                        for _, row in prospectus_data.iterrows():
                            ipo_info[row['item']] = row['value']
                        results.append({
                            'code': code,
                            'name': ipo_info.get('股票简称', ''),
                            'ipo_info': ipo_info
                        })
                        break
                    else:
                        code_cols = ['申购代码', '代码', 'stock_code', 'code']
                        name_cols = ['证券简称', '名称', 'stock_name', 'name']
                        
                        matched = None
                        for col in code_cols:
                            if col in prospectus_data.columns:
                                matched = prospectus_data[prospectus_data[col].astype(str).str.contains(code)]
                                if not matched.empty:
                                    break
                        
                        if matched is None or matched.empty:
                            for col in name_cols:
                                if col in prospectus_data.columns:
                                    matched = prospectus_data[prospectus_data[col].str.contains(code, na=False)]
                                    if not matched.empty:
                                        break
                        
                        if matched is not None and not matched.empty:
                            for _, row in matched.head(10).iterrows():
                                result_code = ''
                                result_name = ''
                                for col in code_cols:
                                    if col in row:
                                        result_code = str(row[col])
                                        break
                                for col in name_cols:
                                    if col in row:
                                        result_name = row[col]
                                        break
                                
                                results.append({
                                    'code': result_code,
                                'name': result_name,
                                'ipo_date': row.get('上网发行日期', row.get('招股日期', row.get('发行日期', ''))),
                                'prospectus_url': row.get('招股书链接', row.get('招股书', ''))
                            })
                        break
            except Exception as e:
                print(f"Prospectus API failed: {e}", file=sys.stderr)
        
        if symbol.endswith('.HK'):
            hk_apis = [
                lambda: data_source_factory.ak.stock_ipo_hk_ths(),
            ]
            
            for api_func in hk_apis:
                try:
                    hk_prospectus = retry_with_backoff(api_func)
                    if hk_prospectus is not None and not hk_prospectus.empty:
                        matched = hk_prospectus[hk_prospectus.get('代码', hk_prospectus.get('code', pd.Series(['']))).astype(str).str.contains(code) | 
                                                hk_prospectus.get('名称', hk_prospectus.get('name', pd.Series(['']))).str.contains(code, na=False)]
                        
                        for _, row in matched.head(10).iterrows():
                            results.append({
                                'code': str(row.get('代码', row.get('code', ''))),
                                'name': row.get('名称', row.get('name', '')),
                                'ipo_date': row.get('招股日期', ''),
                                'prospectus_url': row.get('招股书', '')
                            })
                        break
                except Exception as e:
                    print(f"HK Prospectus API failed: {e}", file=sys.stderr)
        
        if results:
            set_cache('prospectus', cache_params, results)
            return jsonify({'success': True, 'data': results, 'cached': False})
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True, 'warning': '当前数据源无数据，显示缓存数据'})
        
        return jsonify({'success': False, 'error': '未找到招股书信息，请检查股票代码是否正确'})
    
    except Exception as e:
        print(f"Prospectus error: {e}", file=sys.stderr)
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True, 'warning': '网络异常，显示缓存数据'})
        
        return jsonify({'success': False, 'error': error_msg})

@app.route('/api/stock/download_prospectus', methods=['GET'])
def download_prospectus():
    url = request.args.get('url', '')
    filename = request.args.get('filename', 'prospectus.pdf')
    
    try:
        import requests
        
        response = requests.get(url, stream=True)
        if response.status_code != 200:
            return jsonify({'success': False, 'error': '下载失败，服务器返回非200状态码'})
        
        filepath = os.path.join(os.path.dirname(__file__), 'downloads', filename)
        os.makedirs(os.path.dirname(filepath), exist_ok=True)
        
        with open(filepath, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192):
                f.write(chunk)
        
        return jsonify({'success': True, 'filepath': filepath, 'filename': filename})
    
    except Exception as e:
        error_msg = f'网络请求失败: {str(e)}'
        print(f"Download prospectus error: {e}", file=sys.stderr)
        return jsonify({'success': False, 'error': error_msg})

if __name__ == '__main__':
    clear_expired_cache()
    app.run(host='0.0.0.0', port=5000, debug=False)
