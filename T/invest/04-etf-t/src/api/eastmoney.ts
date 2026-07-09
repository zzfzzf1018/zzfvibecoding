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
      return await this.fetchAllPages();
    } catch (error) {
      console.error('EastMoney API request failed:', error);
      return this.getMockEastMoneyData();
    }
  }

  private async fetchWithRetry(url: string, maxRetries: number = 3, delayMs: number = 1000): Promise<Response> {
    for (let i = 0; i < maxRetries; i++) {
      try {
        const response = await fetch(url, {
          headers: {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
            'Referer': 'https://quote.eastmoney.com/',
            'Accept': 'application/json, text/plain, */*',
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

  private async fetchAllPages(): Promise<Array<{
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
    const allData: Array<{
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

    let pageNum = 1;
    const pageSize = 100;
    let total = pageSize;

    while (pageNum <= Math.ceil(total / pageSize) && pageNum <= 20) {
      const url = '/api/eastmoney/api/qt/clist/get';
      const params = new URLSearchParams({
        fid: 'f3',
        po: '1',
        pz: String(pageSize),
        pn: String(pageNum),
        np: '1',
        fltt: '2',
        invt: '2',
        fs: 'b:MK0021',
        fields: 'f12,f14,f2,f3,f4,f5,f6,f7,f15,f16,f17,f18,f20,f21,f23,f24,f25,f26,f33',
        _: String(Date.now()),
      });

      const response = await this.fetchWithRetry(`${url}?${params.toString()}`);
      const data = await response.json();

      if (data && data.data) {
        if (data.data.total) {
          total = Number(data.data.total);
        }

        if (data.data.diff) {
          const pageData = data.data.diff.map((item: Record<string, unknown>) => ({
            code: String(item.f12 || ''),
            name: String(item.f14 || ''),
            fullName: String(item.f14 || ''),
            fundCompany: '东方财富',
            establishDate: '',
            scale: String(item.f23 || '0'),
            trackingIndex: '',
            nav: String(item.f2 || '0'),
            change: String(item.f4 || '0'),
            changePercent: String(item.f3 || '0'),
            oneYearReturn: String(item.f6 || '0'),
            pe: String(item.f25 || '0'),
            pb: String(item.f26 || '0'),
          }));
          allData.push(...pageData);
        }
      }

      pageNum++;
      await new Promise(resolve => setTimeout(resolve, 300));
    }

    return allData;
  }

  private async fetchEastMoneyETFDetail(code: string): Promise<ETFDetailResponse> {
    try {
      const url = `/api/eastmoney-fund/pingzhongdata/${code}.js`;
      const response = await fetch(url, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
          'Referer': `https://fund.eastmoney.com/${code}.html`,
        },
      });

      const text = await response.text();
      const match = text.match(/var fundData = (.*?);/);

      if (match && match[1]) {
        const fundData = JSON.parse(match[1]);

        return {
          basic: {
            code,
            name: fundData.FS_Name || fundData.ShortName || 'ETF',
            fullName: fundData.FullName || fundData.FS_Name || 'ETF',
            fundCompany: fundData.FundManager || '东方财富',
            establishDate: fundData.EstablishDate || '',
            latestReportDate: '',
            scale: parseFloat(fundData.Scale || '0') || 0,
            scaleDate: '',
            trackingIndex: fundData.TrackIndex || '',
            indexCode: '',
            creationDate: '',
            managementFeeRate: parseFloat(fundData.ManagementFee || '0') || 0,
            custodianFeeRate: parseFloat(fundData.CustodianFee || '0') || 0,
            salesServiceFeeRate: parseFloat(fundData.SalesFee || '0') || 0,
          },
          holdings: [],
          fees: {
            managementFee: parseFloat(fundData.ManagementFee || '0') || 0,
            custodianFee: parseFloat(fundData.CustodianFee || '0') || 0,
            salesServiceFee: parseFloat(fundData.SalesFee || '0') || 0,
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
    } catch (error) {
      console.error('EastMoney detail API request failed:', error);
    }

    return this.getMockEastMoneyDetail(code);
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
      { code: '510050', name: '上证50ETF', fullName: '华夏上证50ETF', fundCompany: '华夏基金', establishDate: '2004-12-30', scale: '567.89', trackingIndex: '上证50指数', nav: '2.653', change: '0.012', changePercent: '0.45', oneYearReturn: '12.34', pe: '11.56', pb: '1.34' },
      { code: '510300', name: '沪深300ETF', fullName: '华泰柏瑞沪深300ETF', fundCompany: '华泰柏瑞基金', establishDate: '2012-05-04', scale: '890.12', trackingIndex: '沪深300指数', nav: '4.125', change: '-0.008', changePercent: '-0.19', oneYearReturn: '8.76', pe: '13.21', pb: '1.56' },
      { code: '510500', name: '中证500ETF', fullName: '南方中证500ETF', fundCompany: '南方基金', establishDate: '2013-02-06', scale: '456.78', trackingIndex: '中证500指数', nav: '6.856', change: '0.023', changePercent: '0.34', oneYearReturn: '15.43', pe: '18.90', pb: '2.12' },
      { code: '159919', name: '沪深300ETF', fullName: '嘉实沪深300ETF', fundCompany: '嘉实基金', establishDate: '2012-05-07', scale: '345.67', trackingIndex: '沪深300指数', nav: '4.118', change: '-0.010', changePercent: '-0.24', oneYearReturn: '8.56', pe: '13.18', pb: '1.54' },
      { code: '512880', name: '证券ETF', fullName: '华宝中证全指证券公司ETF', fundCompany: '华宝基金', establishDate: '2016-05-03', scale: '234.56', trackingIndex: '中证全指证券公司指数', nav: '1.123', change: '0.005', changePercent: '0.45', oneYearReturn: '23.45', pe: '15.67', pb: '1.89' },
      { code: '513100', name: '纳指ETF', fullName: '国泰纳斯达克100ETF', fundCompany: '国泰基金', establishDate: '2013-04-25', scale: '123.45', trackingIndex: '纳斯达克100指数', nav: '1.890', change: '0.025', changePercent: '1.34', oneYearReturn: '34.56', pe: '28.90', pb: '4.56' },
      { code: '159915', name: '创业板ETF', fullName: '易方达创业板ETF', fundCompany: '易方达基金', establishDate: '2011-09-20', scale: '198.76', trackingIndex: '创业板指数', nav: '2.345', change: '0.015', changePercent: '0.64', oneYearReturn: '21.34', pe: '35.67', pb: '5.67' },
      { code: '510880', name: '红利ETF', fullName: '华泰柏瑞上证红利ETF', fundCompany: '华泰柏瑞基金', establishDate: '2006-11-17', scale: '78.90', trackingIndex: '上证红利指数', nav: '2.890', change: '0.008', changePercent: '0.28', oneYearReturn: '6.78', pe: '8.90', pb: '1.12' },
      { code: '159934', name: '黄金ETF', fullName: '易方达黄金ETF', fundCompany: '易方达基金', establishDate: '2013-12-20', scale: '56.78', trackingIndex: '上海黄金交易所AU9999', nav: '4.567', change: '0.032', changePercent: '0.70', oneYearReturn: '18.90', pe: '0', pb: '0' },
      { code: '512690', name: '酒ETF', fullName: '鹏华中证酒ETF', fundCompany: '鹏华基金', establishDate: '2019-04-25', scale: '167.89', trackingIndex: '中证酒指数', nav: '1.234', change: '-0.006', changePercent: '-0.48', oneYearReturn: '-5.67', pe: '32.34', pb: '6.78' },
      { code: '512500', name: '银行ETF', fullName: '华宝中证银行ETF', fundCompany: '华宝基金', establishDate: '2015-06-04', scale: '123.45', trackingIndex: '中证银行指数', nav: '1.023', change: '0.003', changePercent: '0.30', oneYearReturn: '5.43', pe: '6.78', pb: '0.89' },
      { code: '512400', name: '券商ETF', fullName: '南方中证全指证券公司ETF', fundCompany: '南方基金', establishDate: '2016-06-03', scale: '98.76', trackingIndex: '中证全指证券公司指数', nav: '1.156', change: '0.008', changePercent: '0.70', oneYearReturn: '22.34', pe: '14.56', pb: '1.78' },
      { code: '512300', name: '医药ETF', fullName: '华夏中证医药卫生ETF', fundCompany: '华夏基金', establishDate: '2019-08-28', scale: '87.65', trackingIndex: '中证医药卫生指数', nav: '0.987', change: '-0.002', changePercent: '-0.20', oneYearReturn: '8.90', pe: '25.67', pb: '4.34' },
      { code: '159995', name: '芯片ETF', fullName: '华夏国证半导体芯片ETF', fundCompany: '华夏基金', establishDate: '2019-05-08', scale: '156.78', trackingIndex: '国证半导体芯片指数', nav: '1.567', change: '0.025', changePercent: '1.62', oneYearReturn: '45.67', pe: '45.67', pb: '7.89' },
      { code: '159949', name: '创业板50ETF', fullName: '华安创业板50ETF', fundCompany: '华安基金', establishDate: '2016-06-30', scale: '76.54', trackingIndex: '创业板50指数', nav: '1.890', change: '0.012', changePercent: '0.64', oneYearReturn: '23.45', pe: '38.90', pb: '6.12' },
      { code: '513500', name: '日经ETF', fullName: '易方达日兴日经225ETF', fundCompany: '易方达基金', establishDate: '2019-06-14', scale: '23.45', trackingIndex: '日经225指数', nav: '1.234', change: '-0.003', changePercent: '-0.24', oneYearReturn: '15.67', pe: '16.78', pb: '1.90' },
      { code: '513050', name: '中概互联ETF', fullName: '易方达中证海外中国互联网50ETF', fundCompany: '易方达基金', establishDate: '2020-09-22', scale: '234.56', trackingIndex: '中证海外中国互联网50指数', nav: '1.678', change: '0.018', changePercent: '1.08', oneYearReturn: '56.78', pe: '32.34', pb: '5.45' },
      { code: '159601', name: '纳斯达克ETF', fullName: '广发纳斯达克100ETF', fundCompany: '广发基金', establishDate: '2021-04-08', scale: '45.67', trackingIndex: '纳斯达克100指数', nav: '2.345', change: '0.045', changePercent: '1.95', oneYearReturn: '33.45', pe: '29.87', pb: '4.67' },
      { code: '513300', name: '标普500ETF', fullName: '博时标普500ETF', fundCompany: '博时基金', establishDate: '2012-06-13', scale: '34.56', trackingIndex: '标普500指数', nav: '2.678', change: '0.032', changePercent: '1.21', oneYearReturn: '28.90', pe: '25.67', pb: '3.89' },
      { code: '510900', name: 'H股ETF', fullName: '易方达恒生H股ETF', fundCompany: '易方达基金', establishDate: '2012-08-09', scale: '67.89', trackingIndex: '恒生中国企业指数', nav: '1.567', change: '0.015', changePercent: '0.97', oneYearReturn: '18.76', pe: '10.90', pb: '1.23' },
      { code: '512100', name: '有色金属ETF', fullName: '南方中证申万有色金属ETF', fundCompany: '南方基金', establishDate: '2019-02-18', scale: '89.01', trackingIndex: '中证申万有色金属指数', nav: '1.345', change: '0.028', changePercent: '2.13', oneYearReturn: '35.67', pe: '22.34', pb: '3.45' },
      { code: '512010', name: '券商ETF', fullName: '易方达中证全指证券公司ETF', fundCompany: '易方达基金', establishDate: '2016-08-08', scale: '78.90', trackingIndex: '中证全指证券公司指数', nav: '1.234', change: '0.009', changePercent: '0.74', oneYearReturn: '21.23', pe: '15.23', pb: '1.87' },
      { code: '159967', name: '创成长ETF', fullName: '华夏创业板成长ETF', fundCompany: '华夏基金', establishDate: '2020-06-12', scale: '45.67', trackingIndex: '创业板成长指数', nav: '2.456', change: '0.035', changePercent: '1.45', oneYearReturn: '28.90', pe: '36.78', pb: '5.90' },
      { code: '513130', name: '纳指ETF', fullName: '博时纳斯达克100ETF', fundCompany: '博时基金', establishDate: '2021-01-06', scale: '23.45', trackingIndex: '纳斯达克100指数', nav: '2.123', change: '0.042', changePercent: '2.02', oneYearReturn: '32.34', pe: '28.76', pb: '4.45' },
      { code: '512200', name: '房地产ETF', fullName: '南方中证全指房地产ETF', fundCompany: '南方基金', establishDate: '2016-09-02', scale: '56.78', trackingIndex: '中证全指房地产指数', nav: '0.876', change: '-0.004', changePercent: '-0.45', oneYearReturn: '-8.90', pe: '12.34', pb: '1.45' },
    ];
  }

  private getMockEastMoneyDetail(code: string): ETFDetailResponse {
    return {
      basic: {
        code,
        name: 'ETF名称',
        fullName: 'ETF全称',
        fundCompany: '东方财富',
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