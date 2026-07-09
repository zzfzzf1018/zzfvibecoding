# 编码规范文档

## 一、命名约定

### 1.1 文件命名

| 文件类型 | 命名规则 | 示例 |
|----------|----------|------|
| 组件文件 | PascalCase | `StockCard.tsx`, `FinancialOverview.tsx` |
| 页面文件 | PascalCase | `HomePage.tsx`, `ComparePage.tsx` |
| 状态管理 | useXXXStore.ts | `useCompareStore.ts`, `useFavoritesStore.ts` |
| 类型定义 | index.ts (统一导出) | `types/index.ts` |
| 工具函数 | camelCase.ts | `ratios.ts`, `format.ts` |
| Mock数据 | mockData.ts | `mockData.ts` |
| 测试文件 | *.test.ts | `mockData.test.ts`, `useCompareStore.test.ts` |

### 1.2 组件命名

- 组件名称使用PascalCase
- 导出时同时导出命名导出和默认导出

```typescript
// 正确
export const StockCard: React.FC<StockCardProps> = ({ stock }) => {
  return <div>{stock.name}</div>;
};

export default StockCard;
```

### 1.3 变量命名

| 类型 | 命名规则 | 示例 |
|------|----------|------|
| 普通变量 | camelCase | `stockCode`, `financialData` |
| 常量 | UPPER_CASE | `MAX_COMPARE_COUNT`, `COLORS` |
| 接口 | PascalCase, 后缀为Interface | `StockInterface`（或直接用interface名） |
| 类型别名 | PascalCase | `MarketType`, `PeriodType` |
| Hook | useCamelCase | `useCompareStore`, `useDebounce` |

### 1.4 函数命名

- 函数名使用camelCase
- 以动词开头

```typescript
// 正确
const calculateRatios = (financial: FinancialData, stock?: Stock) => { ... };
const formatNumber = (num: number) => { ... };

// 错误
const FinancialRatios = () => { ... }; // 这是组件，应使用PascalCase
const getFinancial = () => { ... }; // 语义不明确
```

## 二、组件开发规范

### 2.1 组件结构

```typescript
import React, { useState, useEffect } from 'react';
import { Stock } from '../../types';

interface ComponentNameProps {
  stock: Stock;
  onSelect?: (stock: Stock) => void;
}

export const ComponentName: React.FC<ComponentNameProps> = ({ stock, onSelect }) => {
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    // 副作用逻辑
  }, [stock]);

  const handleClick = () => {
    onSelect?.(stock);
  };

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">标题</h3>
      <button onClick={handleClick} className="px-4 py-2 bg-blue-500 text-white rounded-lg">
        按钮
      </button>
    </div>
  );
};

export default ComponentName;
```

### 2.2 组件分类

| 分类 | 目录 | 说明 |
|------|------|------|
| 通用组件 | `src/components/` | 可复用的业务组件 |
| 页面组件 | `src/pages/` | 页面级组件，包含路由配置 |
| 图表组件 | `src/components/Charts/` | 数据可视化组件 |
| 数据组件 | `src/components/Financial*/` | 财务数据相关组件 |

### 2.3 Props定义规范

- 使用interface定义props
- 可选属性使用`?`标记
- 类型定义放在组件文件顶部

```typescript
interface StockCardProps {
  stock: Stock;                      // 必填
  showPrice?: boolean;               // 可选，默认true
  onAddCompare?: (stock: Stock) => void; // 可选回调
}

// 使用默认值
export const StockCard: React.FC<StockCardProps> = ({ 
  stock, 
  showPrice = true, 
  onAddCompare 
}) => { ... };
```

### 2.4 Hook使用规范

- 使用React Hooks管理状态和副作用
- 遵守Hooks规则：只在组件顶层调用
- 自定义Hook以`use`开头

```typescript
// 正确
const [data, setData] = useState<FinancialData[]>([]);

useEffect(() => {
  fetchData().then(setData);
}, [param]);

// 自定义Hook
const useFinancialData = (stockCode: string) => {
  const [data, setData] = useState<FinancialData | null>(null);
  
  useEffect(() => {
    const financial = getLatestFinancialData(stockCode, 'a股');
    setData(financial);
  }, [stockCode]);
  
  return data;
};
```

## 三、状态管理规范

### 3.1 Zustand Store结构

```typescript
import { create } from 'zustand';

interface StoreName {
  // 状态
  items: CompareItem[];
  
  // actions
  addItem: (stock: Stock, financial: FinancialData) => void;
  removeItem: (stockCode: string) => void;
  clearAll: () => void;
  isInCompare: (stockCode: string) => boolean;
}

export const useStoreName = create<StoreName>((set, get) => ({
  items: [],
  
  addItem: (stock, financial) => set((state) => {
    if (state.items.some(item => item.stock.code === stock.code)) {
      return state;
    }
    return { items: [...state.items, { stock, financial }] };
  }),
  
  removeItem: (stockCode) => set((state) => ({
    items: state.items.filter(item => item.stock.code !== stockCode)
  })),
  
  clearAll: () => set({ items: [] }),
  
  isInCompare: (stockCode) => get().items.some(item => item.stock.code === stockCode),
}));
```

### 3.2 Store命名规范

- Store文件命名：`use{StoreName}Store.ts`
- Store函数命名：`use{StoreName}`
- 状态属性使用名词
- Action使用动词开头

### 3.3 localStorage持久化

对于需要持久化的状态，使用zustand的persist中间件：

```typescript
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export const useFavoritesStore = create(
  persist(
    (set, get) => ({
      favorites: [] as Stock[],
      addFavorite: (stock) => set((state) => ({
        favorites: [...state.favorites, stock]
      })),
      removeFavorite: (stockCode) => set((state) => ({
        favorites: state.favorites.filter(s => s.code !== stockCode)
      })),
      isFavorite: (stockCode) => get().favorites.some(s => s.code === stockCode),
    }),
    {
      name: 'favorites-storage',
    }
  )
);
```

## 四、TypeScript类型规范

### 4.1 类型定义位置

所有类型定义统一放在 `src/types/index.ts` 中：

```typescript
// src/types/index.ts
export type MarketType = 'a股' | '港股';
export type PeriodType = 'annual' | 'quarter';

export interface Stock {
  code: string;
  name: string;
  market: MarketType;
  industry: string;
  price: number;
  change: number;
  changePercent: number;
}
```

### 4.2 类型导入规范

- 使用绝对路径导入类型
- 类型冲突时使用别名

```typescript
// 正确
import { Stock, FinancialData } from '../../types';

// 类型冲突时
import { FinancialRatios as FinancialRatiosType } from '../../types';
```

### 4.3 避免any类型

- 尽量使用具体类型
- 无法确定类型时使用unknown

```typescript
// 正确
const data: FinancialData[] = [];

// 避免
const data: any = [];

// 无法确定类型时
const data: unknown = fetchData();
if (Array.isArray(data)) {
  // data is now Array<unknown>
}
```

### 4.4 泛型使用

在需要复用的组件中使用泛型：

```typescript
interface TableProps<T> {
  data: T[];
  columns: ColumnDef<T>[];
}

export const Table = <T extends Record<string, unknown>>({ data, columns }: TableProps<T>) => {
  return (
    <table>
      {data.map((row) => (
        <tr key={row.id as string}>
          {columns.map((col) => (
            <td>{row[col.accessorKey]}</td>
          ))}
        </tr>
      ))}
    </table>
  );
};
```

## 五、错误处理规范

### 5.1 组件错误边界

使用React错误边界处理组件级错误：

```typescript
class ErrorBoundary extends React.Component<{ children: React.ReactNode }, { hasError: boolean }> {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: Error) {
    console.error('Component error:', error);
  }

  render() {
    if (this.state.hasError) {
      return <div className="text-center py-8">组件加载失败</div>;
    }
    return this.props.children;
  }
}
```

### 5.2 空状态处理

所有数据展示组件需要处理空状态：

```typescript
// 正确
if (data.length === 0) {
  return (
    <div className="text-center py-8 text-gray-500">
      暂无数据
    </div>
  );
}

return <Chart data={data} />;
```

### 5.3 加载状态处理

异步数据加载需要展示加载状态：

```typescript
const [loading, setLoading] = useState(true);
const [data, setData] = useState<FinancialData[]>([]);

useEffect(() => {
  setLoading(true);
  fetchData().then((result) => {
    setData(result);
    setLoading(false);
  });
}, []);

if (loading) {
  return (
    <div className="flex justify-center items-center py-8">
      <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>
  );
}
```

## 六、样式规范

### 6.1 TailwindCSS使用

- 使用TailwindCSS进行样式管理
- 避免使用内联样式
- 复杂样式提取为CSS模块

```typescript
// 正确
<div className="bg-white rounded-xl shadow-sm p-6">
  <h3 className="text-lg font-semibold text-gray-800 mb-4">标题</h3>
</div>

// 避免
<div style={{ backgroundColor: 'white', borderRadius: '0.75rem' }}>...</div>
```

### 6.2 颜色规范

| 场景 | 颜色 | 说明 |
|------|------|------|
| 背景 | bg-gray-50 | 页面背景 |
| 卡片 | bg-white | 卡片背景 |
| 主色 | bg-blue-500 | 主要按钮、链接 |
| 成功 | bg-green-500, text-green-500 | 上涨、正面指标 |
| 失败 | bg-red-500, text-red-500 | 下跌、负面指标 |
| 警告 | bg-yellow-500 | 警告信息 |
| 文字 | text-gray-800, text-gray-600, text-gray-500 | 不同层级文字 |

### 6.3 间距规范

- 使用Tailwind预设间距
- 卡片内边距使用 `p-6`
- 元素间距使用 `gap-4`、`mb-4`、`mt-4`

## 七、性能优化规范

### 7.1 React.memo

对频繁重渲染的组件使用React.memo：

```typescript
interface StockCardProps {
  stock: Stock;
}

export const StockCard = React.memo<StockCardProps>(({ stock }) => {
  return <div>{stock.name}</div>;
});
```

### 7.2 useMemo/useCallback

对计算密集型操作使用useMemo，对回调函数使用useCallback：

```typescript
const ratios = useMemo(() => {
  return calculateRatios(financial, stock);
}, [financial, stock]);

const handleClick = useCallback(() => {
  onSelect?.(stock);
}, [stock, onSelect]);
```

### 7.3 虚拟滚动

对于大量数据列表，使用虚拟滚动：

```typescript
import { FixedSizeList } from 'react-window';

const StockList = ({ stocks }) => (
  <FixedSizeList
    height={400}
    width="100%"
    itemCount={stocks.length}
    itemSize={60}
  >
    {({ index, style }) => (
      <div style={style}>
        <StockCard stock={stocks[index]} />
      </div>
    )}
  </FixedSizeList>
);
```

## 八、测试规范

### 8.1 测试文件结构

```typescript
// src/data/mockData.test.ts
import { describe, it, expect } from 'vitest';
import { searchStocks, getFinancialData } from './mockData';

describe('mockData', () => {
  describe('searchStocks', () => {
    it('should search A股 stocks by code', () => {
      const result = searchStocks('600519');
      expect(result.length).toBeGreaterThan(0);
      expect(result[0].code).toBe('600519');
    });
  });
});
```

### 8.2 测试类型

| 测试类型 | 说明 | 示例 |
|----------|------|------|
| 单元测试 | 测试单个函数/组件 | `mockData.test.ts` |
| 状态测试 | 测试Zustand store | `useCompareStore.test.ts` |
| 集成测试 | 测试组件交互 | `StockCard.test.tsx` |

### 8.3 测试命令

```bash
# 运行所有测试
npx vitest run

# 监听模式
npx vitest watch

# 覆盖率报告
npx vitest run --coverage
```

## 九、代码审查清单

在提交代码前，检查以下内容：

- [ ] 代码符合命名规范
- [ ] TypeScript类型检查通过
- [ ] ESLint检查通过
- [ ] 所有组件有适当的错误处理和空状态
- [ ] 关键函数有单元测试
- [ ] 组件结构清晰，职责单一
- [ ] 避免使用any类型
- [ ] 使用React Hooks正确
- [ ] 样式使用TailwindCSS
- [ ] 性能优化（memo、useMemo等）
