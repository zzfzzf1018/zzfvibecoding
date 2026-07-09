import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import { DataSource, LocalCache } from './datasources';

interface TencentETFData {
  symbol: string;
  name: string;
  code: string;
  price: string;
  yesterdayClose: string;
  open: string;
  change: string;
  changePercent: string;
  high: string;
  low: string;
  volume: string;
  turnover: string;
  turnoverRate: string;
  pe: string;
  amplitude: string;
  marketCap: string;
}

const ETF_CODES = [
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

export class TencentDataSource implements DataSource {
  type: 'tencent' = 'tencent';
  name = '腾讯财经';

  async getETFList(type?: ETFCategory, keyword?: string): Promise<{ etfs: ETF[]; total: number }> {
    const cacheKey = `tencent_list_${type || 'all'}_${keyword || ''}`;
    const cached = LocalCache.get<{ etfs: ETF[]; total: number }>(cacheKey);
    if (cached) {
      return cached;
    }

    const etfs: ETF[] = [];

    const tencentData = await this.fetchTencentETFData();

    tencentData.forEach((item) => {
      const code = item.code;
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
        fundCompany: '腾讯数据',
        establishDate: '',
        scale: parseFloat(item.marketCap) || 0,
        trackingIndex: '',
        category,
        categoryName: this.getCategoryName(category),
        nav: parseFloat(item.price) || 0,
        change: parseFloat(item.change) || 0,
        changePercent: parseFloat(item.changePercent) || 0,
        oneYearReturn: 0,
        pe: parseFloat(item.pe) || 0,
        pb: 0,
      });
    });

    const result = { etfs, total: etfs.length };
    LocalCache.set(cacheKey, result, 1800000);
    return result;
  }

  async getETFDetail(code: string): Promise<ETFDetailResponse> {
    const cacheKey = `tencent_detail_${code}`;
    const cached = LocalCache.get<ETFDetailResponse>(cacheKey);
    if (cached) {
      return cached;
    }

    const detail = await this.fetchTencentETFDetail(code);
    LocalCache.set(cacheKey, detail, 1800000);
    return detail;
  }

  private async fetchTencentETFData(): Promise<TencentETFData[]> {
    try {
      const url = `/api/tencent/q=${ETF_CODES.join(',')}`;
      const response = await fetch(url, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
        },
      });
      const arrayBuffer = await response.arrayBuffer();
      const text = new TextDecoder('GBK').decode(arrayBuffer);

      return this.parseTencentData(text);
    } catch {
      return this.getMockTencentData();
    }
  }

  private parseTencentData(text: string): TencentETFData[] {
    const data: TencentETFData[] = [];
    const lines = text.split('\n');

    lines.forEach((line) => {
      const match = line.match(/v_(.+?)="(.+?)";/);
      if (match && match.length === 3) {
        const symbol = match[1];
        const fields = match[2].split('~');

        if (fields.length >= 40) {
          data.push({
            symbol,
            name: fields[1],
            code: fields[2],
            price: fields[3],
            yesterdayClose: fields[4],
            open: fields[5],
            change: fields[31],
            changePercent: fields[32],
            high: fields[33],
            low: fields[34],
            volume: fields[36],
            turnover: fields[37],
            turnoverRate: fields[38],
            pe: fields[39],
            amplitude: fields[43],
            marketCap: fields[45],
          });
        }
      }
    });

    return data.length > 0 ? data : this.getMockTencentData();
  }

  private async fetchTencentETFDetail(code: string): Promise<ETFDetailResponse> {
    const symbol = this.getTencentSymbol(code);
    
    try {
      const url = `/api/tencent/q=${symbol}`;
      const response = await fetch(url, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
        },
      });
      const arrayBuffer = await response.arrayBuffer();
      const text = new TextDecoder('GBK').decode(arrayBuffer);
      const data = this.parseTencentData(text);

      if (data.length > 0) {
        const item = data[0];
        return {
          basic: {
            code,
            name: item.name,
            fullName: item.name,
            fundCompany: '腾讯数据',
            establishDate: '',
            latestReportDate: '',
            scale: parseFloat(item.marketCap) || 0,
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
            pe: parseFloat(item.pe) || 0,
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

    return this.getMockTencentDetail(code);
  }

  private getTencentSymbol(code: string): string {
    if (code.startsWith('5')) {
      return `sh${code}`;
    }
    return `sz${code}`;
  }

  private mapCategory(code: string): ETFCategory {
    const broadCodes = ['510050', '510300', '510500', '159919', '159920', '510180', '510900', '510330', '510650', '510680', '510760', '159901', '159902', '159949', '159967', '159985'];
    const industryCodes = ['512880', '512100', '512010', '512200', '512800', '512690', '512500', '512400', '512300', '512660', '512670', '512680', '512760', '512850', '512890', '512930', '512960', '512980'];
    const crossBorderCodes = ['513100', '513500', '513050', '513300', '513130', '513030', '159601', '159603', '513010', '513600', '513700', '513800', '513900', '159621', '159631', '159633'];
    const bondCodes = ['511260', '511010', '511210', '511220', '511230', '511360', '511500', '511660', '511700', '511800', '511880', '511990'];

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

  private getMockTencentData(): TencentETFData[] {
    return [
      { symbol: 'sh510050', name: '上证50ETF', code: '510050', price: '2.653', yesterdayClose: '2.641', open: '2.645', change: '0.012', changePercent: '0.45', high: '2.660', low: '2.638', volume: '1256800', turnover: '333333', turnoverRate: '0.59', pe: '11.56', amplitude: '0.83', marketCap: '56789000' },
      { symbol: 'sh510300', name: '沪深300ETF', code: '510300', price: '4.125', yesterdayClose: '4.133', open: '4.130', change: '-0.008', changePercent: '-0.19', high: '4.140', low: '4.115', volume: '985400', turnover: '406543', turnoverRate: '0.45', pe: '13.21', amplitude: '0.60', marketCap: '89012000' },
      { symbol: 'sh510500', name: '中证500ETF', code: '510500', price: '6.856', yesterdayClose: '6.833', open: '6.840', change: '0.023', changePercent: '0.34', high: '6.870', low: '6.840', volume: '678900', turnover: '465432', turnoverRate: '0.68', pe: '18.90', amplitude: '0.44', marketCap: '45678000' },
      { symbol: 'sz159919', name: '沪深300ETF', code: '159919', price: '4.118', yesterdayClose: '4.128', open: '4.125', change: '-0.010', changePercent: '-0.24', high: '4.135', low: '4.110', volume: '876500', turnover: '360679', turnoverRate: '1.07', pe: '13.18', amplitude: '0.61', marketCap: '34567000' },
      { symbol: 'sh512880', name: '证券ETF', code: '512880', price: '1.123', yesterdayClose: '1.118', open: '1.120', change: '0.005', changePercent: '0.45', high: '1.128', low: '1.115', volume: '1567800', turnover: '175988', turnoverRate: '0.75', pe: '15.67', amplitude: '1.16', marketCap: '23456000' },
      { symbol: 'sh513100', name: '纳指ETF', code: '513100', price: '1.890', yesterdayClose: '1.865', open: '1.870', change: '0.025', changePercent: '1.34', high: '1.895', low: '1.868', volume: '456700', turnover: '863163', turnoverRate: '0.70', pe: '28.90', amplitude: '1.45', marketCap: '12345000' },
      { symbol: 'sz159915', name: '创业板ETF', code: '159915', price: '2.345', yesterdayClose: '2.330', open: '2.335', change: '0.015', changePercent: '0.64', high: '2.355', low: '2.330', volume: '789000', turnover: '184923', turnoverRate: '0.93', pe: '35.67', amplitude: '1.07', marketCap: '19876000' },
      { symbol: 'sh510880', name: '红利ETF', code: '510880', price: '2.890', yesterdayClose: '2.882', open: '2.885', change: '0.008', changePercent: '0.28', high: '2.895', low: '2.878', volume: '345600', turnover: '998784', turnoverRate: '0.34', pe: '8.90', amplitude: '0.59', marketCap: '7890000' },
      { symbol: 'sz159934', name: '黄金ETF', code: '159934', price: '4.567', yesterdayClose: '4.535', open: '4.540', change: '0.032', changePercent: '0.70', high: '4.575', low: '4.538', volume: '234500', turnover: '1070346', turnoverRate: '1.86', pe: '0', amplitude: '0.81', marketCap: '5678000' },
      { symbol: 'sh512690', name: '酒ETF', code: '512690', price: '1.234', yesterdayClose: '1.240', open: '1.238', change: '-0.006', changePercent: '-0.48', high: '1.242', low: '1.230', volume: '567800', turnover: '699665', turnoverRate: '0.42', pe: '32.34', amplitude: '0.97', marketCap: '16789000' },
      { symbol: 'sh512500', name: '银行ETF', code: '512500', price: '1.023', yesterdayClose: '1.020', open: '1.021', change: '0.003', changePercent: '0.30', high: '1.026', low: '1.018', volume: '890100', turnover: '910572', turnoverRate: '0.75', pe: '6.78', amplitude: '0.78', marketCap: '12345000' },
      { symbol: 'sh512400', name: '券商ETF', code: '512400', price: '1.156', yesterdayClose: '1.148', open: '1.150', change: '0.008', changePercent: '0.70', high: '1.160', low: '1.145', volume: '765400', turnover: '884892', turnoverRate: '0.91', pe: '14.56', amplitude: '1.31', marketCap: '9876000' },
      { symbol: 'sh512300', name: '医药ETF', code: '512300', price: '0.987', yesterdayClose: '0.989', open: '0.990', change: '-0.002', changePercent: '-0.20', high: '0.992', low: '0.985', volume: '654300', turnover: '646894', turnoverRate: '0.74', pe: '25.67', amplitude: '0.71', marketCap: '8765000' },
      { symbol: 'sz159995', name: '芯片ETF', code: '159995', price: '1.567', yesterdayClose: '1.542', open: '1.550', change: '0.025', changePercent: '1.62', high: '1.575', low: '1.545', volume: '987600', turnover: '1547569', turnoverRate: '0.99', pe: '45.67', amplitude: '1.94', marketCap: '15678000' },
      { symbol: 'sz159949', name: '创业板50ETF', code: '159949', price: '1.890', yesterdayClose: '1.878', open: '1.882', change: '0.012', changePercent: '0.64', high: '1.898', low: '1.875', volume: '567800', turnover: '1073142', turnoverRate: '1.40', pe: '38.90', amplitude: '1.22', marketCap: '7654000' },
      { symbol: 'sh513500', name: '日经ETF', code: '513500', price: '1.234', yesterdayClose: '1.237', open: '1.235', change: '-0.003', changePercent: '-0.24', high: '1.240', low: '1.230', volume: '123400', turnover: '152276', turnoverRate: '0.65', pe: '16.78', amplitude: '0.81', marketCap: '2345000' },
      { symbol: 'sh513050', name: '中概互联ETF', code: '513050', price: '1.678', yesterdayClose: '1.660', open: '1.665', change: '0.018', changePercent: '1.08', high: '1.685', low: '1.662', volume: '456700', turnover: '766343', turnoverRate: '0.33', pe: '32.34', amplitude: '1.38', marketCap: '23456000' },
      { symbol: 'sz159601', name: '纳斯达克ETF', code: '159601', price: '2.345', yesterdayClose: '2.300', open: '2.310', change: '0.045', changePercent: '1.95', high: '2.350', low: '2.305', volume: '345600', turnover: '810532', turnoverRate: '1.81', pe: '29.87', amplitude: '1.95', marketCap: '4567000' },
      { symbol: 'sh513300', name: '标普500ETF', code: '513300', price: '2.678', yesterdayClose: '2.646', open: '2.650', change: '0.032', changePercent: '1.21', high: '2.685', low: '2.648', volume: '234500', turnover: '628292', turnoverRate: '1.82', pe: '25.67', amplitude: '1.40', marketCap: '3456000' },
      { symbol: 'sh510900', name: 'H股ETF', code: '510900', price: '1.567', yesterdayClose: '1.552', open: '1.555', change: '0.015', changePercent: '0.97', high: '1.572', low: '1.550', volume: '345600', turnover: '540595', turnoverRate: '0.81', pe: '10.90', amplitude: '1.42', marketCap: '6789000' },
      { symbol: 'sh512100', name: '有色金属ETF', code: '512100', price: '1.345', yesterdayClose: '1.317', open: '1.320', change: '0.028', changePercent: '2.13', high: '1.355', low: '1.315', volume: '678900', turnover: '912152', turnoverRate: '0.64', pe: '22.34', amplitude: '3.04', marketCap: '8901000' },
      { symbol: 'sh512010', name: '券商ETF', code: '512010', price: '1.234', yesterdayClose: '1.225', open: '1.228', change: '0.009', changePercent: '0.74', high: '1.240', low: '1.222', volume: '456700', turnover: '563568', turnoverRate: '0.70', pe: '15.23', amplitude: '1.46', marketCap: '7890000' },
      { symbol: 'sz159967', name: '创成长ETF', code: '159967', price: '2.456', yesterdayClose: '2.421', open: '2.425', change: '0.035', changePercent: '1.45', high: '2.465', low: '2.418', volume: '345600', turnover: '840794', turnoverRate: '1.85', pe: '36.78', amplitude: '1.94', marketCap: '4567000' },
      { symbol: 'sh513130', name: '纳指ETF', code: '513130', price: '2.123', yesterdayClose: '2.081', open: '2.085', change: '0.042', changePercent: '2.02', high: '2.130', low: '2.082', volume: '123400', turnover: '261816', turnoverRate: '1.10', pe: '28.76', amplitude: '2.30', marketCap: '2345000' },
      { symbol: 'sh512200', name: '房地产ETF', code: '512200', price: '0.876', yesterdayClose: '0.880', open: '0.878', change: '-0.004', changePercent: '-0.45', high: '0.882', low: '0.872', volume: '345600', turnover: '303746', turnoverRate: '0.54', pe: '12.34', amplitude: '1.14', marketCap: '5678000' },
      { symbol: 'sh510330', name: '沪深300ETF', code: '510330', price: '1.567', yesterdayClose: '1.560', open: '1.562', change: '0.007', changePercent: '0.45', high: '1.570', low: '1.558', volume: '234500', turnover: '367890', turnoverRate: '0.56', pe: '13.15', amplitude: '0.77', marketCap: '42345000' },
      { symbol: 'sz159996', name: '家电ETF', code: '159996', price: '1.234', yesterdayClose: '1.230', open: '1.232', change: '0.004', changePercent: '0.33', high: '1.238', low: '1.228', volume: '123400', turnover: '152340', turnoverRate: '0.45', pe: '14.56', amplitude: '0.81', marketCap: '3456000' },
      { symbol: 'sh515030', name: '科技ETF', code: '515030', price: '1.678', yesterdayClose: '1.660', open: '1.665', change: '0.018', changePercent: '1.08', high: '1.685', low: '1.658', volume: '345600', turnover: '580925', turnoverRate: '0.78', pe: '32.34', amplitude: '1.62', marketCap: '71234000' },
      { symbol: 'sh515000', name: '科技ETF', code: '515000', price: '1.234', yesterdayClose: '1.220', open: '1.225', change: '0.014', changePercent: '1.15', high: '1.240', low: '1.218', volume: '234500', turnover: '289012', turnoverRate: '0.56', pe: '35.67', amplitude: '1.80', marketCap: '51234000' },
      { symbol: 'sz159992', name: '创新药ETF', code: '159992', price: '1.567', yesterdayClose: '1.552', open: '1.555', change: '0.015', changePercent: '0.97', high: '1.572', low: '1.550', volume: '456700', turnover: '715379', turnoverRate: '1.35', pe: '28.90', amplitude: '1.41', marketCap: '53456000' },
      { symbol: 'sh515220', name: '煤炭ETF', code: '515220', price: '1.890', yesterdayClose: '1.870', open: '1.875', change: '0.020', changePercent: '1.07', high: '1.898', low: '1.868', volume: '123400', turnover: '233226', turnoverRate: '0.52', pe: '8.90', amplitude: '1.60', marketCap: '4567000' },
      { symbol: 'sh515880', name: '军工ETF', code: '515880', price: '1.345', yesterdayClose: '1.330', open: '1.335', change: '0.015', changePercent: '1.13', high: '1.350', low: '1.328', volume: '567800', turnover: '763691', turnoverRate: '0.62', pe: '22.34', amplitude: '1.65', marketCap: '123450000' },
    ];
  }

  private getMockTencentDetail(code: string): ETFDetailResponse {
    return {
      basic: {
        code,
        name: 'ETF名称',
        fullName: 'ETF全称',
        fundCompany: '腾讯数据',
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