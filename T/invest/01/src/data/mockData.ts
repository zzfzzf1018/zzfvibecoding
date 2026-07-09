import { Stock, FinancialData, MarketType, PeriodType, News, CalendarEvent, IndustryData, KLineData } from '../types';

export const aStocks: Stock[] = [
  { code: '600519', name: '贵州茅台', market: 'a股', industry: '白酒', price: 1685.00, change: 25.50, changePercent: 1.54 },
  { code: '000858', name: '五粮液', market: 'a股', industry: '白酒', price: 142.80, change: 1.20, changePercent: 0.85 },
  { code: '601318', name: '中国平安', market: 'a股', industry: '保险', price: 48.50, change: -0.80, changePercent: -1.62 },
  { code: '600036', name: '招商银行', market: 'a股', industry: '银行', price: 35.20, change: 0.50, changePercent: 1.44 },
  { code: '000001', name: '平安银行', market: 'a股', industry: '银行', price: 12.80, change: 0.15, changePercent: 1.18 },
  { code: '601899', name: '紫金矿业', market: 'a股', industry: '有色金属', price: 15.60, change: 0.30, changePercent: 1.96 },
  { code: '300750', name: '宁德时代', market: 'a股', industry: '新能源', price: 218.50, change: 5.80, changePercent: 2.73 },
  { code: '002594', name: '比亚迪', market: 'a股', industry: '新能源', price: 258.00, change: 8.20, changePercent: 3.27 },
];

export const hongKongStocks: Stock[] = [
  { code: '00001', name: '长和', market: '港股', industry: '综合', price: 58.20, change: 0.80, changePercent: 1.39 },
  { code: '00002', name: '中电控股', market: '港股', industry: '公用事业', price: 72.50, change: -0.30, changePercent: -0.41 },
  { code: '00003', name: '香港中华煤气', market: '港股', industry: '公用事业', price: 12.80, change: 0.10, changePercent: 0.79 },
  { code: '00005', name: '汇丰控股', market: '港股', industry: '银行', price: 68.50, change: 1.20, changePercent: 1.78 },
  { code: '00006', name: '香港电灯', market: '港股', industry: '公用事业', price: 55.20, change: 0.40, changePercent: 0.73 },
  { code: '00700', name: '腾讯控股', market: '港股', industry: '互联网', price: 428.00, change: 12.50, changePercent: 3.00 },
  { code: '00883', name: '中国海洋石油', market: '港股', industry: '石油', price: 15.80, change: 0.25, changePercent: 1.61 },
  { code: '01398', name: '工商银行', market: '港股', industry: '银行', price: 5.60, change: 0.08, changePercent: 1.45 },
];

const createAnnualData = (
  stockCode: string,
  stockName: string,
  market: MarketType,
  year: number,
  data: {
    totalAssets: number;
    totalLiabilities: number;
    totalEquity: number;
    currentAssets: number;
    currentLiabilities: number;
    nonCurrentAssets: number;
    nonCurrentLiabilities: number;
    inventory: number;
    accountsReceivable: number;
    revenue: number;
    grossProfit: number;
    netProfit: number;
    operatingProfit: number;
    eps: number;
    grossMargin: number;
    netMargin: number;
    operatingCashFlow: number;
    investingCashFlow: number;
    financingCashFlow: number;
    netCashFlow: number;
  }
): FinancialData => ({
  stockCode,
  stockName,
  market,
  reportDate: `${year}-12-31`,
  periodType: 'annual',
  balanceSheet: {
    totalAssets: data.totalAssets,
    totalLiabilities: data.totalLiabilities,
    totalEquity: data.totalEquity,
    currentAssets: data.currentAssets,
    currentLiabilities: data.currentLiabilities,
    nonCurrentAssets: data.nonCurrentAssets,
    nonCurrentLiabilities: data.nonCurrentLiabilities,
    inventory: data.inventory,
    accountsReceivable: data.accountsReceivable,
  },
  incomeStatement: {
    revenue: data.revenue,
    grossProfit: data.grossProfit,
    netProfit: data.netProfit,
    operatingProfit: data.operatingProfit,
    eps: data.eps,
    grossMargin: data.grossMargin,
    netMargin: data.netMargin,
  },
  cashFlow: {
    operatingCashFlow: data.operatingCashFlow,
    investingCashFlow: data.investingCashFlow,
    financingCashFlow: data.financingCashFlow,
    netCashFlow: data.netCashFlow,
  },
});

const createQuarterData = (
  stockCode: string,
  stockName: string,
  market: MarketType,
  year: number,
  quarter: number,
  data: {
    totalAssets: number;
    totalLiabilities: number;
    totalEquity: number;
    currentAssets: number;
    currentLiabilities: number;
    nonCurrentAssets: number;
    nonCurrentLiabilities: number;
    inventory: number;
    accountsReceivable: number;
    revenue: number;
    grossProfit: number;
    netProfit: number;
    operatingProfit: number;
    eps: number;
    grossMargin: number;
    netMargin: number;
    operatingCashFlow: number;
    investingCashFlow: number;
    financingCashFlow: number;
    netCashFlow: number;
  }
): FinancialData => ({
  stockCode,
  stockName,
  market,
  reportDate: `${year}-${quarter * 3}-30`,
  periodType: 'quarter',
  balanceSheet: {
    totalAssets: data.totalAssets,
    totalLiabilities: data.totalLiabilities,
    totalEquity: data.totalEquity,
    currentAssets: data.currentAssets,
    currentLiabilities: data.currentLiabilities,
    nonCurrentAssets: data.nonCurrentAssets,
    nonCurrentLiabilities: data.nonCurrentLiabilities,
    inventory: data.inventory,
    accountsReceivable: data.accountsReceivable,
  },
  incomeStatement: {
    revenue: data.revenue,
    grossProfit: data.grossProfit,
    netProfit: data.netProfit,
    operatingProfit: data.operatingProfit,
    eps: data.eps,
    grossMargin: data.grossMargin,
    netMargin: data.netMargin,
  },
  cashFlow: {
    operatingCashFlow: data.operatingCashFlow,
    investingCashFlow: data.investingCashFlow,
    financingCashFlow: data.financingCashFlow,
    netCashFlow: data.netCashFlow,
  },
});

export const financialDataMap: Record<string, FinancialData[]> = {
  'a股_600519': [
    createAnnualData('600519', '贵州茅台', 'a股', 2022, {
      totalAssets: 2575800, totalLiabilities: 525600, totalEquity: 2050200,
      currentAssets: 2156800, currentLiabilities: 412500, nonCurrentAssets: 419000, nonCurrentLiabilities: 113100,
      inventory: 356800, accountsReceivable: 85600,
      revenue: 1364600, grossProfit: 1050600, netProfit: 627200, operatingProfit: 825600,
      eps: 50.01, grossMargin: 77.0, netMargin: 46.0,
      operatingCashFlow: 756800, investingCashFlow: -125600, financingCashFlow: -685600, netCashFlow: -54400,
    }),
    createAnnualData('600519', '贵州茅台', 'a股', 2023, {
      totalAssets: 2985600, totalLiabilities: 605200, totalEquity: 2380400,
      currentAssets: 2525400, currentLiabilities: 473800, nonCurrentAssets: 460200, nonCurrentLiabilities: 131400,
      inventory: 425600, accountsReceivable: 98500,
      revenue: 1556800, grossProfit: 1185500, netProfit: 745700, operatingProfit: 968500,
      eps: 59.45, grossMargin: 76.2, netMargin: 47.9,
      operatingCashFlow: 885600, investingCashFlow: -145800, financingCashFlow: -795600, netCashFlow: -55800,
    }),
    createAnnualData('600519', '贵州茅台', 'a股', 2024, {
      totalAssets: 3405600, totalLiabilities: 685200, totalEquity: 2720400,
      currentAssets: 2865400, currentLiabilities: 523800, nonCurrentAssets: 540200, nonCurrentLiabilities: 161400,
      inventory: 485600, accountsReceivable: 115600,
      revenue: 1726800, grossProfit: 1320500, netProfit: 855700, operatingProfit: 1086500,
      eps: 68.17, grossMargin: 76.5, netMargin: 49.6,
      operatingCashFlow: 1002500, investingCashFlow: -156800, financingCashFlow: -895600, netCashFlow: -49900,
    }),
    createQuarterData('600519', '贵州茅台', 'a股', 2024, 1, {
      totalAssets: 3256000, totalLiabilities: 652500, totalEquity: 2603500,
      currentAssets: 2725400, currentLiabilities: 501200, nonCurrentAssets: 530600, nonCurrentLiabilities: 151300,
      inventory: 462500, accountsReceivable: 108500,
      revenue: 385600, grossProfit: 295600, netProfit: 188500, operatingProfit: 245600,
      eps: 15.02, grossMargin: 76.7, netMargin: 48.9,
      operatingCashFlow: 225600, investingCashFlow: -38500, financingCashFlow: -225600, netCashFlow: -38500,
    }),
    createQuarterData('600519', '贵州茅台', 'a股', 2024, 2, {
      totalAssets: 3325600, totalLiabilities: 668800, totalEquity: 2656800,
      currentAssets: 2795400, currentLiabilities: 512500, nonCurrentAssets: 530200, nonCurrentLiabilities: 156300,
      inventory: 473800, accountsReceivable: 112500,
      revenue: 456800, grossProfit: 350600, netProfit: 228500, operatingProfit: 298500,
      eps: 18.21, grossMargin: 76.7, netMargin: 49.9,
      operatingCashFlow: 285600, investingCashFlow: -41200, financingCashFlow: -245600, netCashFlow: -1200,
    }),
    createQuarterData('600519', '贵州茅台', 'a股', 2024, 3, {
      totalAssets: 3365600, totalLiabilities: 676800, totalEquity: 2688800,
      currentAssets: 2825400, currentLiabilities: 518500, nonCurrentAssets: 540200, nonCurrentLiabilities: 158300,
      inventory: 480600, accountsReceivable: 114500,
      revenue: 442500, grossProfit: 338500, netProfit: 218500, operatingProfit: 285600,
      eps: 17.42, grossMargin: 76.5, netMargin: 49.4,
      operatingCashFlow: 265600, investingCashFlow: -38500, financingCashFlow: -215600, netCashFlow: 11500,
    }),
    createQuarterData('600519', '贵州茅台', 'a股', 2024, 4, {
      totalAssets: 3405600, totalLiabilities: 685200, totalEquity: 2720400,
      currentAssets: 2865400, currentLiabilities: 523800, nonCurrentAssets: 540200, nonCurrentLiabilities: 161400,
      inventory: 485600, accountsReceivable: 115600,
      revenue: 441900, grossProfit: 335800, netProfit: 220200, operatingProfit: 256800,
      eps: 17.52, grossMargin: 76.0, netMargin: 49.8,
      operatingCashFlow: 225700, investingCashFlow: -38600, financingCashFlow: -208800, netCashFlow: -21700,
    }),
  ],
  'a股_000858': [
    createAnnualData('000858', '五粮液', 'a股', 2022, {
      totalAssets: 1325800, totalLiabilities: 345600, totalEquity: 980200,
      currentAssets: 1065800, currentLiabilities: 286800, nonCurrentAssets: 260000, nonCurrentLiabilities: 58800,
      inventory: 285600, accountsReceivable: 68500,
      revenue: 702500, grossProfit: 478600, netProfit: 251300, operatingProfit: 325600,
      eps: 5.27, grossMargin: 68.1, netMargin: 35.8,
      operatingCashFlow: 356800, investingCashFlow: -75600, financingCashFlow: -256800, netCashFlow: 24400,
    }),
    createAnnualData('000858', '五粮液', 'a股', 2023, {
      totalAssets: 1492000, totalLiabilities: 385600, totalEquity: 1106400,
      currentAssets: 1195800, currentLiabilities: 321800, nonCurrentAssets: 296200, nonCurrentLiabilities: 63800,
      inventory: 325600, accountsReceivable: 78500,
      revenue: 785600, grossProfit: 532200, netProfit: 280500, operatingProfit: 358600,
      eps: 5.88, grossMargin: 67.7, netMargin: 35.7,
      operatingCashFlow: 385600, investingCashFlow: -82500, financingCashFlow: -285600, netCashFlow: 17500,
    }),
    createAnnualData('000858', '五粮液', 'a股', 2024, {
      totalAssets: 1658000, totalLiabilities: 425600, totalEquity: 1232400,
      currentAssets: 1325800, currentLiabilities: 356800, nonCurrentAssets: 332200, nonCurrentLiabilities: 68800,
      inventory: 365600, accountsReceivable: 88500,
      revenue: 859000, grossProfit: 586200, netProfit: 308700, operatingProfit: 389500,
      eps: 6.45, grossMargin: 68.2, netMargin: 35.9,
      operatingCashFlow: 425600, investingCashFlow: -89500, financingCashFlow: -312500, netCashFlow: 23600,
    }),
    createQuarterData('000858', '五粮液', 'a股', 2024, 1, {
      totalAssets: 1585600, totalLiabilities: 405600, totalEquity: 1180000,
      currentAssets: 1275800, currentLiabilities: 345600, nonCurrentAssets: 309800, nonCurrentLiabilities: 60000,
      inventory: 350600, accountsReceivable: 85600,
      revenue: 185600, grossProfit: 125600, netProfit: 65600, operatingProfit: 82500,
      eps: 1.37, grossMargin: 67.7, netMargin: 35.3,
      operatingCashFlow: 95600, investingCashFlow: -22500, financingCashFlow: -78500, netCashFlow: -5400,
    }),
    createQuarterData('000858', '五粮液', 'a股', 2024, 2, {
      totalAssets: 1621800, totalLiabilities: 415600, totalEquity: 1206200,
      currentAssets: 1301800, currentLiabilities: 351800, nonCurrentAssets: 320000, nonCurrentLiabilities: 63800,
      inventory: 358600, accountsReceivable: 87500,
      revenue: 225600, grossProfit: 153600, netProfit: 78500, operatingProfit: 98500,
      eps: 1.64, grossMargin: 68.1, netMargin: 34.8,
      operatingCashFlow: 115600, investingCashFlow: -24500, financingCashFlow: -85600, netCashFlow: 5500,
    }),
    createQuarterData('000858', '五粮液', 'a股', 2024, 3, {
      totalAssets: 1640000, totalLiabilities: 420600, totalEquity: 1219400,
      currentAssets: 1315800, currentLiabilities: 354800, nonCurrentAssets: 324200, nonCurrentLiabilities: 65800,
      inventory: 362600, accountsReceivable: 88000,
      revenue: 225600, grossProfit: 154600, netProfit: 82500, operatingProfit: 102500,
      eps: 1.72, grossMargin: 68.5, netMargin: 36.6,
      operatingCashFlow: 118500, investingCashFlow: -21500, financingCashFlow: -75600, netCashFlow: 21400,
    }),
    createQuarterData('000858', '五粮液', 'a股', 2024, 4, {
      totalAssets: 1658000, totalLiabilities: 425600, totalEquity: 1232400,
      currentAssets: 1325800, currentLiabilities: 356800, nonCurrentAssets: 332200, nonCurrentLiabilities: 68800,
      inventory: 365600, accountsReceivable: 88500,
      revenue: 222200, grossProfit: 152400, netProfit: 82100, operatingProfit: 96000,
      eps: 1.72, grossMargin: 68.6, netMargin: 36.9,
      operatingCashFlow: 95900, investingCashFlow: -21000, financingCashFlow: -72800, netCashFlow: 2100,
    }),
  ],
  'a股_601318': [
    createAnnualData('601318', '中国平安', 'a股', 2022, {
      totalAssets: 112350000, totalLiabilities: 104250000, totalEquity: 8100000,
      currentAssets: 42560000, currentLiabilities: 65250000, nonCurrentAssets: 69790000, nonCurrentLiabilities: 39000000,
      inventory: 0, accountsReceivable: 856000,
      revenue: 12580000, grossProfit: 4250000, netProfit: 856000, operatingProfit: 1285000,
      eps: 7.15, grossMargin: 33.8, netMargin: 6.8,
      operatingCashFlow: 4250000, investingCashFlow: -3050000, financingCashFlow: -1050000, netCashFlow: 150000,
    }),
    createAnnualData('601318', '中国平安', 'a股', 2023, {
      totalAssets: 116920000, totalLiabilities: 108300000, totalEquity: 8620000,
      currentAssets: 44120000, currentLiabilities: 66880000, nonCurrentAssets: 72800000, nonCurrentLiabilities: 41420000,
      inventory: 0, accountsReceivable: 925000,
      revenue: 13150000, grossProfit: 4530000, netProfit: 940000, operatingProfit: 1426000,
      eps: 7.85, grossMargin: 34.5, netMargin: 7.2,
      operatingCashFlow: 4410000, investingCashFlow: -3150000, financingCashFlow: -1080000, netCashFlow: 180000,
    }),
    createAnnualData('601318', '中国平安', 'a股', 2024, {
      totalAssets: 121480000, totalLiabilities: 112350000, totalEquity: 9130000,
      currentAssets: 45680000, currentLiabilities: 68520000, nonCurrentAssets: 75800000, nonCurrentLiabilities: 43830000,
      inventory: 0, accountsReceivable: 985000,
      revenue: 13700000, grossProfit: 4820000, netProfit: 1025000, operatingProfit: 1568000,
      eps: 8.56, grossMargin: 35.2, netMargin: 7.5,
      operatingCashFlow: 4580000, investingCashFlow: -3250000, financingCashFlow: -1120000, netCashFlow: 210000,
    }),
    createQuarterData('601318', '中国平安', 'a股', 2024, 1, {
      totalAssets: 118700000, totalLiabilities: 110330000, totalEquity: 8370000,
      currentAssets: 44700000, currentLiabilities: 67600000, nonCurrentAssets: 74000000, nonCurrentLiabilities: 42730000,
      inventory: 0, accountsReceivable: 956000,
      revenue: 3250000, grossProfit: 1150000, netProfit: 235000, operatingProfit: 368000,
      eps: 1.97, grossMargin: 35.4, netMargin: 7.2,
      operatingCashFlow: 1120000, investingCashFlow: -785000, financingCashFlow: -285000, netCashFlow: 50000,
    }),
    createQuarterData('601318', '中国平安', 'a股', 2024, 2, {
      totalAssets: 120090000, totalLiabilities: 111340000, totalEquity: 8750000,
      currentAssets: 45190000, currentLiabilities: 68060000, nonCurrentAssets: 74900000, nonCurrentLiabilities: 43280000,
      inventory: 0, accountsReceivable: 970000,
      revenue: 3450000, grossProfit: 1220000, netProfit: 265000, operatingProfit: 408000,
      eps: 2.21, grossMargin: 35.4, netMargin: 7.7,
      operatingCashFlow: 1160000, investingCashFlow: -820000, financingCashFlow: -295000, netCashFlow: 45000,
    }),
    createQuarterData('601318', '中国平安', 'a股', 2024, 3, {
      totalAssets: 120785000, totalLiabilities: 111845000, totalEquity: 8940000,
      currentAssets: 45437500, currentLiabilities: 68290000, nonCurrentAssets: 75347500, nonCurrentLiabilities: 43555000,
      inventory: 0, accountsReceivable: 977500,
      revenue: 3475000, grossProfit: 1245000, netProfit: 262500, operatingProfit: 400000,
      eps: 2.19, grossMargin: 35.8, netMargin: 7.6,
      operatingCashFlow: 1150000, investingCashFlow: -822500, financingCashFlow: -270000, netCashFlow: 57500,
    }),
    createQuarterData('601318', '中国平安', 'a股', 2024, 4, {
      totalAssets: 121480000, totalLiabilities: 112350000, totalEquity: 9130000,
      currentAssets: 45680000, currentLiabilities: 68520000, nonCurrentAssets: 75800000, nonCurrentLiabilities: 43830000,
      inventory: 0, accountsReceivable: 985000,
      revenue: 3525000, grossProfit: 1205000, netProfit: 262500, operatingProfit: 392000,
      eps: 2.19, grossMargin: 34.2, netMargin: 7.4,
      operatingCashFlow: 1150000, investingCashFlow: -822500, financingCashFlow: -270000, netCashFlow: 57500,
    }),
  ],
  'a股_600036': [
    createAnnualData('600036', '招商银行', 'a股', 2022, {
      totalAssets: 98560000, totalLiabilities: 92230000, totalEquity: 6330000,
      currentAssets: 32560000, currentLiabilities: 82560000, nonCurrentAssets: 66000000, nonCurrentLiabilities: 9670000,
      inventory: 0, accountsReceivable: 1256000,
      revenue: 3150000, grossProfit: 1950000, netProfit: 1280000, operatingProfit: 1680000,
      eps: 6.05, grossMargin: 61.9, netMargin: 40.6,
      operatingCashFlow: 2550000, investingCashFlow: -1950000, financingCashFlow: -520000, netCashFlow: 80000,
    }),
    createAnnualData('600036', '招商银行', 'a股', 2023, {
      totalAssets: 103560000, totalLiabilities: 96730000, totalEquity: 6830000,
      currentAssets: 34120000, currentLiabilities: 86060000, nonCurrentAssets: 69440000, nonCurrentLiabilities: 10670000,
      inventory: 0, accountsReceivable: 1385000,
      revenue: 3335000, grossProfit: 2065000, netProfit: 1365000, operatingProfit: 1785000,
      eps: 6.46, grossMargin: 61.9, netMargin: 40.9,
      operatingCashFlow: 2700000, investingCashFlow: -2050000, financingCashFlow: -550000, netCashFlow: 100000,
    }),
    createAnnualData('600036', '招商银行', 'a股', 2024, {
      totalAssets: 108560000, totalLiabilities: 101230000, totalEquity: 7330000,
      currentAssets: 35680000, currentLiabilities: 89560000, nonCurrentAssets: 72880000, nonCurrentLiabilities: 11670000,
      inventory: 0, accountsReceivable: 1515000,
      revenue: 3520000, grossProfit: 2180000, netProfit: 1450000, operatingProfit: 1890000,
      eps: 6.89, grossMargin: 61.9, netMargin: 41.2,
      operatingCashFlow: 2850000, investingCashFlow: -2150000, financingCashFlow: -580000, netCashFlow: 120000,
    }),
    createQuarterData('600036', '招商银行', 'a股', 2024, 1, {
      totalAssets: 106060000, totalLiabilities: 99230000, totalEquity: 6830000,
      currentAssets: 34680000, currentLiabilities: 88060000, nonCurrentAssets: 71380000, nonCurrentLiabilities: 11170000,
      inventory: 0, accountsReceivable: 1450000,
      revenue: 855000, grossProfit: 528000, netProfit: 348000, operatingProfit: 458000,
      eps: 1.65, grossMargin: 61.8, netMargin: 40.7,
      operatingCashFlow: 685000, investingCashFlow: -525000, financingCashFlow: -145000, netCashFlow: 15000,
    }),
    createQuarterData('600036', '招商银行', 'a股', 2024, 2, {
      totalAssets: 107310000, totalLiabilities: 100230000, totalEquity: 7080000,
      currentAssets: 35180000, currentLiabilities: 88810000, nonCurrentAssets: 72130000, nonCurrentLiabilities: 11420000,
      inventory: 0, accountsReceivable: 1482500,
      revenue: 887500, grossProfit: 548000, netProfit: 365000, operatingProfit: 478000,
      eps: 1.73, grossMargin: 61.7, netMargin: 41.1,
      operatingCashFlow: 715000, investingCashFlow: -545000, financingCashFlow: -155000, netCashFlow: 15000,
    }),
    createQuarterData('600036', '招商银行', 'a股', 2024, 3, {
      totalAssets: 107935000, totalLiabilities: 100730000, totalEquity: 7205000,
      currentAssets: 35430000, currentLiabilities: 89185000, nonCurrentAssets: 72505000, nonCurrentLiabilities: 11545000,
      inventory: 0, accountsReceivable: 1498750,
      revenue: 896250, grossProfit: 554000, netProfit: 367500, operatingProfit: 482000,
      eps: 1.74, grossMargin: 61.8, netMargin: 41.0,
      operatingCashFlow: 725000, investingCashFlow: -547500, financingCashFlow: -165000, netCashFlow: 12500,
    }),
    createQuarterData('600036', '招商银行', 'a股', 2024, 4, {
      totalAssets: 108560000, totalLiabilities: 101230000, totalEquity: 7330000,
      currentAssets: 35680000, currentLiabilities: 89560000, nonCurrentAssets: 72880000, nonCurrentLiabilities: 11670000,
      inventory: 0, accountsReceivable: 1515000,
      revenue: 881250, grossProfit: 550000, netProfit: 369500, operatingProfit: 472000,
      eps: 1.75, grossMargin: 62.4, netMargin: 41.9,
      operatingCashFlow: 725000, investingCashFlow: -532500, financingCashFlow: -115000, netCashFlow: 77500,
    }),
  ],
  'a股_000001': [
    createAnnualData('000001', '平安银行', 'a股', 2022, {
      totalAssets: 48560000, totalLiabilities: 45230000, totalEquity: 3330000,
      currentAssets: 18560000, currentLiabilities: 40560000, nonCurrentAssets: 30000000, nonCurrentLiabilities: 4670000,
      inventory: 0, accountsReceivable: 856000,
      revenue: 1520000, grossProfit: 985000, netProfit: 455000, operatingProfit: 685000,
      eps: 1.65, grossMargin: 64.8, netMargin: 29.9,
      operatingCashFlow: 1250000, investingCashFlow: -950000, financingCashFlow: -250000, netCashFlow: 50000,
    }),
    createAnnualData('000001', '平安银行', 'a股', 2023, {
      totalAssets: 51560000, totalLiabilities: 48030000, totalEquity: 3530000,
      currentAssets: 19620000, currentLiabilities: 42860000, nonCurrentAssets: 31940000, nonCurrentLiabilities: 5170000,
      inventory: 0, accountsReceivable: 925000,
      revenue: 1620000, grossProfit: 1055000, netProfit: 495000, operatingProfit: 735000,
      eps: 1.79, grossMargin: 65.1, netMargin: 30.6,
      operatingCashFlow: 1350000, investingCashFlow: -1020000, financingCashFlow: -280000, netCashFlow: 50000,
    }),
    createAnnualData('000001', '平安银行', 'a股', 2024, {
      totalAssets: 54560000, totalLiabilities: 50830000, totalEquity: 3730000,
      currentAssets: 20680000, currentLiabilities: 45160000, nonCurrentAssets: 33880000, nonCurrentLiabilities: 5670000,
      inventory: 0, accountsReceivable: 985000,
      revenue: 1720000, grossProfit: 1125000, netProfit: 535000, operatingProfit: 785000,
      eps: 1.93, grossMargin: 65.4, netMargin: 31.1,
      operatingCashFlow: 1450000, investingCashFlow: -1090000, financingCashFlow: -310000, netCashFlow: 50000,
    }),
    createQuarterData('000001', '平安银行', 'a股', 2024, 1, {
      totalAssets: 53060000, totalLiabilities: 49530000, totalEquity: 3530000,
      currentAssets: 20180000, currentLiabilities: 44060000, nonCurrentAssets: 32880000, nonCurrentLiabilities: 5470000,
      inventory: 0, accountsReceivable: 956000,
      revenue: 415000, grossProfit: 272000, netProfit: 128000, operatingProfit: 188000,
      eps: 0.46, grossMargin: 65.5, netMargin: 30.8,
      operatingCashFlow: 350000, investingCashFlow: -265000, financingCashFlow: -75000, netCashFlow: 10000,
    }),
    createQuarterData('000001', '平安银行', 'a股', 2024, 2, {
      totalAssets: 53810000, totalLiabilities: 50180000, totalEquity: 3630000,
      currentAssets: 20430000, currentLiabilities: 44610000, nonCurrentAssets: 33380000, nonCurrentLiabilities: 5570000,
      inventory: 0, accountsReceivable: 970000,
      revenue: 437500, grossProfit: 288000, netProfit: 135000, operatingProfit: 198000,
      eps: 0.49, grossMargin: 65.8, netMargin: 30.9,
      operatingCashFlow: 375000, investingCashFlow: -280000, financingCashFlow: -85000, netCashFlow: 10000,
    }),
    createQuarterData('000001', '平安银行', 'a股', 2024, 3, {
      totalAssets: 54185000, totalLiabilities: 50505000, totalEquity: 3680000,
      currentAssets: 20555000, currentLiabilities: 44885000, nonCurrentAssets: 33630000, nonCurrentLiabilities: 5620000,
      inventory: 0, accountsReceivable: 977500,
      revenue: 441250, grossProfit: 290000, netProfit: 137500, operatingProfit: 202000,
      eps: 0.50, grossMargin: 65.7, netMargin: 31.2,
      operatingCashFlow: 362500, investingCashFlow: -277500, financingCashFlow: -75000, netCashFlow: 10000,
    }),
    createQuarterData('000001', '平安银行', 'a股', 2024, 4, {
      totalAssets: 54560000, totalLiabilities: 50830000, totalEquity: 3730000,
      currentAssets: 20680000, currentLiabilities: 45160000, nonCurrentAssets: 33880000, nonCurrentLiabilities: 5670000,
      inventory: 0, accountsReceivable: 985000,
      revenue: 426250, grossProfit: 275000, netProfit: 134500, operatingProfit: 197000,
      eps: 0.49, grossMargin: 64.5, netMargin: 31.6,
      operatingCashFlow: 362500, investingCashFlow: -267500, financingCashFlow: -75000, netCashFlow: 20000,
    }),
  ],
  'a股_601899': [
    createAnnualData('601899', '紫金矿业', 'a股', 2022, {
      totalAssets: 2356000, totalLiabilities: 1425000, totalEquity: 931000,
      currentAssets: 1056000, currentLiabilities: 925000, nonCurrentAssets: 1300000, nonCurrentLiabilities: 500000,
      inventory: 285600, accountsReceivable: 125600,
      revenue: 2256000, grossProfit: 385000, netProfit: 205000, operatingProfit: 265000,
      eps: 0.88, grossMargin: 17.1, netMargin: 9.1,
      operatingCashFlow: 356000, investingCashFlow: -425000, financingCashFlow: 65000, netCashFlow: -5000,
    }),
    createAnnualData('601899', '紫金矿业', 'a股', 2023, {
      totalAssets: 2656000, totalLiabilities: 1585000, totalEquity: 1071000,
      currentAssets: 1185000, currentLiabilities: 1025000, nonCurrentAssets: 1471000, nonCurrentLiabilities: 560000,
      inventory: 325600, accountsReceivable: 138500,
      revenue: 2456000, grossProfit: 425000, netProfit: 235000, operatingProfit: 295000,
      eps: 1.00, grossMargin: 17.3, netMargin: 9.6,
      operatingCashFlow: 395000, investingCashFlow: -465000, financingCashFlow: 65000, netCashFlow: -5000,
    }),
    createAnnualData('601899', '紫金矿业', 'a股', 2024, {
      totalAssets: 2956000, totalLiabilities: 1745000, totalEquity: 1211000,
      currentAssets: 1315000, currentLiabilities: 1125000, nonCurrentAssets: 1641000, nonCurrentLiabilities: 620000,
      inventory: 365600, accountsReceivable: 151500,
      revenue: 2656000, grossProfit: 465000, netProfit: 265000, operatingProfit: 325000,
      eps: 1.13, grossMargin: 17.5, netMargin: 10.0,
      operatingCashFlow: 435000, investingCashFlow: -505000, financingCashFlow: 65000, netCashFlow: -5000,
    }),
    createQuarterData('601899', '紫金矿业', 'a股', 2024, 1, {
      totalAssets: 2806000, totalLiabilities: 1665000, totalEquity: 1141000,
      currentAssets: 1250000, currentLiabilities: 1075000, nonCurrentAssets: 1556000, nonCurrentLiabilities: 590000,
      inventory: 350600, accountsReceivable: 145000,
      revenue: 635000, grossProfit: 110000, netProfit: 62000, operatingProfit: 78000,
      eps: 0.26, grossMargin: 17.3, netMargin: 9.8,
      operatingCashFlow: 105000, investingCashFlow: -120000, financingCashFlow: 15000, netCashFlow: 0,
    }),
    createQuarterData('601899', '紫金矿业', 'a股', 2024, 2, {
      totalAssets: 2881000, totalLiabilities: 1705000, totalEquity: 1176000,
      currentAssets: 1282500, currentLiabilities: 1100000, nonCurrentAssets: 1598500, nonCurrentLiabilities: 605000,
      inventory: 358100, accountsReceivable: 148250,
      revenue: 668750, grossProfit: 117500, netProfit: 67500, operatingProfit: 85000,
      eps: 0.29, grossMargin: 17.6, netMargin: 10.1,
      operatingCashFlow: 115000, investingCashFlow: -130000, financingCashFlow: 15000, netCashFlow: 0,
    }),
    createQuarterData('601899', '紫金矿业', 'a股', 2024, 3, {
      totalAssets: 2918500, totalLiabilities: 1725000, totalEquity: 1193500,
      currentAssets: 1298750, currentLiabilities: 1112500, nonCurrentAssets: 1619750, nonCurrentLiabilities: 612500,
      inventory: 361850, accountsReceivable: 149875,
      revenue: 668750, grossProfit: 118750, netProfit: 67500, operatingProfit: 85000,
      eps: 0.29, grossMargin: 17.8, netMargin: 10.1,
      operatingCashFlow: 107500, investingCashFlow: -127500, financingCashFlow: 17500, netCashFlow: -2500,
    }),
    createQuarterData('601899', '紫金矿业', 'a股', 2024, 4, {
      totalAssets: 2956000, totalLiabilities: 1745000, totalEquity: 1211000,
      currentAssets: 1315000, currentLiabilities: 1125000, nonCurrentAssets: 1641000, nonCurrentLiabilities: 620000,
      inventory: 365600, accountsReceivable: 151500,
      revenue: 683500, grossProfit: 118750, netProfit: 68000, operatingProfit: 77000,
      eps: 0.29, grossMargin: 17.4, netMargin: 9.9,
      operatingCashFlow: 107500, investingCashFlow: -127500, financingCashFlow: 17500, netCashFlow: -2500,
    }),
  ],
  'a股_300750': [
    createAnnualData('300750', '宁德时代', 'a股', 2022, {
      totalAssets: 3256000, totalLiabilities: 2056000, totalEquity: 1200000,
      currentAssets: 2256000, currentLiabilities: 1656000, nonCurrentAssets: 1000000, nonCurrentLiabilities: 400000,
      inventory: 585600, accountsReceivable: 325600,
      revenue: 3056000, grossProfit: 456000, netProfit: 302000, operatingProfit: 365000,
      eps: 13.67, grossMargin: 14.9, netMargin: 9.9,
      operatingCashFlow: 585000, investingCashFlow: -725000, financingCashFlow: 135000, netCashFlow: -5000,
    }),
    createAnnualData('300750', '宁德时代', 'a股', 2023, {
      totalAssets: 3921000, totalLiabilities: 2441000, totalEquity: 1480000,
      currentAssets: 2756000, currentLiabilities: 1956000, nonCurrentAssets: 1165000, nonCurrentLiabilities: 485000,
      inventory: 685600, accountsReceivable: 385600,
      revenue: 3308000, grossProfit: 512000, netProfit: 328000, operatingProfit: 396000,
      eps: 14.86, grossMargin: 15.5, netMargin: 9.9,
      operatingCashFlow: 635000, investingCashFlow: -790000, financingCashFlow: 145000, netCashFlow: -10000,
    }),
    createAnnualData('300750', '宁德时代', 'a股', 2024, {
      totalAssets: 4586000, totalLiabilities: 2856000, totalEquity: 1730000,
      currentAssets: 3256000, currentLiabilities: 2256000, nonCurrentAssets: 1330000, nonCurrentLiabilities: 600000,
      inventory: 785600, accountsReceivable: 445600,
      revenue: 3560000, grossProfit: 568000, netProfit: 352000, operatingProfit: 425000,
      eps: 15.88, grossMargin: 15.9, netMargin: 9.9,
      operatingCashFlow: 685000, investingCashFlow: -856000, financingCashFlow: 156000, netCashFlow: -15000,
    }),
    createQuarterData('300750', '宁德时代', 'a股', 2024, 1, {
      totalAssets: 4253500, totalLiabilities: 2648500, totalEquity: 1605000,
      currentAssets: 3006000, currentLiabilities: 2106000, nonCurrentAssets: 1247500, nonCurrentLiabilities: 542500,
      inventory: 735600, accountsReceivable: 415600,
      revenue: 825000, grossProfit: 130000, netProfit: 82000, operatingProfit: 98000,
      eps: 3.71, grossMargin: 15.8, netMargin: 9.9,
      operatingCashFlow: 155000, investingCashFlow: -205000, financingCashFlow: 35000, netCashFlow: -15000,
    }),
    createQuarterData('300750', '宁德时代', 'a股', 2024, 2, {
      totalAssets: 4419750, totalLiabilities: 2752250, totalEquity: 1667500,
      currentAssets: 3131000, currentLiabilities: 2181000, nonCurrentAssets: 1288750, nonCurrentLiabilities: 571250,
      inventory: 760600, accountsReceivable: 430600,
      revenue: 912500, grossProfit: 145000, netProfit: 91000, operatingProfit: 110000,
      eps: 4.12, grossMargin: 15.9, netMargin: 10.0,
      operatingCashFlow: 175000, investingCashFlow: -225000, financingCashFlow: 45000, netCashFlow: -5000,
    }),
    createQuarterData('300750', '宁德时代', 'a股', 2024, 3, {
      totalAssets: 4502875, totalLiabilities: 2804125, totalEquity: 1698750,
      currentAssets: 3193500, currentLiabilities: 2218500, nonCurrentAssets: 1309375, nonCurrentLiabilities: 585625,
      inventory: 773100, accountsReceivable: 438100,
      revenue: 906250, grossProfit: 146250, netProfit: 90500, operatingProfit: 108750,
      eps: 4.10, grossMargin: 16.1, netMargin: 10.0,
      operatingCashFlow: 177500, investingCashFlow: -215000, financingCashFlow: 38750, netCashFlow: 1250,
    }),
    createQuarterData('300750', '宁德时代', 'a股', 2024, 4, {
      totalAssets: 4586000, totalLiabilities: 2856000, totalEquity: 1730000,
      currentAssets: 3256000, currentLiabilities: 2256000, nonCurrentAssets: 1330000, nonCurrentLiabilities: 600000,
      inventory: 785600, accountsReceivable: 445600,
      revenue: 916250, grossProfit: 146750, netProfit: 88500, operatingProfit: 108250,
      eps: 4.01, grossMargin: 16.0, netMargin: 9.7,
      operatingCashFlow: 177500, investingCashFlow: -211000, financingCashFlow: 37250, netCashFlow: 3750,
    }),
  ],
  'a股_002594': [
    createAnnualData('002594', '比亚迪', 'a股', 2022, {
      totalAssets: 3256000, totalLiabilities: 2356000, totalEquity: 900000,
      currentAssets: 2156000, currentLiabilities: 1956000, nonCurrentAssets: 1100000, nonCurrentLiabilities: 400000,
      inventory: 685600, accountsReceivable: 385600,
      revenue: 4256000, grossProfit: 425000, netProfit: 165000, operatingProfit: 225000,
      eps: 4.81, grossMargin: 10.0, netMargin: 3.9,
      operatingCashFlow: 425000, investingCashFlow: -785000, financingCashFlow: 350000, netCashFlow: -10000,
    }),
    createAnnualData('002594', '比亚迪', 'a股', 2023, {
      totalAssets: 4056000, totalLiabilities: 2941000, totalEquity: 1115000,
      currentAssets: 2656000, currentLiabilities: 2341000, nonCurrentAssets: 1400000, nonCurrentLiabilities: 600000,
      inventory: 785600, accountsReceivable: 485600,
      revenue: 4723000, grossProfit: 512000, netProfit: 216000, operatingProfit: 285000,
      eps: 6.26, grossMargin: 10.8, netMargin: 4.6,
      operatingCashFlow: 485000, investingCashFlow: -856000, financingCashFlow: 365000, netCashFlow: -5000,
    }),
    createAnnualData('002594', '比亚迪', 'a股', 2024, {
      totalAssets: 4856000, totalLiabilities: 3512000, totalEquity: 1344000,
      currentAssets: 3156000, currentLiabilities: 2756000, nonCurrentAssets: 1700000, nonCurrentLiabilities: 756000,
      inventory: 885600, accountsReceivable: 585600,
      revenue: 5256000, grossProfit: 625000, netProfit: 285000, operatingProfit: 365000,
      eps: 8.28, grossMargin: 11.9, netMargin: 5.4,
      operatingCashFlow: 585000, investingCashFlow: -925000, financingCashFlow: 335000, netCashFlow: -5000,
    }),
    createQuarterData('002594', '比亚迪', 'a股', 2024, 1, {
      totalAssets: 4456000, totalLiabilities: 3212000, totalEquity: 1244000,
      currentAssets: 2956000, currentLiabilities: 2556000, nonCurrentAssets: 1500000, nonCurrentLiabilities: 656000,
      inventory: 835600, accountsReceivable: 535600,
      revenue: 1256000, grossProfit: 145000, netProfit: 65000, operatingProfit: 85000,
      eps: 1.89, grossMargin: 11.5, netMargin: 5.2,
      operatingCashFlow: 135000, investingCashFlow: -225000, financingCashFlow: 85000, netCashFlow: -5000,
    }),
    createQuarterData('002594', '比亚迪', 'a股', 2024, 2, {
      totalAssets: 4656000, totalLiabilities: 3362000, totalEquity: 1294000,
      currentAssets: 3056000, currentLiabilities: 2656000, nonCurrentAssets: 1600000, nonCurrentLiabilities: 706000,
      inventory: 860600, accountsReceivable: 560600,
      revenue: 1356000, grossProfit: 165000, netProfit: 75000, operatingProfit: 98000,
      eps: 2.19, grossMargin: 12.2, netMargin: 5.5,
      operatingCashFlow: 155000, investingCashFlow: -245000, financingCashFlow: 85000, netCashFlow: -5000,
    }),
    createQuarterData('002594', '比亚迪', 'a股', 2024, 3, {
      totalAssets: 4756000, totalLiabilities: 3437000, totalEquity: 1319000,
      currentAssets: 3106000, currentLiabilities: 2706000, nonCurrentAssets: 1650000, nonCurrentLiabilities: 731000,
      inventory: 873100, accountsReceivable: 573100,
      revenue: 1325000, grossProfit: 157500, netProfit: 72500, operatingProfit: 93750,
      eps: 2.11, grossMargin: 11.9, netMargin: 5.5,
      operatingCashFlow: 147500, investingCashFlow: -237500, financingCashFlow: 87500, netCashFlow: -2500,
    }),
    createQuarterData('002594', '比亚迪', 'a股', 2024, 4, {
      totalAssets: 4856000, totalLiabilities: 3512000, totalEquity: 1344000,
      currentAssets: 3156000, currentLiabilities: 2756000, nonCurrentAssets: 1700000, nonCurrentLiabilities: 756000,
      inventory: 885600, accountsReceivable: 585600,
      revenue: 1320000, grossProfit: 157500, netProfit: 72500, operatingProfit: 77500,
      eps: 2.11, grossMargin: 12.0, netMargin: 5.5,
      operatingCashFlow: 147500, investingCashFlow: -217500, financingCashFlow: 77500, netCashFlow: 7500,
    }),
  ],
  '港股_00001': [
    createAnnualData('00001', '长和', '港股', 2022, {
      totalAssets: 16256000, totalLiabilities: 9856000, totalEquity: 6400000,
      currentAssets: 5856000, currentLiabilities: 5256000, nonCurrentAssets: 10400000, nonCurrentLiabilities: 4600000,
      inventory: 856000, accountsReceivable: 1256000,
      revenue: 3856000, grossProfit: 1256000, netProfit: 385000, operatingProfit: 525000,
      eps: 2.85, grossMargin: 32.6, netMargin: 10.0,
      operatingCashFlow: 856000, investingCashFlow: -725000, financingCashFlow: -125000, netCashFlow: 5000,
    }),
    createAnnualData('00001', '长和', '港股', 2023, {
      totalAssets: 17125000, totalLiabilities: 10356000, totalEquity: 6769000,
      currentAssets: 6125000, currentLiabilities: 5525000, nonCurrentAssets: 11000000, nonCurrentLiabilities: 4831000,
      inventory: 925000, accountsReceivable: 1325000,
      revenue: 4025000, grossProfit: 1325000, netProfit: 415000, operatingProfit: 565000,
      eps: 3.07, grossMargin: 32.9, netMargin: 10.3,
      operatingCashFlow: 925000, investingCashFlow: -785000, financingCashFlow: -135000, netCashFlow: 5000,
    }),
    createAnnualData('00001', '长和', '港股', 2024, {
      totalAssets: 18000000, totalLiabilities: 10856000, totalEquity: 7144000,
      currentAssets: 6400000, currentLiabilities: 5800000, nonCurrentAssets: 11600000, nonCurrentLiabilities: 5056000,
      inventory: 985000, accountsReceivable: 1385000,
      revenue: 4195000, grossProfit: 1395000, netProfit: 445000, operatingProfit: 605000,
      eps: 3.29, grossMargin: 33.3, netMargin: 10.6,
      operatingCashFlow: 995000, investingCashFlow: -845000, financingCashFlow: -145000, netCashFlow: 5000,
    }),
  ],
  '港股_00002': [
    createAnnualData('00002', '中电控股', '港股', 2022, {
      totalAssets: 22560000, totalLiabilities: 12560000, totalEquity: 10000000,
      currentAssets: 4856000, currentLiabilities: 5856000, nonCurrentAssets: 17704000, nonCurrentLiabilities: 6704000,
      inventory: 0, accountsReceivable: 1256000,
      revenue: 1856000, grossProfit: 985000, netProfit: 485000, operatingProfit: 625000,
      eps: 2.56, grossMargin: 53.1, netMargin: 26.1,
      operatingCashFlow: 1156000, investingCashFlow: -825000, financingCashFlow: -325000, netCashFlow: 5000,
    }),
    createAnnualData('00002', '中电控股', '港股', 2023, {
      totalAssets: 23256000, totalLiabilities: 12956000, totalEquity: 10300000,
      currentAssets: 5025600, currentLiabilities: 5985600, nonCurrentAssets: 18230400, nonCurrentLiabilities: 6970400,
      inventory: 0, accountsReceivable: 1285000,
      revenue: 1925000, grossProfit: 1025000, netProfit: 505000, operatingProfit: 655000,
      eps: 2.66, grossMargin: 53.2, netMargin: 26.2,
      operatingCashFlow: 1225000, investingCashFlow: -865000, financingCashFlow: -355000, netCashFlow: 5000,
    }),
    createAnnualData('00002', '中电控股', '港股', 2024, {
      totalAssets: 23956000, totalLiabilities: 13356000, totalEquity: 10600000,
      currentAssets: 5195600, currentLiabilities: 6115600, nonCurrentAssets: 18760400, nonCurrentLiabilities: 7240400,
      inventory: 0, accountsReceivable: 1315000,
      revenue: 1995000, grossProfit: 1065000, netProfit: 525000, operatingProfit: 685000,
      eps: 2.76, grossMargin: 53.4, netMargin: 26.3,
      operatingCashFlow: 1295000, investingCashFlow: -905000, financingCashFlow: -385000, netCashFlow: 5000,
    }),
  ],
  '港股_00003': [
    createAnnualData('00003', '香港中华煤气', '港股', 2022, {
      totalAssets: 8560000, totalLiabilities: 4256000, totalEquity: 4304000,
      currentAssets: 1856000, currentLiabilities: 1856000, nonCurrentAssets: 6704000, nonCurrentLiabilities: 2400000,
      inventory: 85600, accountsReceivable: 525600,
      revenue: 856000, grossProfit: 425000, netProfit: 215000, operatingProfit: 285000,
      eps: 1.28, grossMargin: 49.6, netMargin: 25.1,
      operatingCashFlow: 425000, investingCashFlow: -285000, financingCashFlow: -135000, netCashFlow: 5000,
    }),
    createAnnualData('00003', '香港中华煤气', '港股', 2023, {
      totalAssets: 8856000, totalLiabilities: 4385600, totalEquity: 4470400,
      currentAssets: 1925600, currentLiabilities: 1925600, nonCurrentAssets: 6930400, nonCurrentLiabilities: 2460000,
      inventory: 92500, accountsReceivable: 545600,
      revenue: 885000, grossProfit: 445000, netProfit: 225000, operatingProfit: 298000,
      eps: 1.34, grossMargin: 50.3, netMargin: 25.4,
      operatingCashFlow: 455000, investingCashFlow: -305000, financingCashFlow: -145000, netCashFlow: 5000,
    }),
    createAnnualData('00003', '香港中华煤气', '港股', 2024, {
      totalAssets: 9156000, totalLiabilities: 4515600, totalEquity: 4640400,
      currentAssets: 1995600, currentLiabilities: 1995600, nonCurrentAssets: 7160400, nonCurrentLiabilities: 2520000,
      inventory: 98500, accountsReceivable: 565600,
      revenue: 915000, grossProfit: 465000, netProfit: 235000, operatingProfit: 310000,
      eps: 1.40, grossMargin: 50.8, netMargin: 25.7,
      operatingCashFlow: 485000, investingCashFlow: -325000, financingCashFlow: -155000, netCashFlow: 5000,
    }),
  ],
  '港股_00005': [
    createAnnualData('00005', '汇丰控股', '港股', 2022, {
      totalAssets: 258560000, totalLiabilities: 245230000, totalEquity: 13330000,
      currentAssets: 85600000, currentLiabilities: 225600000, nonCurrentAssets: 172960000, nonCurrentLiabilities: 19630000,
      inventory: 0, accountsReceivable: 2560000,
      revenue: 6856000, grossProfit: 4256000, netProfit: 1856000, operatingProfit: 2525000,
      eps: 0.98, grossMargin: 62.1, netMargin: 27.1,
      operatingCashFlow: 8560000, investingCashFlow: -6856000, financingCashFlow: -1690000, netCashFlow: 14000,
    }),
    createAnnualData('00005', '汇丰控股', '港股', 2023, {
      totalAssets: 268560000, totalLiabilities: 254230000, totalEquity: 14330000,
      currentAssets: 88560000, currentLiabilities: 233600000, nonCurrentAssets: 180000000, nonCurrentLiabilities: 20630000,
      inventory: 0, accountsReceivable: 2725000,
      revenue: 7256000, grossProfit: 4556000, netProfit: 1985000, operatingProfit: 2725000,
      eps: 1.05, grossMargin: 62.8, netMargin: 27.4,
      operatingCashFlow: 9256000, investingCashFlow: -7256000, financingCashFlow: -1985000, netCashFlow: 15000,
    }),
    createAnnualData('00005', '汇丰控股', '港股', 2024, {
      totalAssets: 278560000, totalLiabilities: 263230000, totalEquity: 15330000,
      currentAssets: 91560000, currentLiabilities: 241600000, nonCurrentAssets: 187000000, nonCurrentLiabilities: 21630000,
      inventory: 0, accountsReceivable: 2885000,
      revenue: 7656000, grossProfit: 4856000, netProfit: 2115000, operatingProfit: 2925000,
      eps: 1.12, grossMargin: 63.4, netMargin: 27.6,
      operatingCashFlow: 9956000, investingCashFlow: -7656000, financingCashFlow: -2285000, netCashFlow: 15000,
    }),
  ],
  '港股_00006': [
    createAnnualData('00006', '香港电灯', '港股', 2022, {
      totalAssets: 15256000, totalLiabilities: 7856000, totalEquity: 7400000,
      currentAssets: 2856000, currentLiabilities: 2856000, nonCurrentAssets: 12400000, nonCurrentLiabilities: 5000000,
      inventory: 0, accountsReceivable: 625600,
      revenue: 1256000, grossProfit: 685000, netProfit: 325000, operatingProfit: 425000,
      eps: 2.85, grossMargin: 54.5, netMargin: 25.9,
      operatingCashFlow: 685000, investingCashFlow: -425000, financingCashFlow: -255000, netCashFlow: 5000,
    }),
    createAnnualData('00006', '香港电灯', '港股', 2023, {
      totalAssets: 15656000, totalLiabilities: 8056000, totalEquity: 7600000,
      currentAssets: 2956000, currentLiabilities: 2956000, nonCurrentAssets: 12700000, nonCurrentLiabilities: 5100000,
      inventory: 0, accountsReceivable: 645600,
      revenue: 1285000, grossProfit: 705000, netProfit: 335000, operatingProfit: 440000,
      eps: 2.94, grossMargin: 54.9, netMargin: 26.1,
      operatingCashFlow: 705000, investingCashFlow: -440000, financingCashFlow: -260000, netCashFlow: 5000,
    }),
    createAnnualData('00006', '香港电灯', '港股', 2024, {
      totalAssets: 16056000, totalLiabilities: 8256000, totalEquity: 7800000,
      currentAssets: 3056000, currentLiabilities: 3056000, nonCurrentAssets: 13000000, nonCurrentLiabilities: 5200000,
      inventory: 0, accountsReceivable: 665600,
      revenue: 1315000, grossProfit: 725000, netProfit: 345000, operatingProfit: 455000,
      eps: 3.03, grossMargin: 55.2, netMargin: 26.2,
      operatingCashFlow: 725000, investingCashFlow: -455000, financingCashFlow: -265000, netCashFlow: 5000,
    }),
  ],
  '港股_00700': [
    createAnnualData('00700', '腾讯控股', '港股', 2022, {
      totalAssets: 14256000, totalLiabilities: 6256000, totalEquity: 8000000,
      currentAssets: 9856000, currentLiabilities: 4256000, nonCurrentAssets: 4400000, nonCurrentLiabilities: 2000000,
      inventory: 0, accountsReceivable: 1856000,
      revenue: 5556000, grossProfit: 3256000, netProfit: 1856000, operatingProfit: 2456000,
      eps: 19.36, grossMargin: 58.6, netMargin: 33.4,
      operatingCashFlow: 2856000, investingCashFlow: -2256000, financingCashFlow: -585000, netCashFlow: 15000,
    }),
    createAnnualData('00700', '腾讯控股', '港股', 2023, {
      totalAssets: 15125000, totalLiabilities: 6656000, totalEquity: 8469000,
      currentAssets: 10325000, currentLiabilities: 4485600, nonCurrentAssets: 4800000, nonCurrentLiabilities: 2170400,
      inventory: 0, accountsReceivable: 1985000,
      revenue: 5825000, grossProfit: 3425000, netProfit: 1985000, operatingProfit: 2625000,
      eps: 20.72, grossMargin: 58.8, netMargin: 34.1,
      operatingCashFlow: 3085000, investingCashFlow: -2425000, financingCashFlow: -655000, netCashFlow: 5000,
    }),
    createAnnualData('00700', '腾讯控股', '港股', 2024, {
      totalAssets: 16000000, totalLiabilities: 7056000, totalEquity: 8944000,
      currentAssets: 10800000, currentLiabilities: 4715600, nonCurrentAssets: 5200000, nonCurrentLiabilities: 2340400,
      inventory: 0, accountsReceivable: 2115000,
      revenue: 6095000, grossProfit: 3595000, netProfit: 2115000, operatingProfit: 2795000,
      eps: 22.08, grossMargin: 59.0, netMargin: 34.7,
      operatingCashFlow: 3315000, investingCashFlow: -2595000, financingCashFlow: -715000, netCashFlow: 5000,
    }),
  ],
  '港股_00883': [
    createAnnualData('00883', '中国海洋石油', '港股', 2022, {
      totalAssets: 18560000, totalLiabilities: 8256000, totalEquity: 10304000,
      currentAssets: 6856000, currentLiabilities: 4856000, nonCurrentAssets: 11704000, nonCurrentLiabilities: 3400000,
      inventory: 525600, accountsReceivable: 825600,
      revenue: 4856000, grossProfit: 2256000, netProfit: 1485000, operatingProfit: 1856000,
      eps: 1.85, grossMargin: 46.5, netMargin: 30.6,
      operatingCashFlow: 2856000, investingCashFlow: -2256000, financingCashFlow: -585000, netCashFlow: 15000,
    }),
    createAnnualData('00883', '中国海洋石油', '港股', 2023, {
      totalAssets: 19256000, totalLiabilities: 8556000, totalEquity: 10700000,
      currentAssets: 7125600, currentLiabilities: 5025600, nonCurrentAssets: 12130400, nonCurrentLiabilities: 3530400,
      inventory: 545600, accountsReceivable: 855600,
      revenue: 4625000, grossProfit: 2025000, netProfit: 1325000, operatingProfit: 1685000,
      eps: 1.65, grossMargin: 43.8, netMargin: 28.7,
      operatingCashFlow: 2625000, investingCashFlow: -2125000, financingCashFlow: -485000, netCashFlow: 15000,
    }),
    createAnnualData('00883', '中国海洋石油', '港股', 2024, {
      totalAssets: 19956000, totalLiabilities: 8856000, totalEquity: 11100000,
      currentAssets: 7395600, currentLiabilities: 5195600, nonCurrentAssets: 12560400, nonCurrentLiabilities: 3660400,
      inventory: 565600, accountsReceivable: 885600,
      revenue: 4715000, grossProfit: 2115000, netProfit: 1385000, operatingProfit: 1745000,
      eps: 1.72, grossMargin: 44.9, netMargin: 29.4,
      operatingCashFlow: 2715000, investingCashFlow: -2195000, financingCashFlow: -505000, netCashFlow: 15000,
    }),
  ],
  '港股_01398': [
    createAnnualData('01398', '工商银行', '港股', 2022, {
      totalAssets: 358560000, totalLiabilities: 335230000, totalEquity: 23330000,
      currentAssets: 125600000, currentLiabilities: 315600000, nonCurrentAssets: 232960000, nonCurrentLiabilities: 19630000,
      inventory: 0, accountsReceivable: 3560000,
      revenue: 9856000, grossProfit: 5856000, netProfit: 3856000, operatingProfit: 5256000,
      eps: 0.96, grossMargin: 59.4, netMargin: 39.1,
      operatingCashFlow: 8560000, investingCashFlow: -6856000, financingCashFlow: -1690000, netCashFlow: 14000,
    }),
    createAnnualData('01398', '工商银行', '港股', 2023, {
      totalAssets: 372560000, totalLiabilities: 348230000, totalEquity: 24330000,
      currentAssets: 130600000, currentLiabilities: 327600000, nonCurrentAssets: 241960000, nonCurrentLiabilities: 20630000,
      inventory: 0, accountsReceivable: 3725000,
      revenue: 10256000, grossProfit: 6125000, netProfit: 4056000, operatingProfit: 5525000,
      eps: 1.01, grossMargin: 59.7, netMargin: 39.5,
      operatingCashFlow: 9256000, investingCashFlow: -7256000, financingCashFlow: -1985000, netCashFlow: 15000,
    }),
    createAnnualData('01398', '工商银行', '港股', 2024, {
      totalAssets: 386560000, totalLiabilities: 361230000, totalEquity: 25330000,
      currentAssets: 135600000, currentLiabilities: 339600000, nonCurrentAssets: 250960000, nonCurrentLiabilities: 21630000,
      inventory: 0, accountsReceivable: 3885000,
      revenue: 10656000, grossProfit: 6395000, netProfit: 4256000, operatingProfit: 5795000,
      eps: 1.06, grossMargin: 60.0, netMargin: 39.9,
      operatingCashFlow: 9956000, investingCashFlow: -7656000, financingCashFlow: -2285000, netCashFlow: 15000,
    }),
  ],
};

export const newsData: News[] = [
  { id: '1', stockCode: '600519', stockName: '贵州茅台', title: '贵州茅台发布2024年业绩报告，净利润同比增长14.7%', content: '贵州茅台今日发布2024年度业绩报告，实现营业收入1726.8亿元，同比增长11.0%；净利润855.7亿元，同比增长14.7%。每股收益68.17元，同比增长14.7%。', date: '2025-03-20', type: 'announcement' },
  { id: '2', stockCode: '600519', stockName: '贵州茅台', title: '茅台集团拟增持股份，彰显长期发展信心', content: '茅台集团宣布拟通过二级市场增持公司股份，增持金额不低于50亿元，彰显对公司长期发展的信心。', date: '2025-03-18', type: 'news' },
  { id: '3', stockCode: '00700', stockName: '腾讯控股', title: '腾讯2024年净利润突破2100亿，游戏业务增长强劲', content: '腾讯控股发布2024年度财报，全年净利润2115亿元，同比增长6.6%。游戏业务表现强劲，国际市场收入同比增长18%。', date: '2025-03-21', type: 'announcement' },
  { id: '4', stockCode: '00700', stockName: '腾讯控股', title: '腾讯投资AI初创公司，布局人工智能领域', content: '腾讯宣布战略投资多家AI初创公司，累计投资金额超过10亿美元，加速人工智能技术布局。', date: '2025-03-19', type: 'news' },
  { id: '5', stockCode: '300750', stockName: '宁德时代', title: '宁德时代发布新型钠离子电池，能量密度提升20%', content: '宁德时代今日发布新一代钠离子电池，能量密度提升至200Wh/kg，较上一代提升约20%，成本降低15%。', date: '2025-03-22', type: 'news' },
  { id: '6', stockCode: '002594', stockName: '比亚迪', title: '比亚迪2024年新能源汽车销量突破300万辆', content: '比亚迪发布产销快报，2024年新能源汽车销量达302万辆，同比增长35%，继续领跑全球新能源汽车市场。', date: '2025-03-17', type: 'announcement' },
  { id: '7', stockCode: '600036', stockName: '招商银行', title: '招商银行数字化转型成效显著，零售业务占比超60%', content: '招商银行披露数字化转型成果，零售业务收入占比超过60%，手机银行月活用户突破1.8亿。', date: '2025-03-16', type: 'news' },
  { id: '8', stockCode: '601318', stockName: '中国平安', title: '中国平安启动新一轮战略升级，聚焦保险+科技', content: '中国平安宣布启动新一轮战略升级，将"保险+科技"作为核心战略，计划未来五年投入500亿元用于科技创新。', date: '2025-03-15', type: 'news' },
];

export const calendarEvents: CalendarEvent[] = [
  { id: '1', date: '2025-04-10', stockCode: '600519', stockName: '贵州茅台', eventType: 'earnings', title: '2025年第一季度财报发布' },
  { id: '2', date: '2025-04-12', stockCode: '00700', stockName: '腾讯控股', eventType: 'earnings', title: '2025年第一季度财报发布' },
  { id: '3', date: '2025-04-15', stockCode: '300750', stockName: '宁德时代', eventType: 'earnings', title: '2025年第一季度财报发布' },
  { id: '4', date: '2025-04-20', stockCode: '600036', stockName: '招商银行', eventType: 'earnings', title: '2025年第一季度财报发布' },
  { id: '5', date: '2025-05-20', stockCode: '600519', stockName: '贵州茅台', eventType: 'dividend', title: '2024年度分红派息' },
  { id: '6', date: '2025-06-15', stockCode: '00700', stockName: '腾讯控股', eventType: 'meeting', title: '年度股东大会' },
  { id: '7', date: '2025-04-25', stockCode: '002594', stockName: '比亚迪', eventType: 'earnings', title: '2025年第一季度财报发布' },
  { id: '8', date: '2025-05-10', stockCode: '601318', stockName: '中国平安', eventType: 'meeting', title: '年度股东大会' },
];

const generateKLineData = (basePrice: number, days: number): KLineData[] => {
  const data: KLineData[] = [];
  let price = basePrice;
  const now = Math.floor(Date.now() / 1000);
  const daySeconds = 24 * 60 * 60;

  for (let i = days - 1; i >= 0; i--) {
    const time = now - i * daySeconds;
    const change = (Math.random() - 0.5) * basePrice * 0.04;
    const open = price;
    const close = price + change;
    const high = Math.max(open, close) + Math.random() * basePrice * 0.02;
    const low = Math.min(open, close) - Math.random() * basePrice * 0.02;
    const volume = Math.floor(Math.random() * 10000000 + 5000000);

    data.push({ time, open, high, low, close, volume });
    price = close;
  }

  return data;
};

export const klineDataMap: Record<string, KLineData[]> = {
  'a股_600519': generateKLineData(1685, 120),
  'a股_000858': generateKLineData(142.8, 120),
  'a股_601318': generateKLineData(48.5, 120),
  'a股_600036': generateKLineData(35.2, 120),
  'a股_000001': generateKLineData(12.8, 120),
  'a股_601899': generateKLineData(15.6, 120),
  'a股_300750': generateKLineData(218.5, 120),
  'a股_002594': generateKLineData(258, 120),
  '港股_00001': generateKLineData(58.2, 120),
  '港股_00002': generateKLineData(72.5, 120),
  '港股_00003': generateKLineData(12.8, 120),
  '港股_00005': generateKLineData(68.5, 120),
  '港股_00006': generateKLineData(55.2, 120),
  '港股_00700': generateKLineData(428, 120),
  '港股_00883': generateKLineData(15.8, 120),
  '港股_01398': generateKLineData(5.6, 120),
};

export const searchStocks = (keyword: string = '', market?: MarketType, industry?: string): Stock[] => {
  const allStocks = [...aStocks, ...hongKongStocks];
  return allStocks.filter(stock => {
    const matchKeyword = !keyword || stock.name.includes(keyword) || stock.code.includes(keyword);
    const matchMarket = !market || stock.market === market;
    const matchIndustry = !industry || stock.industry === industry;
    return matchKeyword && matchMarket && matchIndustry;
  });
};

export const getHotStocks = (): Stock[] => {
  return [...aStocks, ...hongKongStocks].slice(0, 6);
};

export const getFinancialData = (stockCode: string, market: MarketType, periodType?: PeriodType): FinancialData[] => {
  const key = `${market}_${stockCode}`;
  let data = financialDataMap[key] || [];
  if (periodType) {
    data = data.filter(d => d.periodType === periodType);
  }
  return data.sort((a, b) => new Date(b.reportDate).getTime() - new Date(a.reportDate).getTime());
};

export const getLatestFinancialData = (stockCode: string, market: MarketType): FinancialData | undefined => {
  const data = getFinancialData(stockCode, market, 'annual');
  return data[0];
};

export const getNews = (stockCode?: string): News[] => {
  if (!stockCode) return newsData;
  return newsData.filter(news => news.stockCode === stockCode);
};

export const getCalendarEvents = (month?: string): CalendarEvent[] => {
  let events = calendarEvents;
  if (month) {
    events = events.filter(event => event.date.startsWith(month));
  }
  return events.sort((a, b) => a.date.localeCompare(b.date));
};

export const getKLineData = (stockCode: string, market: MarketType): KLineData[] => {
  const key = `${market}_${stockCode}`;
  return klineDataMap[key] || [];
};

export const getIndustries = (): string[] => {
  const allStocks = [...aStocks, ...hongKongStocks];
  return [...new Set(allStocks.map(stock => stock.industry))];
};

export const getIndustryData = (industry: string): IndustryData => {
  const stocks = [...aStocks, ...hongKongStocks].filter(s => s.industry === industry);
  const financials = stocks.map(s => getLatestFinancialData(s.code, s.market)).filter(Boolean) as FinancialData[];

  let totalPe = 0;
  let totalPb = 0;
  let totalRoe = 0;
  let totalDebtRatio = 0;
  let totalRevenue = 0;
  let totalNetProfit = 0;

  financials.forEach(f => {
    const stock = stocks.find(st => st.code === f.stockCode);
    
    if (f.incomeStatement.netProfit > 0 && stock && f.incomeStatement.eps > 0) {
      totalPe += stock.price / f.incomeStatement.eps;
    }
    
    if (f.balanceSheet.totalEquity > 0 && stock) {
      const shares = 22;
      totalPb += stock.price / (f.balanceSheet.totalEquity / shares);
      totalRoe += (f.incomeStatement.netProfit / f.balanceSheet.totalEquity) * 100;
    }
    
    if (f.balanceSheet.totalAssets > 0) {
      totalDebtRatio += (f.balanceSheet.totalLiabilities / f.balanceSheet.totalAssets) * 100;
    }
    
    totalRevenue += f.incomeStatement.revenue;
    totalNetProfit += f.incomeStatement.netProfit;
  });

  const count = financials.length || 1;
  const averages = {
    pe: totalPe / count,
    pb: totalPb / count,
    roe: totalRoe / count,
    debtRatio: totalDebtRatio / count,
    revenue: totalRevenue / count,
    netProfit: totalNetProfit / count,
  };

  return { name: industry, stocks, averages };
};