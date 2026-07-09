import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import etfsData from '@/data/etfs.json';
import detail510050 from '@/data/etf-details/510050.json';
import detail510300 from '@/data/etf-details/510300.json';
import detail510500 from '@/data/etf-details/510500.json';
import detail159919 from '@/data/etf-details/159919.json';
import detail512880 from '@/data/etf-details/512880.json';
import detail513100 from '@/data/etf-details/513100.json';
import type { DataSource, DataSourceType } from './datasources';
import { LocalCache } from './datasources';
import { SinaDataSource } from './sina';
import { EastMoneyDataSource } from './eastmoney';

const detailMap: Record<string, ETFDetailResponse> = {
  '510050': detail510050,
  '510300': detail510300,
  '510500': detail510500,
  '159919': detail159919,
  '512880': detail512880,
  '513100': detail513100,
};

class MockDataSource implements DataSource {
  type: 'mock' = 'mock';
  name = '模拟数据';

  async getETFList(type?: ETFCategory, keyword?: string): Promise<{ etfs: ETF[]; total: number }> {
    const cacheKey = `mock_list_${type || 'all'}_${keyword || ''}`;
    const cached = LocalCache.get<{ etfs: ETF[]; total: number }>(cacheKey);
    if (cached) {
      return cached;
    }

    await new Promise((resolve) => setTimeout(resolve, 300));

    let filtered = etfsData.etfs as ETF[];

    if (type) {
      filtered = filtered.filter((etf) => etf.category === type);
    }

    if (keyword) {
      const lowerKeyword = keyword.toLowerCase();
      filtered = filtered.filter(
        (etf) =>
          etf.code.toLowerCase().includes(lowerKeyword) ||
          etf.name.toLowerCase().includes(lowerKeyword) ||
          etf.fullName.toLowerCase().includes(lowerKeyword)
      );
    }

    const result = { etfs: filtered, total: filtered.length };
    LocalCache.set(cacheKey, result, 86400000);
    return result;
  }

  async getETFDetail(code: string): Promise<ETFDetailResponse> {
    const cacheKey = `mock_detail_${code}`;
    const cached = LocalCache.get<ETFDetailResponse>(cacheKey);
    if (cached) {
      return cached;
    }

    await new Promise((resolve) => setTimeout(resolve, 300));

    const detail = detailMap[code];
    if (detail) {
      LocalCache.set(cacheKey, detail, 86400000);
      return detail;
    }

    throw new Error(`ETF ${code} not found`);
  }
}

const dataSources: Record<DataSourceType, DataSource> = {
  mock: new MockDataSource(),
  sina: new SinaDataSource(),
  eastmoney: new EastMoneyDataSource(),
};

let currentDataSource: DataSourceType = 'mock';

export const setDataSource = (type: DataSourceType): void => {
  currentDataSource = type;
  LocalCache.set('current_datasource', type, 86400000);
};

export const getDataSource = (): DataSourceType => {
  const cached = LocalCache.get<DataSourceType>('current_datasource');
  return cached || currentDataSource;
};

export const getAvailableDataSources = (): Array<{ type: DataSourceType; name: string }> => {
  return Object.entries(dataSources).map(([type, source]) => ({
    type: type as DataSourceType,
    name: source.name,
  }));
};

export const getETFList = async (
  type?: ETFCategory,
  keyword?: string
): Promise<{ etfs: ETF[]; total: number }> => {
  return dataSources[currentDataSource].getETFList(type, keyword);
};

export const getETFDetail = async (code: string): Promise<ETFDetailResponse> => {
  return dataSources[currentDataSource].getETFDetail(code);
};

export const clearCache = (): void => {
  LocalCache.clear();
};

export const getCacheInfo = () => {
  return LocalCache.getCacheInfo();
};