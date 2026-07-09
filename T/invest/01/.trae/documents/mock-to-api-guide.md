# Mock数据替换指南

## 一、概述

当前项目使用Mock数据进行开发，在实际部署时需要将Mock数据替换为真实API。本指南详细说明如何进行API接入和数据层改造。

## 二、数据层架构

### 当前架构（Mock模式）

```
组件 → mockData.ts → Mock数据
```

### 目标架构（API模式）

```
组件 → api/ → axios/fetch → 后端API
```

## 三、API接口设计

### 3.1 股票数据接口

| API路径 | 方法 | 功能 | 参数 | 返回值 |
|---------|------|------|------|--------|
| `/api/stocks` | GET | 获取股票列表 | `market`: 'a股' \| '港股', `industry`: string | `Stock[]` |
| `/api/stocks/search` | GET | 搜索股票 | `keyword`: string | `Stock[]` |
| `/api/stocks/:code` | GET | 获取股票详情 | `code`: string, `market`: string | `Stock` |

### 3.2 财务数据接口

| API路径 | 方法 | 功能 | 参数 | 返回值 |
|---------|------|------|------|--------|
| `/api/financial/:code` | GET | 获取财务数据列表 | `code`: string, `market`: string, `periodType`: 'annual' \| 'quarter' | `FinancialData[]` |
| `/api/financial/:code/latest` | GET | 获取最新财务数据 | `code`: string, `market`: string | `FinancialData` |

### 3.3 新闻数据接口

| API路径 | 方法 | 功能 | 参数 | 返回值 |
|---------|------|------|------|--------|
| `/api/news` | GET | 获取新闻列表 | `stockCode`: string, `type`: 'news' \| 'announcement' | `News[]` |
| `/api/news/hot` | GET | 获取热点新闻 | 无 | `News[]` |

### 3.4 日历数据接口

| API路径 | 方法 | 功能 | 参数 | 返回值 |
|---------|------|------|------|--------|
| `/api/calendar` | GET | 获取日历事件 | `date`: string, `stockCode`: string | `CalendarEvent[]` |
| `/api/calendar/upcoming` | GET | 获取近期事件 | `days`: number | `CalendarEvent[]` |

### 3.5 K线数据接口

| API路径 | 方法 | 功能 | 参数 | 返回值 |
|---------|------|------|------|--------|
| `/api/kline/:code` | GET | 获取K线数据 | `code`: string, `market`: string, `period`: 'day' \| 'week' \| 'month' | `KLineData[]` |

### 3.6 行业数据接口

| API路径 | 方法 | 功能 | 参数 | 返回值 |
|---------|------|------|------|--------|
| `/api/industries` | GET | 获取行业列表 | 无 | `string[]` |
| `/api/industries/:name` | GET | 获取行业数据 | `name`: string | `IndustryData` |

## 四、API接入步骤

### 步骤1：创建API层

**创建API目录结构**：

```
src/
├── api/
│   ├── index.ts          # API配置和请求封装
│   ├── stocks.ts         # 股票相关API
│   ├── financial.ts      # 财务数据API
│   ├── news.ts           # 新闻API
│   ├── calendar.ts       # 日历API
│   ├── kline.ts          # K线数据API
│   └── industry.ts       # 行业数据API
```

### 步骤2：封装HTTP请求

**创建API配置文件**：

```typescript
// src/api/index.ts
import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://api.example.com';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

apiClient.interceptors.response.use(
  (response) => response.data,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);

export default apiClient;
```

### 步骤3：实现API模块

**股票API示例**：

```typescript
// src/api/stocks.ts
import apiClient from './index';
import { Stock, MarketType } from '../types';

export const getStocks = async (market?: MarketType, industry?: string): Promise<Stock[]> => {
  const params: Record<string, string> = {};
  if (market) params.market = market;
  if (industry) params.industry = industry;
  
  return apiClient.get('/stocks', { params });
};

export const searchStocks = async (keyword: string): Promise<Stock[]> => {
  return apiClient.get('/stocks/search', { params: { keyword } });
};

export const getStockDetail = async (code: string, market: MarketType): Promise<Stock> => {
  return apiClient.get(`/stocks/${code}`, { params: { market } });
};
```

**财务数据API示例**：

```typescript
// src/api/financial.ts
import apiClient from './index';
import { FinancialData, MarketType, PeriodType } from '../types';

export const getFinancialData = async (
  code: string,
  market: MarketType,
  periodType?: PeriodType
): Promise<FinancialData[]> => {
  const params: Record<string, string> = { market };
  if (periodType) params.periodType = periodType;
  
  return apiClient.get(`/financial/${code}`, { params });
};

export const getLatestFinancialData = async (
  code: string,
  market: MarketType
): Promise<FinancialData> => {
  return apiClient.get(`/financial/${code}/latest`, { params: { market } });
};
```

### 步骤4：创建数据服务层

**创建数据服务层，统一管理数据获取**：

```typescript
// src/services/dataService.ts
import * as stocksApi from '../api/stocks';
import * as financialApi from '../api/financial';
import * as newsApi from '../api/news';
import * as calendarApi from '../api/calendar';
import * as klineApi from '../api/kline';
import * as industryApi from '../api/industry';

import * as mockStocks from '../data/mockData';

const USE_MOCK = process.env.REACT_APP_USE_MOCK === 'true';

export const searchStocks = async (keyword: string) => {
  if (USE_MOCK) {
    return mockStocks.searchStocks(keyword);
  }
  return stocksApi.searchStocks(keyword);
};

export const getFinancialData = async (code: string, market: string, periodType?: string) => {
  if (USE_MOCK) {
    return mockStocks.getFinancialData(code, market);
  }
  return financialApi.getFinancialData(code, market as any, periodType as any);
};

export const getLatestFinancialData = async (code: string, market: string) => {
  if (USE_MOCK) {
    return mockStocks.getLatestFinancialData(code, market);
  }
  return financialApi.getLatestFinancialData(code, market as any);
};

export const getNews = async (stockCode?: string) => {
  if (USE_MOCK) {
    return mockStocks.getNews(stockCode);
  }
  return newsApi.getNews(stockCode);
};

export const getCalendarEvents = async (stockCode?: string) => {
  if (USE_MOCK) {
    return mockStocks.getCalendarEvents(stockCode);
  }
  return calendarApi.getCalendarEvents(stockCode);
};

export const getKLineData = async (stockCode: string) => {
  if (USE_MOCK) {
    return mockStocks.getKLineData(stockCode);
  }
  return klineApi.getKLineData(stockCode);
};

export const getIndustryData = async (industry: string) => {
  if (USE_MOCK) {
    return mockStocks.getIndustryData(industry);
  }
  return industryApi.getIndustryData(industry);
};

export const getHotStocks = () => {
  if (USE_MOCK) {
    return mockStocks.getHotStocks();
  }
  return stocksApi.getStocks();
};
```

### 步骤5：更新组件使用

**组件中使用数据服务**：

```typescript
import { useState, useEffect } from 'react';
import { searchStocks, getLatestFinancialData } from '../services/dataService';

export default function StockSearch() {
  const [stocks, setStocks] = useState<Stock[]>([]);
  const [financial, setFinancial] = useState<FinancialData | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    searchStocks('茅台').then((result) => {
      setStocks(result);
      setLoading(false);
    });
  }, []);

  const handleSelectStock = async (stock: Stock) => {
    setLoading(true);
    const data = await getLatestFinancialData(stock.code, stock.market);
    setFinancial(data);
    setLoading(false);
  };

  return (
    <div>
      {loading && <div>Loading...</div>}
      {stocks.map((stock) => (
        <div key={stock.code} onClick={() => handleSelectStock(stock)}>
          {stock.name}
        </div>
      ))}
    </div>
  );
}
```

## 五、环境配置

### 5.1 添加环境变量

**创建 `.env` 文件**：

```env
# 开发环境
REACT_APP_API_URL=https://api.example.com
REACT_APP_USE_MOCK=true

# 生产环境
# REACT_APP_API_URL=https://api.example.com
# REACT_APP_USE_MOCK=false
```

**创建 `.env.production` 文件**：

```env
REACT_APP_API_URL=https://api.yourdomain.com
REACT_APP_USE_MOCK=false
```

### 5.2 Vite配置

**更新 `vite.config.ts`**：

```typescript
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': '/src',
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'https://api.example.com',
        changeOrigin: true,
      },
    },
  },
});
```

## 六、错误处理

### 6.1 API错误处理

```typescript
// src/api/index.ts
apiClient.interceptors.response.use(
  (response) => response.data,
  (error) => {
    if (error.response) {
      switch (error.response.status) {
        case 401:
          localStorage.removeItem('token');
          window.location.href = '/login';
          break;
        case 403:
          console.error('Forbidden');
          break;
        case 404:
          console.error('Not Found');
          break;
        case 500:
          console.error('Server Error');
          break;
        default:
          console.error('Unknown Error:', error.response.data);
      }
    } else {
      console.error('Network Error:', error.message);
    }
    return Promise.reject(error);
  }
);
```

### 6.2 组件级错误处理

```typescript
const [error, setError] = useState<string | null>(null);

useEffect(() => {
  fetchData()
    .then(setData)
    .catch((err) => {
      setError(err.message);
      console.error('Data fetch error:', err);
    });
}, []);

if (error) {
  return (
    <div className="text-center py-8 text-red-500">
      数据加载失败：{error}
      <button onClick={() => window.location.reload()} className="mt-4 px-4 py-2 bg-blue-500 text-white rounded-lg">
        重试
      </button>
    </div>
  );
}
```

## 七、数据缓存策略

### 7.1 使用React Query（推荐）

**安装React Query**：

```bash
npm install @tanstack/react-query
```

**配置React Query**：

```typescript
// src/main.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, 
      cacheTime: 30 * 60 * 1000,
      retry: 2,
    },
  },
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <QueryClientProvider client={queryClient}>
    <App />
  </QueryClientProvider>
);
```

**使用React Query**：

```typescript
import { useQuery } from '@tanstack/react-query';
import { getFinancialData } from '../api/financial';

const FinancialDataComponent = ({ stockCode, market }) => {
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['financial', stockCode, market],
    queryFn: () => getFinancialData(stockCode, market),
  });

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <div>
      {data.map((item) => (
        <div key={item.reportDate}>{item.incomeStatement.netProfit}</div>
      ))}
      <button onClick={refetch}>刷新数据</button>
    </div>
  );
};
```

### 7.2 使用localStorage缓存

```typescript
const CACHE_EXPIRY = 5 * 60 * 1000;

const getCachedData = (key: string) => {
  const cached = localStorage.getItem(key);
  if (!cached) return null;
  
  const { data, timestamp } = JSON.parse(cached);
  if (Date.now() - timestamp > CACHE_EXPIRY) {
    localStorage.removeItem(key);
    return null;
  }
  
  return data;
};

const setCachedData = (key: string, data: any) => {
  localStorage.setItem(key, JSON.stringify({
    data,
    timestamp: Date.now(),
  }));
};

export const getFinancialData = async (code: string, market: string) => {
  const cacheKey = `financial_${code}_${market}`;
  const cached = getCachedData(cacheKey);
  
  if (cached) {
    return cached;
  }
  
  const data = await financialApi.getFinancialData(code, market);
  setCachedData(cacheKey, data);
  return data;
};
```

## 八、API接入检查清单

在完成API接入后，检查以下项目：

- [ ] 创建了API目录结构
- [ ] 封装了HTTP请求（axios）
- [ ] 实现了所有API模块
- [ ] 创建了数据服务层，支持Mock/API切换
- [ ] 更新了所有组件使用数据服务
- [ ] 配置了环境变量
- [ ] 实现了API错误处理
- [ ] 配置了开发环境代理
- [ ] 添加了数据缓存策略
- [ ] 测试了API调用正常
- [ ] Mock数据可以通过环境变量切换

## 九、注意事项

1. **API认证**：如果API需要认证，在请求头中添加Authorization
2. **CORS问题**：确保后端配置了正确的CORS策略
3. **数据格式**：确保API返回的数据格式与类型定义一致
4. **错误边界**：为关键组件添加错误边界
5. **加载状态**：所有异步数据加载都需要展示加载状态
6. **数据刷新**：提供数据刷新机制
7. **API限流**：注意API调用频率限制，添加防抖处理
8. **日志记录**：添加适当的日志记录，便于排查问题
