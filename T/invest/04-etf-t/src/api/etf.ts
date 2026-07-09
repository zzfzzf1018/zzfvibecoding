import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import type { DataSource, DataSourceType } from './datasources';
import { LocalCache } from './datasources';
import { SinaDataSource } from './sina';
import { EastMoneyDataSource } from './eastmoney';
import { TencentDataSource } from './tencent';
import { AkShareDataSource } from './akshare';

const dataSources: Record<DataSourceType, DataSource> = {
  sina: new SinaDataSource(),
  eastmoney: new EastMoneyDataSource(),
  tencent: new TencentDataSource(),
  akshare: new AkShareDataSource(),
};

let currentDataSource: DataSourceType = 'eastmoney';

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

export const clearCacheByPrefix = (prefix: string): void => {
  LocalCache.clearByPrefix(prefix);
};

export const getCacheInfo = () => {
  return LocalCache.getCacheInfo();
};