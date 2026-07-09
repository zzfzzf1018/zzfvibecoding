import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { useCompareStore } from './useCompareStore';
import { Stock, FinancialData } from '../types';

describe('useCompareStore', () => {
  const testStock: Stock = {
    code: '600519',
    name: '贵州茅台',
    market: 'a股',
    industry: '白酒',
    price: 1685.00,
    change: 25.50,
    changePercent: 1.54,
  };

  const testFinancial: FinancialData = {
    stockCode: '600519',
    stockName: '贵州茅台',
    market: 'a股',
    reportDate: '2024-12-31',
    periodType: 'annual',
    balanceSheet: {
      totalAssets: 3405600,
      totalLiabilities: 685200,
      totalEquity: 2720400,
      currentAssets: 2865400,
      currentLiabilities: 523800,
      nonCurrentAssets: 540200,
      nonCurrentLiabilities: 161400,
      inventory: 356000,
      accountsReceivable: 12500,
    },
    incomeStatement: {
      revenue: 1726800,
      grossProfit: 1320500,
      netProfit: 855700,
      operatingProfit: 1086500,
      eps: 68.17,
      grossMargin: 76.5,
      netMargin: 49.6,
    },
    cashFlow: {
      operatingCashFlow: 1002500,
      investingCashFlow: -156800,
      financingCashFlow: -895600,
      netCashFlow: -49900,
    },
  };

  beforeEach(() => {
    useCompareStore.getState().clearAll();
  });

  afterEach(() => {
    useCompareStore.getState().clearAll();
  });

  it('should initialize with empty items', () => {
    expect(useCompareStore.getState().items).toEqual([]);
  });

  it('should add an item to compare list', () => {
    useCompareStore.getState().addItem(testStock, testFinancial);
    const state = useCompareStore.getState();
    expect(state.items.length).toBe(1);
    expect(state.items[0].stock.code).toBe('600519');
    expect(state.items[0].stock.name).toBe('贵州茅台');
  });

  it('should not add duplicate items', () => {
    useCompareStore.getState().addItem(testStock, testFinancial);
    useCompareStore.getState().addItem(testStock, testFinancial);
    const state = useCompareStore.getState();
    expect(state.items.length).toBe(1);
  });

  it('should remove an item from compare list', () => {
    useCompareStore.getState().addItem(testStock, testFinancial);
    let state = useCompareStore.getState();
    expect(state.items.length).toBe(1);
    useCompareStore.getState().removeItem('600519');
    state = useCompareStore.getState();
    expect(state.items.length).toBe(0);
  });

  it('should clear all items', () => {
    useCompareStore.getState().addItem(testStock, testFinancial);
    const anotherStock: Stock = {
      code: '00700',
      name: '腾讯控股',
      market: '港股',
      industry: '互联网',
      price: 428.00,
      change: 12.50,
      changePercent: 3.00,
    };
    const anotherFinancial: FinancialData = {
      stockCode: '00700',
      stockName: '腾讯控股',
      market: '港股',
      reportDate: '2024-12-31',
      periodType: 'annual',
      balanceSheet: {
        totalAssets: 1685000,
        totalLiabilities: 525600,
        totalEquity: 1159400,
        currentAssets: 1085600,
        currentLiabilities: 385600,
        nonCurrentAssets: 600000,
        nonCurrentLiabilities: 140000,
        inventory: 0,
        accountsReceivable: 85600,
      },
      incomeStatement: {
        revenue: 685000,
        grossProfit: 425600,
        netProfit: 215000,
        operatingProfit: 285600,
        eps: 14.88,
        grossMargin: 62.1,
        netMargin: 31.4,
      },
      cashFlow: {
        operatingCashFlow: 352000,
        investingCashFlow: -185600,
        financingCashFlow: -156800,
        netCashFlow: 9600,
      },
    };
    useCompareStore.getState().addItem(anotherStock, anotherFinancial);
    let state = useCompareStore.getState();
    expect(state.items.length).toBe(2);
    useCompareStore.getState().clearAll();
    state = useCompareStore.getState();
    expect(state.items.length).toBe(0);
  });

  it('should check if an item is in compare list', () => {
    let state = useCompareStore.getState();
    expect(state.isInCompare('600519')).toBe(false);
    useCompareStore.getState().addItem(testStock, testFinancial);
    state = useCompareStore.getState();
    expect(state.isInCompare('600519')).toBe(true);
    expect(state.isInCompare('00700')).toBe(false);
  });
});
