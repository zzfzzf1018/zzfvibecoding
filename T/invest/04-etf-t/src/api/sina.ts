import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import { DataSource, LocalCache } from './datasources';

interface SinaETFData {
  symbol: string;
  name: string;
  price: string;
  change: string;
  changePercent: string;
  volume: string;
  turnover: string;
}

const ETF_CODES = [
  'sh510050', 'sh510300', 'sh510500', 'sz159919', 'sz159920', 'sh512880', 'sh513100',
  'sz159915', 'sz159934', 'sh512690', 'sh510880', 'sz159901', 'sz159902', 'sh510180',
  'sh512500', 'sh512400', 'sh512300', 'sz159995', 'sz159949', 'sh513500', 'sh513050',
  'sz159601', 'sz159603', 'sh513300', 'sh510900', 'sh512100', 'sh512010', 'sz159967',
  'sh513130', 'sz159959', 'sh512200', 'sh512800', 'sh510650', 'sz159985', 'sh513030',
];

export class SinaDataSource implements DataSource {
  type: 'sina' = 'sina';
  name = '新浪财经';

  async getETFList(type?: ETFCategory, keyword?: string): Promise<{ etfs: ETF[]; total: number }> {
    const cacheKey = `sina_list_${type || 'all'}_${keyword || ''}`;
    const cached = LocalCache.get<{ etfs: ETF[]; total: number }>(cacheKey);
    if (cached) {
      return cached;
    }

    const etfs: ETF[] = [];

    const sinaData = await this.fetchSinaETFData();

    sinaData.forEach((item) => {
      const code = this.extractCode(item.symbol);
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
        fundCompany: '新浪数据',
        establishDate: '',
        scale: parseFloat(item.turnover) || 0,
        trackingIndex: '',
        category,
        categoryName: this.getCategoryName(category),
        nav: parseFloat(item.price) || 0,
        change: parseFloat(item.change) || 0,
        changePercent: parseFloat(item.changePercent) || 0,
        oneYearReturn: 0,
        pe: 0,
        pb: 0,
      });
    });

    const result = { etfs, total: etfs.length };
    LocalCache.set(cacheKey, result, 1800000);
    return result;
  }

  async getETFDetail(code: string): Promise<ETFDetailResponse> {
    const cacheKey = `sina_detail_${code}`;
    const cached = LocalCache.get<ETFDetailResponse>(cacheKey);
    if (cached) {
      return cached;
    }

    const detail = await this.fetchSinaETFDetail(code);
    LocalCache.set(cacheKey, detail, 1800000);
    return detail;
  }

  private async fetchSinaETFData(): Promise<SinaETFData[]> {
    try {
      const url = `/api/sina/list=${ETF_CODES.join(',')}`;
      const response = await fetch(url, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
        },
      });
      const text = await response.text();

      return this.parseSinaData(text);
    } catch {
      return this.getMockSinaData();
    }
  }

  private parseSinaData(text: string): SinaETFData[] {
    const data: SinaETFData[] = [];
    const lines = text.split('\n');

    lines.forEach((line) => {
      const match = line.match(/var hq_str_(.+?)="(.+?)";/);
      if (match && match.length === 3) {
        const symbol = match[1];
        const fields = match[2].split(',');

        if (fields.length >= 5) {
          const name = fields[0];
          const price = fields[3];
          const change = fields[4];
          const changePercent = fields[5];
          const volume = fields[8];
          const turnover = fields[9];

          data.push({
            symbol,
            name,
            price,
            change,
            changePercent,
            volume,
            turnover,
          });
        }
      }
    });

    return data.length > 0 ? data : this.getMockSinaData();
  }

  private async fetchSinaETFDetail(code: string): Promise<ETFDetailResponse> {
    const symbol = this.getSinaSymbol(code);
    
    try {
      const url = `/api/sina/list=${symbol}`;
      const response = await fetch(url, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
        },
      });
      const text = await response.text();
      const data = this.parseSinaData(text);

      if (data.length > 0) {
        const item = data[0];
        return {
          basic: {
            code,
            name: item.name,
            fullName: item.name,
            fundCompany: '新浪数据',
            establishDate: '',
            latestReportDate: '',
            scale: parseFloat(item.turnover) || 0,
            scaleDate: '',
            trackingIndex: '',
            indexCode: '',
            creationDate: '',
            managementFeeRate: 0,
            custodianFeeRate: 0,
            salesServiceFeeRate: 0,
          },
          holdings: [],
          fees: {
            managementFee: 0,
            custodianFee: 0,
            salesServiceFee: 0,
            subscriptionFee: 0,
            redemptionFee: 0,
            managementFeeRate: '',
            custodianFeeRate: '',
            salesServiceFeeRate: '',
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

    return this.getMockSinaDetail(code);
  }

  private getSinaSymbol(code: string): string {
    if (code.startsWith('5')) {
      return `sh${code}`;
    }
    return `sz${code}`;
  }

  private extractCode(symbol: string): string {
    return symbol.replace('sh', '').replace('sz', '');
  }

  private mapCategory(code: string): ETFCategory {
    const broadCodes = ['510050', '510300', '510500', '159919', '159920', '510180', '510900'];
    const industryCodes = ['512880', '512100', '512010', '512200', '512800', '512690', '512500', '512400', '512300'];
    const crossBorderCodes = ['513100', '513500', '513050', '513300', '513130', '513030', '159601', '159603'];
    const bondCodes = ['511260', '511010'];

    if (broadCodes.includes(code)) return 'broad';
    if (industryCodes.includes(code)) return 'industry';
    if (crossBorderCodes.includes(code)) return 'cross-border';
    if (bondCodes.includes(code)) return 'bond';
    return 'theme';
  }

  private getCategoryName(category: ETFCategory): string {
    const names: Record<ETFCategory, string> = {
      'broad': '宽基',
      'industry': '行业',
      'theme': '主题',
      'bond': '债券',
      'cross-border': '跨境',
    };
    return names[category];
  }

  private getMockSinaData(): SinaETFData[] {
    return [
      { symbol: 'sh510050', name: '上证50ETF', price: '2.653', change: '0.012', changePercent: '0.45', volume: '125680000', turnover: '3333333' },
      { symbol: 'sh510300', name: '沪深300ETF', price: '4.125', change: '-0.008', changePercent: '-0.19', volume: '98540000', turnover: '4065432' },
      { symbol: 'sh510500', name: '中证500ETF', price: '6.856', change: '0.023', changePercent: '0.34', volume: '67890000', turnover: '4654321' },
      { symbol: 'sz159919', name: '沪深300ETF', price: '4.118', change: '-0.010', changePercent: '-0.24', volume: '87650000', turnover: '3606789' },
      { symbol: 'sh512880', name: '证券ETF', price: '1.123', change: '0.005', changePercent: '0.45', volume: '156780000', turnover: '1759876' },
      { symbol: 'sh513100', name: '纳指ETF', price: '1.890', change: '0.025', changePercent: '1.34', volume: '45670000', turnover: '8631630' },
      { symbol: 'sz159915', name: '创业板ETF', price: '2.345', change: '0.015', changePercent: '0.64', volume: '78900000', turnover: '1849225' },
      { symbol: 'sh510880', name: '红利ETF', price: '2.890', change: '0.008', changePercent: '0.28', volume: '34560000', turnover: '9987840' },
      { symbol: 'sz159934', name: '黄金ETF', price: '4.567', change: '0.032', changePercent: '0.70', volume: '23450000', turnover: '10703455' },
      { symbol: 'sh512690', name: '酒ETF', price: '1.234', change: '-0.006', changePercent: '-0.48', volume: '56780000', turnover: '6996652' },
      { symbol: 'sz159901', name: '深证100ETF', price: '5.678', change: '0.018', changePercent: '0.32', volume: '45670000', turnover: '25916406' },
      { symbol: 'sz159902', name: '中小板ETF', price: '3.456', change: '-0.005', changePercent: '-0.14', volume: '23450000', turnover: '8099780' },
      { symbol: 'sh512500', name: '银行ETF', price: '1.023', change: '0.003', changePercent: '0.30', volume: '89010000', turnover: '9105723' },
      { symbol: 'sh512400', name: '券商ETF', price: '1.156', change: '0.008', changePercent: '0.70', volume: '76540000', turnover: '8848924' },
      { symbol: 'sh512300', name: '医药ETF', price: '0.987', change: '-0.002', changePercent: '-0.20', volume: '65430000', turnover: '6468941' },
      { symbol: 'sz159995', name: '芯片ETF', price: '1.567', change: '0.025', changePercent: '1.62', volume: '98760000', turnover: '15475692' },
      { symbol: 'sz159949', name: '创业板50ETF', price: '1.890', change: '0.012', changePercent: '0.64', volume: '56780000', turnover: '10731420' },
      { symbol: 'sh513500', name: '日经ETF', price: '1.234', change: '-0.003', changePercent: '-0.24', volume: '12340000', turnover: '1522756' },
      { symbol: 'sh513050', name: '中概互联ETF', price: '1.678', change: '0.018', changePercent: '1.08', volume: '45670000', turnover: '7663426' },
      { symbol: 'sz159601', name: '纳斯达克ETF', price: '2.345', change: '0.045', changePercent: '1.95', volume: '34560000', turnover: '8105320' },
    ];
  }

  private getMockSinaDetail(code: string): ETFDetailResponse {
    return {
      basic: {
        code,
        name: 'ETF名称',
        fullName: 'ETF全称',
        fundCompany: '新浪数据',
        establishDate: '',
        latestReportDate: '',
        scale: 0,
        scaleDate: '',
        trackingIndex: '',
        indexCode: '',
        creationDate: '',
        managementFeeRate: 0,
        custodianFeeRate: 0,
        salesServiceFeeRate: 0,
      },
      holdings: [],
      fees: {
        managementFee: 0.5,
        custodianFee: 0.1,
        salesServiceFee: 0,
        subscriptionFee: 0,
        redemptionFee: 0,
        managementFeeRate: '',
        custodianFeeRate: '',
        salesServiceFeeRate: '',
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
}