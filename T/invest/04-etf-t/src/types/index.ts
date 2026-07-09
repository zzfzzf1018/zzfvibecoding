export type ETFCategory = 'broad' | 'industry' | 'theme' | 'bond' | 'cross-border';

export interface ETF {
  code: string;
  name: string;
  fullName: string;
  fundCompany: string;
  establishDate: string;
  scale: number;
  trackingIndex: string;
  category: ETFCategory;
  categoryName: string;
  nav: number;
  change: number;
  changePercent: number;
  oneYearReturn: number;
  pe: number;
  pb: number;
}

export interface ETFBasicInfo {
  code: string;
  name: string;
  fullName: string;
  fundCompany: string;
  establishDate: string;
  latestReportDate: string;
  scale: number;
  scaleDate: string;
  trackingIndex: string;
  indexCode: string;
  creationDate: string;
  managementFeeRate: number;
  custodianFeeRate: number;
  salesServiceFeeRate: number;
}

export interface ETFHolding {
  rank: number;
  stockCode: string;
  stockName: string;
  weight: number;
  marketValue: number;
  changePercent: number;
}

export interface ETFFees {
  managementFee: number;
  custodianFee: number;
  salesServiceFee: number;
  subscriptionFee: number;
  redemptionFee: number;
  managementFeeRate: string;
  custodianFeeRate: string;
  salesServiceFeeRate: string;
}

export interface ETFDividend {
  dividendDate: string;
  exDividendDate: string;
  dividendAmount: number;
  dividendType: string;
  recordDate: string;
}

export interface ETFValuation {
  pe: number;
  pePercentile: number;
  pb: number;
  pbPercentile: number;
  ps: number;
  psPercentile: number;
  earningsYield: number;
  dividendYield: number;
}

export interface ETFQuantile {
  date: string;
  pe: number;
  pePercentile: number;
  pb: number;
  pbPercentile: number;
}

export interface ETFDetailResponse {
  basic: ETFBasicInfo;
  holdings: ETFHolding[];
  fees: ETFFees;
  dividends: ETFDividend[];
  valuation: ETFValuation;
  quantiles: ETFQuantile[];
}

export interface ETFListResponse {
  etfs: ETF[];
  total: number;
}

export const categoryMap: Record<ETFCategory, string> = {
  'broad': '宽基',
  'industry': '行业',
  'theme': '主题',
  'bond': '债券',
  'cross-border': '跨境',
};