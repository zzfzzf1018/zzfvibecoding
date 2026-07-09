export type MarketType = 'a股' | '港股';
export type PeriodType = 'annual' | 'quarter';
export type NewsType = 'news' | 'announcement';
export type EventType = 'earnings' | 'dividend' | 'meeting';

export interface Stock {
  code: string;
  name: string;
  market: MarketType;
  industry: string;
  price: number;
  change: number;
  changePercent: number;
}

export interface BalanceSheet {
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  currentAssets: number;
  currentLiabilities: number;
  nonCurrentAssets: number;
  nonCurrentLiabilities: number;
  inventory: number;
  accountsReceivable: number;
}

export interface IncomeStatement {
  revenue: number;
  grossProfit: number;
  netProfit: number;
  operatingProfit: number;
  eps: number;
  grossMargin: number;
  netMargin: number;
}

export interface CashFlow {
  operatingCashFlow: number;
  investingCashFlow: number;
  financingCashFlow: number;
  netCashFlow: number;
}

export interface FinancialRatios {
  pe: number;
  pb: number;
  ps: number;
  roe: number;
  roa: number;
  debtRatio: number;
  currentRatio: number;
  quickRatio: number;
  arTurnover: number;
  inventoryTurnover: number;
}

export interface FinancialData {
  stockCode: string;
  stockName: string;
  market: MarketType;
  reportDate: string;
  periodType: PeriodType;
  balanceSheet: BalanceSheet;
  incomeStatement: IncomeStatement;
  cashFlow: CashFlow;
}

export interface CompareItem {
  stock: Stock;
  financial: FinancialData;
}

export interface News {
  id: string;
  stockCode: string;
  stockName: string;
  title: string;
  content: string;
  date: string;
  type: NewsType;
}

export interface CalendarEvent {
  id: string;
  date: string;
  stockCode: string;
  stockName: string;
  eventType: EventType;
  title: string;
}

export interface IndustryData {
  name: string;
  stocks: Stock[];
  averages: {
    pe: number;
    pb: number;
    roe: number;
    debtRatio: number;
    revenue: number;
    netProfit: number;
  };
}

export interface KLineData {
  time: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface FinancialHealthScore {
  score: number;
  level: 'excellent' | 'good' | 'fair' | 'poor';
  warnings: string[];
  details: {
    indicator: string;
    score: number;
    status: 'pass' | 'warning' | 'danger';
  }[];
}
