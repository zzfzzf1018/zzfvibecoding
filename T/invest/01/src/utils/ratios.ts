import { FinancialData, Stock, FinancialRatios } from '../types';

export const calculateRatios = (financial: FinancialData, stock?: Stock): FinancialRatios => {
  const { balanceSheet, incomeStatement } = financial;
  
  const pe = stock && incomeStatement.eps > 0 
    ? stock.price / incomeStatement.eps 
    : 0;

  const shares = stock ? (balanceSheet.totalEquity / stock.price) * pe : 22;
  
  const pb = stock && balanceSheet.totalEquity > 0 
    ? stock.price / (balanceSheet.totalEquity / shares) 
    : 0;

  const ps = stock && incomeStatement.revenue > 0 
    ? (stock.price * shares) / incomeStatement.revenue 
    : 0;

  const roe = balanceSheet.totalEquity > 0 
    ? (incomeStatement.netProfit / balanceSheet.totalEquity) * 100 
    : 0;

  const roa = balanceSheet.totalAssets > 0 
    ? (incomeStatement.netProfit / balanceSheet.totalAssets) * 100 
    : 0;

  const debtRatio = balanceSheet.totalAssets > 0 
    ? (balanceSheet.totalLiabilities / balanceSheet.totalAssets) * 100 
    : 0;

  const currentRatio = balanceSheet.currentLiabilities > 0 
    ? balanceSheet.currentAssets / balanceSheet.currentLiabilities 
    : 0;

  const quickRatio = balanceSheet.currentLiabilities > 0 
    ? (balanceSheet.currentAssets - balanceSheet.inventory) / balanceSheet.currentLiabilities 
    : 0;

  const arTurnover = balanceSheet.accountsReceivable > 0 
    ? incomeStatement.revenue / balanceSheet.accountsReceivable 
    : 0;

  const inventoryTurnover = balanceSheet.inventory > 0 
    ? incomeStatement.revenue / balanceSheet.inventory 
    : 0;

  return {
    pe: Math.round(pe * 100) / 100,
    pb: Math.round(pb * 100) / 100,
    ps: Math.round(ps * 100) / 100,
    roe: Math.round(roe * 100) / 100,
    roa: Math.round(roa * 100) / 100,
    debtRatio: Math.round(debtRatio * 100) / 100,
    currentRatio: Math.round(currentRatio * 100) / 100,
    quickRatio: Math.round(quickRatio * 100) / 100,
    arTurnover: Math.round(arTurnover * 100) / 100,
    inventoryTurnover: Math.round(inventoryTurnover * 100) / 100,
  };
};

export const formatNumber = (num: number, decimals: number = 2): string => {
  if (Math.abs(num) >= 100000000) {
    return (num / 100000000).toFixed(decimals) + '亿';
  }
  if (Math.abs(num) >= 10000) {
    return (num / 10000).toFixed(decimals) + '万';
  }
  return num.toFixed(decimals);
};

export const calculateGrowth = (current: number, previous: number): number => {
  if (previous === 0) return current > 0 ? 100 : 0;
  return Math.round(((current - previous) / previous) * 10000) / 100;
};
