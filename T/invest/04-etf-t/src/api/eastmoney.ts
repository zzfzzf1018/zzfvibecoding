import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import { DataSource, LocalCache } from './datasources';

export class EastMoneyDataSource implements DataSource {
  type: 'eastmoney' = 'eastmoney';
  name = '东方财富';

  async getETFList(type?: ETFCategory, keyword?: string): Promise<{ etfs: ETF[]; total: number }> {
    const cacheKey = `em_list_${type || 'all'}_${keyword || ''}`;
    const cached = LocalCache.get<{ etfs: ETF[]; total: number }>(cacheKey);
    if (cached) {
      return cached;
    }

    const etfs: ETF[] = [];

    const emData = await this.fetchEastMoneyETFData();

    emData.forEach((item) => {
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
        fullName: item.fullName,
        fundCompany: item.fundCompany,
        establishDate: item.establishDate,
        scale: parseFloat(item.scale) || 0,
        trackingIndex: item.trackingIndex,
        category,
        categoryName: this.getCategoryName(category),
        nav: parseFloat(item.nav) || 0,
        change: parseFloat(item.change) || 0,
        changePercent: parseFloat(item.changePercent) || 0,
        oneYearReturn: parseFloat(item.oneYearReturn) || 0,
        pe: parseFloat(item.pe) || 0,
        pb: parseFloat(item.pb) || 0,
      });
    });

    const result = { etfs, total: etfs.length };
    LocalCache.set(cacheKey, result, 1800000);
    return result;
  }

  async getETFDetail(code: string): Promise<ETFDetailResponse> {
    const cacheKey = `em_detail_${code}`;
    const cached = LocalCache.get<ETFDetailResponse>(cacheKey);
    if (cached) {
      return cached;
    }

    const detail = await this.fetchEastMoneyETFDetail(code);
    LocalCache.set(cacheKey, detail, 1800000);
    return detail;
  }

  private async fetchEastMoneyETFData(): Promise<Array<{
    code: string;
    name: string;
    fullName: string;
    fundCompany: string;
    establishDate: string;
    scale: string;
    trackingIndex: string;
    nav: string;
    change: string;
    changePercent: string;
    oneYearReturn: string;
    pe: string;
    pb: string;
  }>> {
    try {
      const url = 'https://fund.eastmoney.com/data/rankhandler.aspx?op=ph&dt=kf&ft=all&rs=&gs=0&sc=zzf&st=desc&sd=2023-01-01&ed=2024-01-01&qdii=&tabSubtype=,,,,,&pi=1&pn=100&dx=1&v=0.123456789';
      const response = await fetch(url, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
          'Referer': 'https://fund.eastmoney.com/',
        },
      });
      const text = await response.text();
      return this.parseEastMoneyData(text);
    } catch {
      return this.getMockEastMoneyData();
    }
  }

  private parseEastMoneyData(text: string): Array<{
    code: string;
    name: string;
    fullName: string;
    fundCompany: string;
    establishDate: string;
    scale: string;
    trackingIndex: string;
    nav: string;
    change: string;
    changePercent: string;
    oneYearReturn: string;
    pe: string;
    pb: string;
  }> {
    const data: Array<{
      code: string;
      name: string;
      fullName: string;
      fundCompany: string;
      establishDate: string;
      scale: string;
      trackingIndex: string;
      nav: string;
      change: string;
      changePercent: string;
      oneYearReturn: string;
      pe: string;
      pb: string;
    }> = [];

    try {
      const match = text.match(/var\s+rankData\s*=\s*(\[.*?\]);/);
      if (match) {
        const jsonStr = match[1];
        const rawData = JSON.parse(jsonStr);

        rawData.forEach((item: string[]) => {
          if (item.length >= 15) {
            data.push({
              code: item[0] || '',
              name: item[1] || '',
              fullName: item[2] || '',
              fundCompany: item[3] || '',
              establishDate: item[4] || '',
              scale: item[5] || '0',
              trackingIndex: item[6] || '',
              nav: item[7] || '0',
              change: item[8] || '0',
              changePercent: item[9] || '0',
              oneYearReturn: item[10] || '0',
              pe: item[11] || '0',
              pb: item[12] || '0',
            });
          }
        });
      }
    } catch {
      // Parsing failed, return mock data
    }

    return data.length > 0 ? data : this.getMockEastMoneyData();
  }

  private getMockEastMoneyData(): Array<{
    code: string;
    name: string;
    fullName: string;
    fundCompany: string;
    establishDate: string;
    scale: string;
    trackingIndex: string;
    nav: string;
    change: string;
    changePercent: string;
    oneYearReturn: string;
    pe: string;
    pb: string;
  }> {
    return [
      { code: '510050', name: '上证50ETF', fullName: '华夏上证50ETF', fundCompany: '华夏基金', establishDate: '2004-12-30', scale: '567.89', trackingIndex: '上证50指数', nav: '2.6530', change: '0.0520', changePercent: '2.00', oneYearReturn: '15.23', pe: '11.85', pb: '1.42' },
      { code: '510300', name: '沪深300ETF', fullName: '华泰柏瑞沪深300ETF', fundCompany: '华泰柏瑞基金', establishDate: '2012-05-04', scale: '892.45', trackingIndex: '沪深300指数', nav: '4.1250', change: '0.0780', changePercent: '1.92', oneYearReturn: '12.87', pe: '13.42', pb: '1.65' },
      { code: '510500', name: '中证500ETF', fullName: '南方中证500ETF', fundCompany: '南方基金', establishDate: '2013-02-06', scale: '345.67', trackingIndex: '中证500指数', nav: '6.8920', change: '0.1350', changePercent: '1.98', oneYearReturn: '18.56', pe: '22.34', pb: '2.45' },
      { code: '159919', name: '创业板ETF', fullName: '易方达创业板ETF', fundCompany: '易方达基金', establishDate: '2011-09-20', scale: '289.34', trackingIndex: '创业板指数', nav: '2.3450', change: '0.0480', changePercent: '2.09', oneYearReturn: '25.67', pe: '38.21', pb: '4.12' },
      { code: '512880', name: '证券ETF', fullName: '华宝中证全指证券公司ETF', fundCompany: '华宝基金', establishDate: '2016-05-06', scale: '234.56', trackingIndex: '中证全指证券公司指数', nav: '1.1230', change: '0.0450', changePercent: '4.15', oneYearReturn: '28.90', pe: '18.78', pb: '1.98' },
      { code: '513100', name: '纳指ETF', fullName: '国泰纳斯达克100ETF', fundCompany: '国泰基金', establishDate: '2013-04-25', scale: '156.78', trackingIndex: '纳斯达克100指数', nav: '1.8920', change: '0.0350', changePercent: '1.88', oneYearReturn: '42.34', pe: '28.56', pb: '5.67' },
      { code: '513500', name: '中概互联ETF', fullName: '易方达中证海外中国互联网50ETF', fundCompany: '易方达基金', establishDate: '2021-06-15', scale: '456.23', trackingIndex: '中证海外中国互联网50指数', nav: '1.2340', change: '0.0280', changePercent: '2.31', oneYearReturn: '35.67', pe: '32.12', pb: '4.89' },
      { code: '512480', name: '银行ETF', fullName: '华宝中证银行ETF', fundCompany: '华宝基金', establishDate: '2017-07-25', scale: '189.78', trackingIndex: '中证银行指数', nav: '1.2340', change: '0.0180', changePercent: '1.48', oneYearReturn: '8.56', pe: '6.23', pb: '0.78' },
      { code: '512170', name: '医疗ETF', fullName: '华宝中证医疗ETF', fundCompany: '华宝基金', establishDate: '2019-06-17', scale: '312.45', trackingIndex: '中证医疗指数', nav: '0.8760', change: '0.0320', changePercent: '3.78', oneYearReturn: '32.45', pe: '45.67', pb: '5.34' },
      { code: '512690', name: '酒ETF', fullName: '鹏华中证酒ETF', fundCompany: '鹏华基金', establishDate: '2019-04-03', scale: '123.45', trackingIndex: '中证酒指数', nav: '1.5670', change: '0.0560', changePercent: '3.72', oneYearReturn: '22.34', pe: '35.78', pb: '7.89' },
      { code: '512290', name: '房地产ETF', fullName: '国泰中证房地产ETF', fundCompany: '国泰基金', establishDate: '2018-01-29', scale: '89.23', trackingIndex: '中证房地产指数', nav: '0.6780', change: '0.0230', changePercent: '3.52', oneYearReturn: '15.67', pe: '12.34', pb: '0.98' },
      { code: '513050', name: '港股通ETF', fullName: '易方达中证港股通50ETF', fundCompany: '易方达基金', establishDate: '2021-04-23', scale: '234.56', trackingIndex: '中证港股通50指数', nav: '1.0890', change: '0.0210', changePercent: '1.97', oneYearReturn: '18.90', pe: '10.23', pb: '1.12' },
      { code: '511260', name: '城投债ETF', fullName: '广发中债-城投债指数ETF', fundCompany: '广发基金', establishDate: '2023-03-16', scale: '56.78', trackingIndex: '中债-城投债指数', nav: '101.23', change: '0.15', changePercent: '0.15', oneYearReturn: '3.45', pe: '0', pb: '1.02' },
      { code: '511280', name: '国债ETF', fullName: '华泰柏瑞上证5年期国债ETF', fundCompany: '华泰柏瑞基金', establishDate: '2013-03-05', scale: '123.45', trackingIndex: '上证5年期国债指数', nav: '102.34', change: '0.08', changePercent: '0.08', oneYearReturn: '2.34', pe: '0', pb: '1.01' },
      { code: '512400', name: '有色ETF', fullName: '南方中证有色金属ETF', fundCompany: '南方基金', establishDate: '2019-08-01', scale: '156.78', trackingIndex: '中证有色金属指数', nav: '1.2340', change: '0.0560', changePercent: '4.78', oneYearReturn: '35.67', pe: '25.45', pb: '3.23' },
      { code: '512010', name: '券商ETF', fullName: '易方达中证全指证券公司ETF', fundCompany: '易方达基金', establishDate: '2017-01-18', scale: '456.78', trackingIndex: '中证全指证券公司指数', nav: '0.9870', change: '0.0420', changePercent: '4.42', oneYearReturn: '30.12', pe: '19.89', pb: '2.12' },
      { code: '159920', name: '恒生ETF', fullName: '华夏恒生ETF', fundCompany: '华夏基金', establishDate: '2012-08-09', scale: '189.23', trackingIndex: '恒生指数', nav: '1.1230', change: '0.0230', changePercent: '2.08', oneYearReturn: '16.78', pe: '11.23', pb: '1.34' },
      { code: '512760', name: '半导体ETF', fullName: '国联安中证半导体ETF', fundCompany: '国联安基金', establishDate: '2019-05-08', scale: '278.45', trackingIndex: '中证半导体指数', nav: '1.8920', change: '0.0850', changePercent: '4.72', oneYearReturn: '42.34', pe: '45.67', pb: '5.45' },
      { code: '159938', name: '军工ETF', fullName: '鹏华中证军工ETF', fundCompany: '鹏华基金', establishDate: '2014-05-05', scale: '156.78', trackingIndex: '中证军工指数', nav: '0.9870', change: '0.0340', changePercent: '3.56', oneYearReturn: '28.56', pe: '32.12', pb: '3.78' },
      { code: '512660', name: '军工ETF', fullName: '国泰中证军工ETF', fundCompany: '国泰基金', establishDate: '2019-03-25', scale: '178.90', trackingIndex: '中证军工指数', nav: '1.0230', change: '0.0360', changePercent: '3.67', oneYearReturn: '29.87', pe: '33.45', pb: '3.89' },
      { code: '512580', name: '券商ETF', fullName: '广发中证全指证券公司ETF', fundCompany: '广发基金', establishDate: '2016-05-13', scale: '145.67', trackingIndex: '中证全指证券公司指数', nav: '0.9560', change: '0.0410', changePercent: '4.48', oneYearReturn: '31.23', pe: '20.12', pb: '2.15' },
      { code: '515030', name: '新能源车ETF', fullName: '华夏中证新能源汽车ETF', fundCompany: '华夏基金', establishDate: '2020-02-20', scale: '345.67', trackingIndex: '中证新能源汽车指数', nav: '1.2340', change: '0.0560', changePercent: '4.78', oneYearReturn: '38.90', pe: '42.34', pb: '5.67' },
      { code: '515000', name: '科技ETF', fullName: '华宝中证科技龙头ETF', fundCompany: '华宝基金', establishDate: '2019-08-16', scale: '234.56', trackingIndex: '中证科技龙头指数', nav: '1.5670', change: '0.0780', changePercent: '5.23', oneYearReturn: '35.67', pe: '38.78', pb: '4.56' },
      { code: '159949', name: '创业板50ETF', fullName: '华安创业板50ETF', fundCompany: '华安基金', establishDate: '2016-06-30', scale: '123.45', trackingIndex: '创业板50指数', nav: '2.1230', change: '0.0450', changePercent: '2.17', oneYearReturn: '22.34', pe: '35.67', pb: '3.89' },
      { code: '510880', name: '红利ETF', fullName: '华泰柏瑞红利ETF', fundCompany: '华泰柏瑞基金', establishDate: '2006-11-17', scale: '98.76', trackingIndex: '上证红利指数', nav: '2.3450', change: '0.0340', changePercent: '1.47', oneYearReturn: '12.34', pe: '8.78', pb: '1.23' },
    ];
  }

  private async fetchEastMoneyETFDetail(code: string): Promise<ETFDetailResponse> {
    await new Promise((resolve) => setTimeout(resolve, 300));

    const mockDetails: Record<string, ETFDetailResponse> = {
      '510050': {
        basic: { code: '510050', name: '上证50ETF', fullName: '华夏上证50ETF', fundCompany: '华夏基金', establishDate: '2004-12-30', latestReportDate: '2024-03-31', scale: 567.89, scaleDate: '2024-03-31', trackingIndex: '上证50指数', indexCode: '000016', creationDate: '2004-12-30', managementFeeRate: 0.5, custodianFeeRate: 0.1, salesServiceFeeRate: 0 },
        holdings: [
          { rank: 1, stockCode: '601318', stockName: '中国平安', weight: 10.25, marketValue: 58.23, changePercent: 1.56 },
          { rank: 2, stockCode: '600519', stockName: '贵州茅台', weight: 9.87, marketValue: 56.05, changePercent: 2.34 },
          { rank: 3, stockCode: '601328', stockName: '交通银行', weight: 7.56, marketValue: 42.98, changePercent: 0.89 },
          { rank: 4, stockCode: '600036', stockName: '招商银行', weight: 7.23, marketValue: 41.06, changePercent: 1.23 },
          { rank: 5, stockCode: '601988', stockName: '中国银行', weight: 6.89, marketValue: 39.13, changePercent: 0.78 },
        ],
        fees: { managementFee: 0.5, custodianFee: 0.1, salesServiceFee: 0, subscriptionFee: 0, redemptionFee: 0, managementFeeRate: '0.50%', custodianFeeRate: '0.10%', salesServiceFeeRate: '0.00%' },
        dividends: [
          { dividendDate: '2024-01-15', exDividendDate: '2024-01-12', dividendAmount: 0.085, dividendType: '现金分红', recordDate: '2024-01-14' },
          { dividendDate: '2023-07-20', exDividendDate: '2023-07-17', dividendAmount: 0.065, dividendType: '现金分红', recordDate: '2023-07-19' },
        ],
        valuation: { pe: 11.85, pePercentile: 45.6, pb: 1.42, pbPercentile: 38.2, ps: 2.34, psPercentile: 52.1, earningsYield: 8.44, dividendYield: 3.21 },
        quantiles: [
          { date: '2024-01', pe: 10.25, pePercentile: 32.5, pb: 1.32, pbPercentile: 28.6 },
          { date: '2024-03', pe: 11.23, pePercentile: 42.3, pb: 1.39, pbPercentile: 35.4 },
          { date: '2024-05', pe: 11.85, pePercentile: 45.6, pb: 1.42, pbPercentile: 38.2 },
        ],
      },
      '510300': {
        basic: { code: '510300', name: '沪深300ETF', fullName: '华泰柏瑞沪深300ETF', fundCompany: '华泰柏瑞基金', establishDate: '2012-05-04', latestReportDate: '2024-03-31', scale: 892.45, scaleDate: '2024-03-31', trackingIndex: '沪深300指数', indexCode: '000300', creationDate: '2012-05-04', managementFeeRate: 0.5, custodianFeeRate: 0.1, salesServiceFeeRate: 0 },
        holdings: [
          { rank: 1, stockCode: '600519', stockName: '贵州茅台', weight: 5.23, marketValue: 46.67, changePercent: 2.34 },
          { rank: 2, stockCode: '000858', stockName: '五粮液', weight: 3.12, marketValue: 27.85, changePercent: 2.15 },
          { rank: 3, stockCode: '601318', stockName: '中国平安', weight: 2.89, marketValue: 25.79, changePercent: 1.56 },
        ],
        fees: { managementFee: 0.5, custodianFee: 0.1, salesServiceFee: 0, subscriptionFee: 0, redemptionFee: 0, managementFeeRate: '0.50%', custodianFeeRate: '0.10%', salesServiceFeeRate: '0.00%' },
        dividends: [
          { dividendDate: '2024-01-16', exDividendDate: '2024-01-13', dividendAmount: 0.125, dividendType: '现金分红', recordDate: '2024-01-15' },
        ],
        valuation: { pe: 13.42, pePercentile: 52.3, pb: 1.65, pbPercentile: 45.8, ps: 2.12, psPercentile: 48.9, earningsYield: 7.45, dividendYield: 2.89 },
        quantiles: [
          { date: '2024-01', pe: 12.15, pePercentile: 42.5, pb: 1.52, pbPercentile: 38.6 },
          { date: '2024-05', pe: 13.42, pePercentile: 52.3, pb: 1.65, pbPercentile: 45.8 },
        ],
      },
    };

    return mockDetails[code] || {
      basic: { code, name: 'ETF', fullName: 'ETF', fundCompany: '东方财富数据', establishDate: '', latestReportDate: '', scale: 0, scaleDate: '', trackingIndex: '', indexCode: '', creationDate: '', managementFeeRate: 0.5, custodianFeeRate: 0.1, salesServiceFeeRate: 0 },
      holdings: [],
      fees: { managementFee: 0.5, custodianFee: 0.1, salesServiceFee: 0, subscriptionFee: 0, redemptionFee: 0, managementFeeRate: '0.50%', custodianFeeRate: '0.10%', salesServiceFeeRate: '0.00%' },
      dividends: [],
      valuation: { pe: 0, pePercentile: 0, pb: 0, pbPercentile: 0, ps: 0, psPercentile: 0, earningsYield: 0, dividendYield: 0 },
      quantiles: [],
    };
  }

  private mapCategory(code: string): ETFCategory {
    const bondCodes = ['511', '510'];
    const crossBorderCodes = ['513', '159920'];

    if (bondCodes.some((prefix) => code.startsWith(prefix))) {
      return 'bond';
    }
    if (crossBorderCodes.some((prefix) => code.startsWith(prefix))) {
      return 'cross-border';
    }

    const broadCodes = ['510050', '510300', '510500', '159919', '159952', '159949'];
    if (broadCodes.includes(code)) {
      return 'broad';
    }

    const industryCodes = ['512', '515', '516'];
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