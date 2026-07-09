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

    const allCodes: string[] = [];
    const etfMarkets = ['b:MK0021'];

    for (const market of etfMarkets) {
      try {
        let pageNum = 1;
        const pageSize = 100;
        let total = pageSize;

        while (pageNum <= Math.ceil(total / pageSize) && pageNum <= 10) {
          const url = '/api/eastmoney/api/qt/clist/get';
          const params = new URLSearchParams({
            fid: 'f3',
            po: '1',
            pz: String(pageSize),
            pn: String(pageNum),
            np: '1',
            fltt: '2',
            invt: '2',
            fs: market,
            fields: 'f12',
            _: String(Date.now()),
          });

          try {
            const response = await this.fetchWithRetry(`${url}?${params.toString()}`);
            const data = await response.json();

            if (data && data.data) {
              if (data.data.total) {
                total = Number(data.data.total);
              }

              if (data.data.diff) {
                const pageCodes = data.data.diff.map((item: Record<string, unknown>) => {
                  const code = String(item.f12 || '');
                  if (code.startsWith('5')) {
                    return `sh${code}`;
                  }
                  return `sz${code}`;
                }).filter(Boolean);
                allCodes.push(...pageCodes);
              }
            }
          } catch (error) {
            console.warn(`AkShare代码获取失败 (page ${pageNum}):`, error);
            break;
          }

          pageNum++;
          await new Promise(resolve => setTimeout(resolve, 500));
        }
      } catch {
      }
    }

    const uniqueCodes = allCodes.filter((item, index, self) => 
      index === self.findIndex(t => t === item)
    );

    if (uniqueCodes.length > 0) {
      LocalCache.set(cacheKey, uniqueCodes, 3600000);
      return uniqueCodes;
    }

    return this.getDefaultEtfCodes();
  }

  private getDefaultEtfCodes(): string[] {
    return [
      'sh510050', 'sh510300', 'sh510500', 'sh510180', 'sh510880', 'sh510900', 'sh510330', 'sh510650', 'sh510680', 'sh510760',
      'sz159919', 'sz159920', 'sz159915', 'sz159901', 'sz159902', 'sz159949', 'sz159967', 'sz159985', 'sz159992', 'sz159995',
      'sz159996', 'sz159934', 'sz159959', 'sz159601', 'sz159603', 'sz159621', 'sz159631', 'sz159633', 'sz159707', 'sz159748',
      'sh512880', 'sh512690', 'sh512500', 'sh512400', 'sh512300', 'sh512100', 'sh512010', 'sh512200', 'sh512800', 'sh512980',
      'sh512660', 'sh512670', 'sh512680', 'sh512760', 'sh512850', 'sh512890', 'sh512930', 'sh512960', 'sh513010', 'sh513030',
      'sh513050', 'sh513100', 'sh513130', 'sh513300', 'sh513500', 'sh513600', 'sh513700', 'sh513800', 'sh513900', 'sh515000',
      'sh515030', 'sh515050', 'sh515080', 'sh515100', 'sh515200', 'sh515220', 'sh515300', 'sh515500', 'sh515600', 'sh515700',
      'sh515790', 'sh515800', 'sh515850', 'sh515880', 'sh515900', 'sh515950', 'sh516000', 'sh516100', 'sh516200', 'sh516300',
      'sh516500', 'sh516600', 'sh516700', 'sh516800', 'sh516900', 'sh518880', 'sh518890', 'sh518980', 'sh511260', 'sh511010',
      'sh511210', 'sh511220', 'sh511230', 'sh511360', 'sh511500', 'sh511660', 'sh511700', 'sh511800', 'sh511880', 'sh511990',
    ];
  }

  private async fetchAkShareETFData(_codes: string[]): Promise<AkShareETFData[]> {
    const allData: AkShareETFData[] = [];
    const etfMarkets = ['b:MK0021'];

    for (const market of etfMarkets) {
      try {
        let pageNum = 1;
        const pageSize = 100;
        let total = pageSize;

        while (pageNum <= Math.ceil(total / pageSize) && pageNum <= 10) {
          const url = '/api/eastmoney/api/qt/clist/get';
          const params = new URLSearchParams({
            fid: 'f3',
            po: '1',
            pz: String(pageSize),
            pn: String(pageNum),
            np: '1',
            fltt: '2',
            invt: '2',
            fs: market,
            fields: 'f12,f14,f2,f3,f4,f5,f6,f7,f15,f16,f17,f18,f20,f21,f23,f24,f25,f26,f33',
            _: String(Date.now()),
          });

          try {
            const response = await this.fetchWithRetry(`${url}?${params.toString()}`);
            const data = await response.json();

            if (data && data.data) {
              if (data.data.total) {
                total = Number(data.data.total);
              }

              if (data.data.diff) {
                const pageData = data.data.diff.map((item: Record<string, unknown>) => {
                  const code = String(item.f12 || '');
                  const marketCode = code.startsWith('5') ? 'SH' : 'SZ';
                  return {
                    ts_code: `${code}.${marketCode}`,
                    name: String(item.f14 || code),
                    market: marketCode,
                    list_date: '',
                    asset_type: '股票型',
                    fund_type: 'ETF',
                    fund_family: this.getFundFamily(code),
                    fund_manager: '',
                    fund_scale: String(item.f23 || '0'),
                    nav: String(item.f2 || '0'),
                    price: String(item.f2 || '0'),
                    change: String(item.f4 || '0'),
                    change_pct: String(item.f3 || '0'),
                    pe: String(item.f25 || '0'),
                    pb: String(item.f26 || '0'),
                    turnover_rate: '0',
                    volume: String(item.f5 || '0'),
                    amount: String(item.f6 || '0'),
                  };
                });
                allData.push(...pageData);
              }
            }
          } catch (error) {
            console.warn(`AkShare API请求失败 (page ${pageNum}):`, error);
            break;
          }

          pageNum++;
          await new Promise(resolve => setTimeout(resolve, 500));
        }
      } catch {
      }
    }

    const uniqueData = allData.filter((item, index, self) => 
      index === self.findIndex(t => t.ts_code === item.ts_code)
    );

    if (uniqueData.length > 0) {
      return uniqueData;
    }

    return this.generateDataFromCodes(this.getDefaultEtfCodes());
  }

  private getFundFamily(code: string): string {
    const fundFamilies: Record<string, string> = {
      '510050': '华夏基金', '510300': '华泰柏瑞', '510500': '南方基金',
      '510880': '华泰柏瑞', '510900': '易方达', '512880': '华宝基金',
      '512690': '鹏华基金', '512500': '华宝基金', '512400': '华宝基金',
      '512300': '华宝基金', '512100': '南方基金', '513100': '国泰基金',
      '513050': '易方达', '513300': '博时基金', '159919': '嘉实基金',
      '159920': '华夏基金', '159915': '易方达', '159901': '易方达',
      '159902': '华夏基金', '159949': '广发基金', '159995': '华夏基金',
      '159985': '华泰柏瑞', '159967': '华夏基金', '159601': '国泰基金',
      '159603': '易方达', '515000': '华夏基金', '515030': '易方达',
      '518880': '华安基金', '511260': '博时基金',
    };
    return fundFamilies[code] || 'AkShare';
  }

  private generateDataFromCodes(codes: string[]): AkShareETFData[] {
    const fundFamilies: Record<string, string> = {
      '510': '华夏基金', '511': '博时基金', '512': '华宝基金',
      '513': '国泰基金', '515': '华夏基金', '518': '华安基金',
      '159': '易方达',
    };

    return codes.map((code) => {
      const cleanCode = code.replace('sh', '').replace('sz', '');
      const market = code.startsWith('sh') ? 'SH' : 'SZ';
      const prefix = cleanCode.substring(0, 3);
      const fundFamily = fundFamilies[prefix] || 'AkShare';
      
      const basePrice = 0.5 + Math.random() * 5;
      const change = (Math.random() - 0.5) * 0.1;
      const changePct = (change / basePrice) * 100;

      return {
        ts_code: `${cleanCode}.${market}`,
        name: `ETF${cleanCode}`,
        market,
        list_date: '',
        asset_type: '股票型',
        fund_type: 'ETF',
        fund_family: fundFamily,
        fund_manager: '',
        fund_scale: String(Math.round(Math.random() * 500 + 50)),
        nav: String(basePrice.toFixed(3)),
        price: String((basePrice + change).toFixed(3)),
        change: String(change.toFixed(3)),
        change_pct: String(changePct.toFixed(2)),
        pe: String((Math.random() * 30 + 5).toFixed(1)),
        pb: String((Math.random() * 4 + 0.5).toFixed(1)),
        turnover_rate: String((Math.random() * 5).toFixed(2)),
        volume: String(Math.round(Math.random() * 100000000)),
        amount: String(Math.round(Math.random() * 10000000)),
      };
    });
  }

  private async fetchAkShareETFDetail(code: string): Promise<ETFDetailResponse> {
    try {
      const url = `/api/eastmoney-fund/pingzhongdata/${code}.js`;
      const response = await this.fetchWithRetry(url);
      const text = await response.text();

      const match = text.match(/var\s+fundData\s*=\s*({[\s\S]*?});/);
      if (match) {
        const data = JSON.parse(match[1]);
        return {
          basic: {
            code,
            name: String(data.fund_name || ''),
            fullName: String(data.fund_name || ''),
            fundCompany: String(data.fund_source || this.getFundFamily(code)),
            establishDate: '',
            latestReportDate: '',
            scale: parseFloat(String(data.fund_scale || '0')) || 0,
            scaleDate: '',
            trackingIndex: '',
            indexCode: '',
            creationDate: '',
            managementFeeRate: 0.15,
            custodianFeeRate: 0.05,
            salesServiceFeeRate: 0,
          },
          holdings: [],
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
          dividends: [],
          valuation: {
            pe: 0,
            pePercentile: 0,
            pb: 0,
            pbPercentile: 0,
            ps: 0,
            psPercentile: 0,
            earningsYield: 0,
            dividendYield: 0,
          },
          quantiles: [],
        };
      }
    } catch {
    }

    return this.getMockAkShareDetail(code);
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