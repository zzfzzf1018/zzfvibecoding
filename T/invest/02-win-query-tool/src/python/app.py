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

akshare_available = True
try:
    import akshare as ak
    
    session = requests.Session()
    session.headers.update({
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
    })
    ak.requests_session = session
    session.timeout = 30
except ImportError:
    akshare_available = False

from cache import set_cache, get_cache, clear_cache, clear_expired_cache, get_cache_stats

def retry_with_backoff(func, max_retries=5, delay=2):
    for i in range(max_retries):
        try:
            return func()
        except Exception as e:
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
    
    cache_params = {'keyword': keyword, 'market': market}
    cached_data = get_cache('stock_search', cache_params)
    
    try:
        results = []
        use_fallback = False
        
        if market == 'all' or market == 'cn':
            try:
                stock_info = retry_with_backoff(lambda: ak.stock_info_a_code_name())
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
                hk_stocks = retry_with_backoff(lambda: ak.stock_hk_spot_em())
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

@app.route('/api/stock/finance_report', methods=['GET'])
def get_finance_report():
    symbol = request.args.get('symbol', '')
    report_type = request.args.get('type', 'balance')
    
    if not akshare_available:
        return jsonify({'success': False, 'error': 'akshare未安装，请先安装依赖'})
    
    cache_params = {'symbol': symbol, 'type': report_type}
    cached_data = get_cache('finance_report', cache_params)
    
    try:
        if symbol.endswith('.SH') or symbol.endswith('.SZ'):
            code = symbol[:-3]
            
            if report_type == 'balance':
                df = retry_with_backoff(lambda: ak.stock_financial_analysis_indicator(symbol=code))
            elif report_type == 'income':
                df = retry_with_backoff(lambda: ak.stock_financial_report_sina(symbol=code, symbol_type='main', report_type='income'))
            elif report_type == 'cash':
                df = retry_with_backoff(lambda: ak.stock_financial_report_sina(symbol=code, symbol_type='main', report_type='cash'))
            else:
                df = retry_with_backoff(lambda: ak.stock_financial_analysis_indicator(symbol=code))
            
            if df is not None and not df.empty:
                df = df.dropna(axis=1, how='all')
                df = df.replace({np.nan: None})
                
                columns = df.columns.tolist()
                data = df.values.tolist()
                
                result = {'columns': columns, 'data': data}
                set_cache('finance_report', cache_params, result)
                return jsonify({'success': True, **result, 'cached': False})
        
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            
            if report_type == 'balance':
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='资产负债表'))
            elif report_type == 'income':
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='综合收益表'))
            elif report_type == 'cash':
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='现金流量表'))
            else:
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='资产负债表'))
            
            if df is not None and not df.empty:
                df = df.dropna(axis=1, how='all')
                df = df.replace({np.nan: None})
                
                columns = df.columns.tolist()
                data = df.values.tolist()
                
                result = {'columns': columns, 'data': data}
                set_cache('finance_report', cache_params, result)
                return jsonify({'success': True, **result, 'cached': False})
        
        else:
            return jsonify({'success': False, 'error': 'Invalid symbol format'})
        
        if cached_data:
            return jsonify({'success': True, **cached_data, 'cached': True})
        
        return jsonify({'success': False, 'error': '未获取到财务数据'})
    
    except Exception as e:
        error_msg = f'网络请求失败: {str(e)}'
        print(f"Finance report error: {e}", file=sys.stderr)
        
        if cached_data:
            return jsonify({'success': True, **cached_data, 'cached': True, 'warning': '网络异常，显示缓存数据'})
        
        return jsonify({'success': False, 'error': error_msg})

@app.route('/api/stock/analysis', methods=['GET'])
def get_stock_analysis():
    symbol = request.args.get('symbol', '')
    
    if not akshare_available:
        return jsonify({'success': False, 'error': 'akshare未安装，请先安装依赖'})
    
    cache_params = {'symbol': symbol}
    cached_data = get_cache('stock_analysis', cache_params)
    
    try:
        if symbol.endswith('.SH') or symbol.endswith('.SZ'):
            code = symbol[:-3]
            
            stock_zh_a_indicator_df = retry_with_backoff(lambda: ak.stock_zh_a_indicator(symbol=f"{code}.SH"))
            stock_financial_analysis_indicator_df = retry_with_backoff(lambda: ak.stock_financial_analysis_indicator(symbol=code))
            
            pe = stock_zh_a_indicator_df.get('市盈率', np.nan)
            pb = stock_zh_a_indicator_df.get('市净率', np.nan)
            ps = stock_zh_a_indicator_df.get('市销率', np.nan)
            dv = stock_zh_a_indicator_df.get('股息率', np.nan)
            
            if isinstance(pe, pd.Series):
                pe = pe.iloc[0] if len(pe) > 0 else np.nan
            if isinstance(pb, pd.Series):
                pb = pb.iloc[0] if len(pb) > 0 else np.nan
            if isinstance(ps, pd.Series):
                ps = ps.iloc[0] if len(ps) > 0 else np.nan
            if isinstance(dv, pd.Series):
                dv = dv.iloc[0] if len(dv) > 0 else np.nan
            
            analysis_data = {
                'valuation': {
                    'pe': round(float(pe), 2) if not np.isnan(pe) else None,
                    'pb': round(float(pb), 2) if not np.isnan(pb) else None,
                    'ps': round(float(ps), 2) if not np.isnan(ps) else None,
                    'dividend_yield': round(float(dv), 2) if not np.isnan(dv) else None
                },
                'financial_ratios': {}
            }
            
            if stock_financial_analysis_indicator_df is not None and not stock_financial_analysis_indicator_df.empty:
                for col in stock_financial_analysis_indicator_df.columns[:20]:
                    val = stock_financial_analysis_indicator_df[col].iloc[0] if len(stock_financial_analysis_indicator_df) > 0 else np.nan
                    if not np.isnan(val):
                        analysis_data['financial_ratios'][col] = round(float(val), 2)
            
            if analysis_data['valuation']['pe'] is not None or analysis_data['financial_ratios']:
                set_cache('stock_analysis', cache_params, analysis_data)
                return jsonify({'success': True, 'data': analysis_data, 'cached': False})
        
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            
            stock_hk_indicator_df = retry_with_backoff(lambda: ak.stock_hk_indicator(symbol=f"{code}.HK"))
            
            pe = stock_hk_indicator_df.get('市盈率', np.nan)
            pb = stock_hk_indicator_df.get('市净率', np.nan)
            
            if isinstance(pe, pd.Series):
                pe = pe.iloc[0] if len(pe) > 0 else np.nan
            if isinstance(pb, pd.Series):
                pb = pb.iloc[0] if len(pb) > 0 else np.nan
            
            analysis_data = {
                'valuation': {
                    'pe': round(float(pe), 2) if not np.isnan(pe) else None,
                    'pb': round(float(pb), 2) if not np.isnan(pb) else None,
                    'ps': None,
                    'dividend_yield': None
                },
                'financial_ratios': {}
            }
            
            if analysis_data['valuation']['pe'] is not None:
                set_cache('stock_analysis', cache_params, analysis_data)
                return jsonify({'success': True, 'data': analysis_data, 'cached': False})
        
        else:
            return jsonify({'success': False, 'error': 'Invalid symbol format'})
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True})
        
        return jsonify({'success': False, 'error': '未获取到分析数据'})
    
    except Exception as e:
        error_msg = f'网络请求失败: {str(e)}'
        print(f"Stock analysis error: {e}", file=sys.stderr)
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True, 'warning': '网络异常，显示缓存数据'})
        
        return jsonify({'success': False, 'error': error_msg})

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
            
            if report_type == 'balance':
                df = retry_with_backoff(lambda: ak.stock_financial_analysis_indicator(symbol=code))
            elif report_type == 'income':
                df = retry_with_backoff(lambda: ak.stock_financial_report_sina(symbol=code, symbol_type='main', report_type='income'))
            elif report_type == 'cash':
                df = retry_with_backoff(lambda: ak.stock_financial_report_sina(symbol=code, symbol_type='main', report_type='cash'))
            else:
                df = retry_with_backoff(lambda: ak.stock_financial_analysis_indicator(symbol=code))
        
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            
            if report_type == 'balance':
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='资产负债表'))
            elif report_type == 'income':
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='综合收益表'))
            elif report_type == 'cash':
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='现金流量表'))
            else:
                df = retry_with_backoff(lambda: ak.stock_hk_financial_report(symbol=f"{code}.HK", indicator='资产负债表'))
        
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
    
    cache_params = {'symbol': symbol}
    cached_data = get_cache('prospectus', cache_params)
    
    try:
        if symbol.endswith('.SH') or symbol.endswith('.SZ'):
            code = symbol[:-3]
            
            prospectus_data = retry_with_backoff(lambda: ak.stock_new_stock_em())
            prospectus_data = prospectus_data[prospectus_data['申购代码'].astype(str).str.contains(code) | 
                                              prospectus_data['证券简称'].str.contains(code)]
            
            results = []
            for _, row in prospectus_data.head(10).iterrows():
                results.append({
                    'code': str(row['申购代码']),
                    'name': row['证券简称'],
                    'ipo_date': row.get('上网发行日期', ''),
                    'prospectus_url': row.get('招股书链接', '')
                })
            
            if results:
                set_cache('prospectus', cache_params, results)
                return jsonify({'success': True, 'data': results, 'cached': False})
        
        elif symbol.endswith('.HK'):
            code = symbol[:-3]
            
            hk_prospectus = retry_with_backoff(lambda: ak.stock_hk_new_stock())
            hk_prospectus = hk_prospectus[hk_prospectus['代码'].astype(str).str.contains(code) | 
                                         hk_prospectus['名称'].str.contains(code)]
            
            results = []
            for _, row in hk_prospectus.head(10).iterrows():
                results.append({
                    'code': str(row['代码']),
                    'name': row['名称'],
                    'ipo_date': row.get('招股日期', ''),
                    'prospectus_url': row.get('招股书', '')
                })
            
            if results:
                set_cache('prospectus', cache_params, results)
                return jsonify({'success': True, 'data': results, 'cached': False})
        
        else:
            prospectus_data = retry_with_backoff(lambda: ak.stock_new_stock_em())
            prospectus_data = prospectus_data[prospectus_data['申购代码'].astype(str).str.contains(symbol) | 
                                              prospectus_data['证券简称'].str.contains(symbol)]
            
            results = []
            for _, row in prospectus_data.head(10).iterrows():
                results.append({
                    'code': str(row['申购代码']),
                    'name': row['证券简称'],
                    'ipo_date': row.get('上网发行日期', ''),
                    'prospectus_url': row.get('招股书链接', '')
                })
            
            if results:
                set_cache('prospectus', cache_params, results)
                return jsonify({'success': True, 'data': results, 'cached': False})
            
            hk_prospectus = retry_with_backoff(lambda: ak.stock_hk_new_stock())
            hk_prospectus = hk_prospectus[hk_prospectus['代码'].astype(str).str.contains(symbol) | 
                                         hk_prospectus['名称'].str.contains(symbol)]
            
            results = []
            for _, row in hk_prospectus.head(10).iterrows():
                results.append({
                    'code': str(row['代码']),
                    'name': row['名称'],
                    'ipo_date': row.get('招股日期', ''),
                    'prospectus_url': row.get('招股书', '')
                })
            
            if results:
                set_cache('prospectus', cache_params, results)
                return jsonify({'success': True, 'data': results, 'cached': False})
        
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True})
        
        return jsonify({'success': False, 'error': '未找到招股书信息'})
    
    except Exception as e:
        error_msg = f'网络请求失败: {str(e)}'
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
