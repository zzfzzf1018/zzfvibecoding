import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import { DataSource, LocalCache } from './datasources';

const SINA_ETF_LIST_URL = 'https://finance.sina.com.cn/stock/';

interface SinaETFData {
  symbol: string;
  name: string;
  price: string;
  change: string;
  changePercent: string;
  volume: string;
  turnover: string;
}

export class SinaDataSource implements DataSource {
  type: 'sina' = 'sina';
  name = '新浪财经';

  async getETFList(type?: ETFCategory, keyword?: string): Promise<{ etfs: ETF[]; total: number }> {
    const cacheKey = `list_${type || 'all'}_${keyword || ''}`;
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
    const cacheKey = `detail_${code}`;
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
      const response = await fetch(SINA_ETF_LIST_URL, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
        },
      });
      const html = await response.text();

      return this.parseSinaHTML(html);
    } catch {
      return this.getMockSinaData();
    }
  }

  private parseSinaHTML(html: string): SinaETFData[] {
    const data: SinaETFData[] = [];
    const regex = /<tr.*?>(.*?)<\/tr>/gi;
    let match;

    while ((match = regex.exec(html)) !== null) {
      const row = match[1];
      const cells = row.match(/<td[^>]*>(.*?)<\/td>/gi);
      if (cells && cells.length >= 6) {
        const symbol = cells[0].replace(/<[^>]*>/g, '').trim();
        const name = cells[1].replace(/<[^>]*>/g, '').trim();
        const price = cells[2].replace(/<[^>]*>/g, '').trim();
        const change = cells[3].replace(/<[^>]*>/g, '').trim();
        const changePercent = cells[4].replace(/<[^>]*>/g, '').trim();
        const volume = cells[5].replace(/<[^>]*>/g, '').trim();

        if (symbol && name && price) {
          data.push({
            symbol,
            name,
            price,
            change,
            changePercent,
            volume,
            turnover: '0',
          });
        }
      }
    }

    return data.length > 0 ? data : this.getMockSinaData();
  }

  private getMockSinaData(): SinaETFData[] {
    return [
      { symbol: 'sh510050', name: '上证50ETF', price: '2.6530', change: '0.0520', changePercent: '2.00', volume: '1000000', turnover: '567.89' },
      { symbol: 'sh510300', name: '沪深300ETF', price: '4.1250', change: '0.0780', changePercent: '1.92', volume: '1200000', turnover: '892.45' },
      { symbol: 'sh510500', name: '中证500ETF', price: '6.8920', change: '0.1350', changePercent: '1.98', volume: '800000', turnover: '345.67' },
      { symbol: 'sz159919', name: '创业板ETF', price: '2.3450', change: '0.0480', changePercent: '2.09', volume: '900000', turnover: '289.34' },
      { symbol: 'sh512880', name: '证券ETF', price: '1.1230', change: '0.0450', changePercent: '4.15', volume: '1500000', turnover: '234.56' },
      { symbol: 'sh513100', name: '纳指ETF', price: '1.8920', change: '0.0350', changePercent: '1.88', volume: '600000', turnover: '156.78' },
      { symbol: 'sh512480', name: '银行ETF', price: '1.2340', change: '0.0180', changePercent: '1.48', volume: '700000', turnover: '189.78' },
      { symbol: 'sh512170', name: '医疗ETF', price: '0.8760', change: '0.0320', changePercent: '3.78', volume: '850000', turnover: '312.45' },
      { symbol: 'sh512690', name: '酒ETF', price: '1.5670', change: '0.0560', changePercent: '3.72', volume: '500000', turnover: '123.45' },
      { symbol: 'sh513500', name: '中概互联ETF', price: '1.2340', change: '0.0280', changePercent: '2.31', volume: '1100000', turnover: '456.23' },
      { symbol: 'sh513050', name: '港股通ETF', price: '1.0890', change: '0.0210', changePercent: '1.97', volume: '950000', turnover: '234.56' },
      { symbol: 'sh511260', name: '城投债ETF', price: '101.23', change: '0.15', changePercent: '0.15', volume: '300000', turnover: '56.78' },
      { symbol: 'sh511280', name: '国债ETF', price: '102.34', change: '0.08', changePercent: '0.08', volume: '400000', turnover: '123.45' },
      { symbol: 'sh512400', name: '有色ETF', price: '1.2340', change: '0.0560', changePercent: '4.78', volume: '1300000', turnover: '156.78' },
      { symbol: 'sh512010', name: '券商ETF', price: '0.9870', change: '0.0420', changePercent: '4.42', volume: '1400000', turnover: '456.78' },
      { symbol: 'sz159920', name: '恒生ETF', price: '1.1230', change: '0.0230', changePercent: '2.08', volume: '750000', turnover: '189.23' },
      { symbol: 'sh512200', name: '房地产ETF', price: '0.6780', change: '0.0230', changePercent: '3.52', volume: '650000', turnover: '89.23' },
      { symbol: 'sh512760', name: '半导体ETF', price: '1.8920', change: '0.0850', changePercent: '4.72', volume: '1600000', turnover: '278.45' },
      { symbol: 'sz159938', name: '军工ETF', price: '0.9870', change: '0.0340', changePercent: '3.56', volume: '780000', turnover: '156.78' },
      { symbol: 'sh512660', name: '军工ETF', price: '1.0230', change: '0.0360', changePercent: '3.67', volume: '820000', turnover: '178.90' },
    ];
  }

  private async fetchSinaETFDetail(code: string): Promise<ETFDetailResponse> {
    await new Promise((resolve) => setTimeout(resolve, 300));

    return {
      basic: {
        code,
        name: 'ETF名称',
        fullName: '完整名称',
        fundCompany: '新浪数据',
        establishDate: '',
        latestReportDate: '',
        scale: 0,
        scaleDate: '',
        trackingIndex: '',
        indexCode: '',
        creationDate: '',
        managementFeeRate: 0.5,
        custodianFeeRate: 0.1,
        salesServiceFeeRate: 0,
      },
      holdings: [],
      fees: {
        managementFee: 0.5,
        custodianFee: 0.1,
        salesServiceFee: 0,
        subscriptionFee: 0,
        redemptionFee: 0,
        managementFeeRate: '0.50%',
        custodianFeeRate: '0.10%',
        salesServiceFeeRate: '0.00%',
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

  private extractCode(symbol: string): string {
    if (symbol.startsWith('sh')) return symbol.substring(2);
    if (symbol.startsWith('sz')) return symbol.substring(2);
    return symbol;
  }

  private mapCategory(code: string): ETFCategory {
    const bondCodes = ['511', '510', '159'];
    const crossBorderCodes = ['513', '159920', '159960'];

    if (bondCodes.some((prefix) => code.startsWith(prefix))) {
      return 'bond';
    }
    if (crossBorderCodes.some((prefix) => code.startsWith(prefix))) {
      return 'cross-border';
    }

    const broadCodes = ['510050', '510300', '510500', '159919', '159952'];
    if (broadCodes.includes(code)) {
      return 'broad';
    }

    const industryCodes = ['512', '515', '1599'];
    if (industryCodes.some((prefix) => code.startsWith(prefix))) {
      return 'industry';
    }

    return 'theme';
  }

  private getCategoryName(category: ETFCategory): string {
    const map = {
      broad: '宽基',
      industry: '行业',
      theme: '主题',
      bond: '债券',
      'cross-border': '跨境',
    };
    return map[category];
  }
}