import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';

export type DataSourceType = 'mock' | 'sina' | 'eastmoney' | 'tencent';

export interface DataSource {
  type: DataSourceType;
  name: string;
  getETFList: (type?: ETFCategory, keyword?: string) => Promise<{ etfs: ETF[]; total: number }>;
  getETFDetail: (code: string) => Promise<ETFDetailResponse>;
}

export interface CacheEntry<T> {
  data: T;
  timestamp: number;
  ttl: number;
}

export class LocalCache {
  private static readonly PREFIX = 'etf_cache_';

  static get<T>(key: string): T | null {
    try {
      const item = localStorage.getItem(this.PREFIX + key);
      if (!item) return null;
      const entry: CacheEntry<T> = JSON.parse(item);
      if (Date.now() - entry.timestamp > entry.ttl) {
        localStorage.removeItem(this.PREFIX + key);
        return null;
      }
      return entry.data;
    } catch {
      return null;
    }
  }

  static set<T>(key: string, data: T, ttl: number = 3600000): void {
    try {
      const entry: CacheEntry<T> = {
        data,
        timestamp: Date.now(),
        ttl,
      };
      localStorage.setItem(this.PREFIX + key, JSON.stringify(entry));
    } catch {
      // localStorage not available
    }
  }

  static remove(key: string): void {
    localStorage.removeItem(this.PREFIX + key);
  }

  static clear(): void {
    Object.keys(localStorage).forEach((key) => {
      if (key.startsWith(this.PREFIX)) {
        localStorage.removeItem(key);
      }
    });
  }

  static clearByPrefix(prefix: string): void {
    Object.keys(localStorage).forEach((key) => {
      if (key.startsWith(this.PREFIX + prefix)) {
        localStorage.removeItem(key);
      }
    });
  }

  static getCacheInfo(): { keys: string[]; sizes: Record<string, number> } {
    const keys: string[] = [];
    const sizes: Record<string, number> = {};
    Object.keys(localStorage).forEach((key) => {
      if (key.startsWith(this.PREFIX)) {
        keys.push(key.replace(this.PREFIX, ''));
        sizes[key] = localStorage.getItem(key)?.length || 0;
      }
    });
    return { keys, sizes };
  }
}