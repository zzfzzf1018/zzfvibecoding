import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import type { DataSource } from './datasources';
import { LocalCache } from './datasources';

interface AkShareETFData {
  ts_code: string;
  name: string;
  market: string;
  list_date: string;
  asset_type: string;
  fund_type: string;
  fund_family: string;
  fund_manager: string;
  fund_scale: string;
  nav: string;
  price: string;
  change: string;
  change_pct: string;
  pe: string;
  pb: string;
  turnover_rate: string;
  volume: string;
  amount: string;
}

export class AkShareDataSource implements DataSource {
  type: 'akshare' = 'akshare';
  name = 'AkShare';

  async getETFList(type?: ETFCategory, keyword?: string): Promise<{ etfs: ETF[]; total: number }> {
    const cacheKey = `akshare_list_${type || 'all'}_${keyword || ''}`;
    const cached = LocalCache.get<{ etfs: ETF[]; total: number }>(cacheKey);
    if (cached) {
      return cached;
    }

    const allEtfCodes = await this.fetchAllEtfCodes();
    const akshareData = await this.fetchAkShareETFData(allEtfCodes);

    const etfs: ETF[] = [];

    akshareData.forEach((item) => {
      const code = item.ts_code.replace('.SH', '').replace('.SZ', '');
      const category = this.mapCategory(code);

      if (type && category !== type) return;

      if (keyword) {
        const lowerKeyword = keyword.toLowerCase();
        if (
          !code.toLowerCase().includes(lowerKeyword) &&
          !item.name.toLowerCase().includes(lowerKeyword)
        ) {
          return;
        }
      }

      etfs.push({
        code,
        name: item.name,
        fullName: item.name,
        fundCompany: item.fund_family || 'AkShare',
        establishDate: item.list_date || '',
        scale: parseFloat(item.fund_scale) || 0,
        trackingIndex: '',
        category,
        categoryName: this.getCategoryName(category),
        nav: parseFloat(item.nav) || parseFloat(item.price) || 0,
        change: parseFloat(item.change) || 0,
        changePercent: parseFloat(item.change_pct) || 0,
        oneYearReturn: 0,
        pe: parseFloat(item.pe) || 0,
        pb: parseFloat(item.pb) || 0,
      });
    });

    const result = { etfs, total: etfs.length };
    LocalCache.set(cacheKey, result, 1800000);
    return result;
  }

  async getETFDetail(code: string): Promise<ETFDetailResponse> {
    const cacheKey = `akshare_detail_${code}`;
    const cached = LocalCache.get<ETFDetailResponse>(cacheKey);
    if (cached) {
      return cached;
    }

    const detail = await this.fetchAkShareETFDetail(code);
    LocalCache.set(cacheKey, detail, 1800000);
    return detail;
  }

  private async fetchWithRetry(url: string, maxRetries: number = 3, delayMs: number = 1000): Promise<Response> {
    for (let i = 0; i < maxRetries; i++) {
      try {
        const response = await fetch(url, {
          headers: {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
          },
        });
        if (response.ok) {
          return response;
        }
      } catch {
      }
      if (i < maxRetries - 1) {
        await new Promise(resolve => setTimeout(resolve, delayMs * Math.pow(2, i)));
      }
    }
    throw new Error('Max retries exceeded');
  }

  private async fetchAllEtfCodes(): Promise<string[]> {
    const cacheKey = 'akshare_all_etf_codes';
    const cached = LocalCache.get<string[]>(cacheKey);
    if (cached) {
      return cached;
    }

    try {
      const url = '/api/eastmoney/api/qt/clist/get';
      const params = new URLSearchParams({
        fid: 'f3',
        po: '1',
        pz: '500',
        pn: '1',
        np: '1',
        fltt: '2',
        invt: '2',
        fs: 'b:MK0021',
        fields: 'f12',
        _: String(Date.now()),
      });

      const response = await this.fetchWithRetry(`${url}?${params.toString()}`);
      const data = await response.json();

      if (data && data.data && data.data.diff) {
        const codes = data.data.diff.map((item: Record<string, unknown>) => {
          const code = String(item.f12 || '');
          if (code.startsWith('5')) {
            return `sh${code}`;
          }
          return `sz${code}`;
        }).filter(Boolean);
        LocalCache.set(cacheKey, codes, 3600000);
        return codes;
      }
    } catch {
    }

    return this.getDefaultEtfCodes();
  }

  private getDefaultEtfCodes(): string[] {
    return [
      'sh510050', 'sh510300', 'sh510500', 'sh510180', 'sh510880', 'sh510900',
      'sz159919', 'sz159920', 'sz159915', 'sz159901', 'sz159902', 'sz159949',
      'sh512880', 'sh512690', 'sh512500', 'sh512400', 'sh512300', 'sh512100',
      'sh513100', 'sh513500', 'sh513050', 'sh513300', 'sz159601', 'sz159603',
      'sz159995', 'sz159985', 'sh515000', 'sh515030', 'sh518880', 'sh511260',
    ];
  }

  private async fetchAkShareETFData(codes: string[]): Promise<AkShareETFData[]> {
    if (codes.length === 0) {
      return this.getMockAkShareData();
    }

    try {
      const url = '/api/akshare/stock_etf_hist_em';
      const params = new URLSearchParams({
        symbol: '',
        period: 'daily',
        start_date: '',
        end_date: '',
        adjust: '',
        _: String(Date.now()),
      });

      const response = await this.fetchWithRetry(`${url}?${params.toString()}`);
      const data = await response.json();

      if (data && data.data && Array.isArray(data.data)) {
        return data.data.map((item: Record<string, unknown>) => ({
          ts_code: String(item.ts_code || ''),
          name: String(item.name || ''),
          market: String(item.market || ''),
          list_date: String(item.list_date || ''),
          asset_type: String(item.asset_type || ''),
          fund_type: String(item.fund_type || ''),
          fund_family: String(item.fund_family || ''),
          fund_manager: String(item.fund_manager || ''),
          fund_scale: String(item.fund_scale || '0'),
          nav: String(item.nav || '0'),
          price: String(item.price || '0'),
          change: String(item.change || '0'),
          change_pct: String(item.change_pct || '0'),
          pe: String(item.pe || '0'),
          pb: String(item.pb || '0'),
          turnover_rate: String(item.turnover_rate || '0'),
          volume: String(item.volume || '0'),
          amount: String(item.amount || '0'),
        }));
      }
    } catch {
    }

    return this.getMockAkShareData();
  }

  private async fetchAkShareETFDetail(code: string): Promise<ETFDetailResponse> {
    try {
      const url = '/api/akshare/fund_open_fund_info_em';
      const params = new URLSearchParams({
        symbol: code,
        _: String(Date.now()),
      });

      const response = await this.fetchWithRetry(`${url}?${params.toString()}`);
      const data = await response.json();

      if (data && data.data) {
        const detail = data.data;
        return {
          basic: {
            code,
            name: String(detail.fund_name || ''),
            fullName: String(detail.fund_fullname || ''),
            fundCompany: String(detail.fund_company || ''),
            establishDate: String(detail.found_date || ''),
            latestReportDate: '',
            scale: parseFloat(String(detail.fund_scale || '0')) || 0,
            scaleDate: '',
            trackingIndex: String(detail.track_index || ''),
            indexCode: '',
            creationDate: '',
            managementFeeRate: parseFloat(String(detail.management_fee || '0')) || 0,
            custodianFeeRate: parseFloat(String(detail.custodian_fee || '0')) || 0,
            salesServiceFeeRate: 0,
          },
          holdings: (detail.holdings || []).map((h: Record<string, unknown>, index: number) => ({
            rank: index + 1,
            stockCode: String(h.code || ''),
            stockName: String(h.name || ''),
            weight: parseFloat(String(h.proportion || '0')) || 0,
            marketValue: parseFloat(String(h.quantity || '0')) || 0,
            changePercent: 0,
          })),
          fees: {
            managementFee: parseFloat(String(detail.management_fee || '0')) || 0,
            custodianFee: parseFloat(String(detail.custodian_fee || '0')) || 0,
            salesServiceFee: 0,
            subscriptionFee: parseFloat(String(detail.subscription_fee || '0')) || 0,
            redemptionFee: parseFloat(String(detail.redemption_fee || '0')) || 0,
            managementFeeRate: String(detail.management_fee || '0'),
            custodianFeeRate: String(detail.custodian_fee || '0'),
            salesServiceFeeRate: '0',
          },
          dividends: (detail.dividends || []).map((d: Record<string, unknown>) => ({
            dividendDate: String(d.date || ''),
            exDividendDate: '',
            dividendAmount: parseFloat(String(d.amount || '0')) || 0,
            dividendType: String(d.type || '分红'),
            recordDate: '',
          })),
          valuation: {
            pe: parseFloat(String(detail.pe || '0')) || 0,
            pePercentile: 0,
            pb: parseFloat(String(detail.pb || '0')) || 0,
            pbPercentile: 0,
            ps: parseFloat(String(detail.ps || '0')) || 0,
            psPercentile: 0,
            earningsYield: 0,
            dividendYield: 0,
          },
          quantiles: (detail.valuation_history || []).map((v: Record<string, unknown>) => ({
            date: String(v.date || ''),
            pe: parseFloat(String(v.pe || '0')) || 0,
            pePercentile: parseFloat(String(v.pe_percentile || '0')) || 0,
            pb: parseFloat(String(v.pb || '0')) || 0,
            pbPercentile: parseFloat(String(v.pb_percentile || '0')) || 0,
          })),
        };
      }
    } catch {
    }

    return this.getMockAkShareDetail(code);
  }

  private getMockAkShareData(): AkShareETFData[] {
    return [
      { ts_code: '510050.SH', name: '上证50ETF', market: 'SH', list_date: '2004-01-02',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华夏基金', fund_manager: '',
        fund_scale: '500.5', nav: '2.653', price: '2.653', change: '0.012', change_pct: '0.45',
        pe: '11.2', pb: '1.3', turnover_rate: '2.5', volume: '125680000', amount: '3333333' },
      { ts_code: '510300.SH', name: '沪深300ETF', market: 'SH', list_date: '2012-05-04',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华泰柏瑞', fund_manager: '',
        fund_scale: '450.8', nav: '4.125', price: '4.125', change: '-0.008', change_pct: '-0.19',
        pe: '12.5', pb: '1.4', turnover_rate: '1.8', volume: '98540000', amount: '4065432' },
      { ts_code: '510500.SH', name: '中证500ETF', market: 'SH', list_date: '2013-02-06',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '南方基金', fund_manager: '',
        fund_scale: '320.3', nav: '6.856', price: '6.856', change: '0.023', change_pct: '0.34',
        pe: '22.8', pb: '1.8', turnover_rate: '2.1', volume: '67890000', amount: '4654321' },
      { ts_code: '159919.SZ', name: '沪深300ETF', market: 'SZ', list_date: '2012-05-07',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '嘉实基金', fund_manager: '',
        fund_scale: '380.6', nav: '4.118', price: '4.118', change: '-0.010', change_pct: '-0.24',
        pe: '12.4', pb: '1.4', turnover_rate: '2.0', volume: '87650000', amount: '3606789' },
      { ts_code: '512880.SH', name: '证券ETF', market: 'SH', list_date: '2016-05-03',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华宝基金', fund_manager: '',
        fund_scale: '280.4', nav: '1.123', price: '1.123', change: '0.005', change_pct: '0.45',
        pe: '18.5', pb: '1.9', turnover_rate: '5.2', volume: '156780000', amount: '1759876' },
      { ts_code: '513100.SH', name: '纳指ETF', market: 'SH', list_date: '2013-05-15',
        asset_type: '股票型', fund_type: 'QDII-ETF', fund_family: '国泰基金', fund_manager: '',
        fund_scale: '180.2', nav: '1.890', price: '1.890', change: '0.025', change_pct: '1.34',
        pe: '25.8', pb: '3.2', turnover_rate: '3.5', volume: '45670000', amount: '8631630' },
      { ts_code: '159915.SZ', name: '创业板ETF', market: 'SZ', list_date: '2011-09-20',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '易方达', fund_manager: '',
        fund_scale: '250.9', nav: '2.345', price: '2.345', change: '0.015', change_pct: '0.64',
        pe: '35.6', pb: '4.2', turnover_rate: '3.8', volume: '78900000', amount: '1849225' },
      { ts_code: '510880.SH', name: '红利ETF', market: 'SH', list_date: '2006-11-16',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华泰柏瑞', fund_manager: '',
        fund_scale: '120.5', nav: '2.890', price: '2.890', change: '0.008', change_pct: '0.28',
        pe: '8.5', pb: '1.1', turnover_rate: '1.2', volume: '34560000', amount: '9987840' },
      { ts_code: '159934.SZ', name: '黄金ETF', market: 'SZ', list_date: '2013-07-15',
        asset_type: '商品型', fund_type: 'ETF', fund_family: '华安基金', fund_manager: '',
        fund_scale: '90.3', nav: '4.567', price: '4.567', change: '0.032', change_pct: '0.70',
        pe: '0', pb: '0', turnover_rate: '1.5', volume: '23450000', amount: '10703455' },
      { ts_code: '512690.SH', name: '酒ETF', market: 'SH', list_date: '2019-04-25',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '鹏华基金', fund_manager: '',
        fund_scale: '150.6', nav: '1.234', price: '1.234', change: '-0.006', change_pct: '-0.48',
        pe: '28.5', pb: '5.2', turnover_rate: '4.2', volume: '56780000', amount: '6996652' },
      { ts_code: '512500.SH', name: '银行ETF', market: 'SH', list_date: '2016-07-26',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华宝基金', fund_manager: '',
        fund_scale: '200.8', nav: '1.023', price: '1.023', change: '0.003', change_pct: '0.30',
        pe: '6.8', pb: '0.9', turnover_rate: '2.5', volume: '89010000', amount: '9105723' },
      { ts_code: '512400.SH', name: '券商ETF', market: 'SH', list_date: '2016-03-18',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华宝基金', fund_manager: '',
        fund_scale: '220.4', nav: '1.156', price: '1.156', change: '0.008', change_pct: '0.70',
        pe: '19.2', pb: '2.1', turnover_rate: '4.8', volume: '76540000', amount: '8848924' },
      { ts_code: '512300.SH', name: '医药ETF', market: 'SH', list_date: '2019-08-28',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华宝基金', fund_manager: '',
        fund_scale: '180.7', nav: '0.987', price: '0.987', change: '-0.002', change_pct: '-0.20',
        pe: '24.5', pb: '3.5', turnover_rate: '3.2', volume: '65430000', amount: '6468941' },
      { ts_code: '159995.SZ', name: '芯片ETF', market: 'SZ', list_date: '2019-06-20',
        asset_type: '股票型', fund_type: 'ETF', fund_family: '华夏基金', fund_manager: '',
        fund_scale: '260.3', nav: '1.567', price: '1.567', change: '0.025', change_pct: '1.62',
        pe: '45.2', pb: '5.8', turnover_rate: '6.5', volume: '98760000', amount: '15475692' },
      { ts_code: '513050.SH', name: '中概互联ETF', market: 'SH', list_date: '2019-10-15',
        asset_type: '股票型', fund_type: 'QDII-ETF', fund_family: '易方达', fund_manager: '',
        fund_scale: '150.2', nav: '1.678', price: '1.678', change: '0.018', change_pct: '1.08',
        pe: '22.5', pb: '4.5', turnover_rate: '3.8', volume: '45670000', amount: '7663426' },
    ];
  }

  private getMockAkShareDetail(code: string): ETFDetailResponse {
    return {
      basic: {
        code,
        name: 'ETF详情',
        fullName: 'ETF全称',
        fundCompany: 'AkShare数据',
        establishDate: '2020-01-01',
        latestReportDate: '',
        scale: 100,
        scaleDate: '',
        trackingIndex: '',
        indexCode: '',
        creationDate: '',
        managementFeeRate: 0.15,
        custodianFeeRate: 0.05,
        salesServiceFeeRate: 0,
      },
      holdings: [
        { rank: 1, stockCode: '600000', stockName: '股票1', weight: 10, marketValue: 1000000, changePercent: 0 },
        { rank: 2, stockCode: '600001', stockName: '股票2', weight: 8, marketValue: 800000, changePercent: 0 },
        { rank: 3, stockCode: '600002', stockName: '股票3', weight: 6, marketValue: 600000, changePercent: 0 },
      ],
      fees: {
        managementFee: 0.15,
        custodianFee: 0.05,
        salesServiceFee: 0,
        subscriptionFee: 0.8,
        redemptionFee: 0.5,
        managementFeeRate: '0.15',
        custodianFeeRate: '0.05',
        salesServiceFeeRate: '0',
      },
      dividends: [
        { dividendDate: '2023-06-30', exDividendDate: '', dividendAmount: 0.05, dividendType: '分红', recordDate: '' },
        { dividendDate: '2022-12-31', exDividendDate: '', dividendAmount: 0.04, dividendType: '分红', recordDate: '' },
      ],
      valuation: {
        pe: 15,
        pePercentile: 50,
        pb: 1.8,
        pbPercentile: 45,
        ps: 0,
        psPercentile: 0,
        earningsYield: 0,
        dividendYield: 0,
      },
      quantiles: [
        { date: '2024-01-01', pe: 15, pePercentile: 50, pb: 1.8, pbPercentile: 45 },
        { date: '2023-12-01', pe: 14, pePercentile: 45, pb: 1.7, pbPercentile: 40 },
        { date: '2023-11-01', pe: 14.5, pePercentile: 47, pb: 1.75, pbPercentile: 42 },
      ],
    };
  }

  private mapCategory(code: string): ETFCategory {
    const broadCodes = ['510050', '510300', '510500', '159919', '159920', '510180', '510900', '510330', '510650', '159901', '159902', '159949', '159967', '159985'];
    const industryCodes = ['512880', '512100', '512010', '512200', '512800', '512690', '512500', '512400', '512300', '512660', '512670', '512680', '512980'];
    const crossBorderCodes = ['513100', '513500', '513050', '513300', '513130', '513030', '159601', '159603', '513010'];
    const bondCodes = ['511260', '511010', '511210', '511500', '511800'];

    if (broadCodes.includes(code)) return 'broad';
    if (industryCodes.includes(code)) return 'industry';
    if (crossBorderCodes.includes(code)) return 'cross-border';
    if (bondCodes.includes(code)) return 'bond';
    return 'theme';
  }

  private getCategoryName(category: ETFCategory): string {
    const names: Record<ETFCategory, string> = {
      broad: '宽基指数',
      industry: '行业ETF',
      theme: '主题ETF',
      'cross-border': '跨境ETF',
      bond: '债券ETF',
    };
    return names[category] || '其他';
  }
}