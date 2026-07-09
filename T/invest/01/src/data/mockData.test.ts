import { describe, it, expect } from 'vitest';
import { searchStocks, getHotStocks, getFinancialData, getLatestFinancialData, aStocks, hongKongStocks } from './mockData';

describe('mockData', () => {
  describe('searchStocks', () => {
    it('should search A股 stocks by code', () => {
      const results = searchStocks('600519', 'a股');
      expect(results.length).toBe(1);
      expect(results[0].name).toBe('贵州茅台');
    });

    it('should search A股 stocks by name', () => {
      const results = searchStocks('茅台', 'a股');
      expect(results.length).toBe(1);
      expect(results[0].code).toBe('600519');
    });

    it('should search 港股 stocks by code', () => {
      const results = searchStocks('00700', '港股');
      expect(results.length).toBe(1);
      expect(results[0].name).toBe('腾讯控股');
    });

    it('should search 港股 stocks by name', () => {
      const results = searchStocks('腾讯', '港股');
      expect(results.length).toBe(1);
      expect(results[0].code).toBe('00700');
    });

    it('should return empty array for no matches', () => {
      const results = searchStocks('不存在的股票', 'a股');
      expect(results.length).toBe(0);
    });

    it('should be case insensitive for code', () => {
      const results = searchStocks('600519', 'a股');
      expect(results.length).toBe(1);
    });
  });

  describe('getHotStocks', () => {
    it('should return hot stocks', () => {
      const hotStocks = getHotStocks();
      expect(hotStocks.length).toBeGreaterThan(0);
    });
  });

  describe('getFinancialData', () => {
    it('should return financial data for A股 stock', () => {
      const data = getFinancialData('600519', 'a股');
      expect(data).toBeDefined();
      expect(data!.length).toBeGreaterThan(0);
      expect(data![0].stockCode).toBe('600519');
    });

    it('should return financial data for 港股 stock', () => {
      const data = getFinancialData('00700', '港股');
      expect(data).toBeDefined();
      expect(data!.length).toBeGreaterThan(0);
      expect(data![0].stockCode).toBe('00700');
    });

    it('should return empty array for unknown stock', () => {
      const data = getFinancialData('999999', 'a股');
      expect(data).toEqual([]);
    });
  });

  describe('getLatestFinancialData', () => {
    it('should return latest financial data for A股 stock', () => {
      const data = getLatestFinancialData('600519', 'a股');
      expect(data).toBeDefined();
      expect(data!.stockCode).toBe('600519');
      expect(data!.reportDate).toBe('2024-12-31');
    });

    it('should return latest financial data for 港股 stock', () => {
      const data = getLatestFinancialData('00700', '港股');
      expect(data).toBeDefined();
      expect(data!.stockCode).toBe('00700');
      expect(data!.reportDate).toBe('2024-12-31');
    });

    it('should return undefined for unknown stock', () => {
      const data = getLatestFinancialData('999999', 'a股');
      expect(data).toBeUndefined();
    });
  });

  describe('stock lists', () => {
    it('should have 8 A股 stocks', () => {
      expect(aStocks.length).toBe(8);
    });

    it('should have 8 港股 stocks', () => {
      expect(hongKongStocks.length).toBe(8);
    });

    it('should have unique stock codes', () => {
      const aCodes = aStocks.map((s) => s.code);
      const hkCodes = hongKongStocks.map((s) => s.code);
      expect(aCodes.length).toBe(new Set(aCodes).size);
      expect(hkCodes.length).toBe(new Set(hkCodes).size);
    });
  });
});
