# AI开发者入门指南

欢迎接手财务数据分析平台的开发！本指南将帮助你快速上手项目开发。

## 一、环境搭建

### 1.1 安装依赖

```bash
# 进入项目目录
cd c:\fly_dev_8\TRAE_ws\01

# 安装依赖（推荐使用npm）
npm install

# 如果安装失败，可以尝试清除缓存后重新安装
npm cache clean --force
npm install
```

### 1.2 启动开发服务器

```bash
npm run dev
```

访问 http://localhost:5173/（端口可能自动分配）

### 1.3 开发常用命令速查

| 命令 | 功能 |
|------|------|
| `npm run dev` | 启动开发服务器 |
| `npm run build` | 生产构建 |
| `npx vitest run` | 运行单元测试 |
| `npm run lint` | ESLint代码检查 |
| `npm run preview` | 预览生产构建结果 |

### 1.4 项目结构速览

```
src/
├── components/           # 通用组件（按功能分类）
│   ├── Charts/          # 图表组件
│   ├── Export/          # 导出功能
│   ├── FinancialOverview/ # 财务概览
│   ├── FinancialRatios/   # 财务比率
│   ├── FinancialTable/    # 财务表格
│   ├── FinancialWarning/  # 财务预警
│   ├── Industry/        # 行业对比
│   ├── Search/          # 搜索功能
│   └── StockCard/       # 股票卡片
├── data/                # 数据层（Mock数据）
├── pages/               # 页面组件
├── store/               # Zustand状态管理
├── types/               # TypeScript类型定义
└── utils/               # 工具函数
```

### 1.5 核心技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| React | 18.3.1 | 前端框架 |
| TypeScript | ~5.8.3 | 类型系统 |
| Vite | 6.3.5 | 构建工具 |
| TailwindCSS | 3.4.17 | 样式框架 |
| Zustand | 5.0.3 | 状态管理 |
| React Router | 7.3.0 | 路由管理 |
| Recharts | 3.9.2 | 图表库 |
| lightweight-charts | 5.2.0 | K线图 |
| xlsx | 0.18.5 | Excel导出 |
| jspdf | 4.2.1 | PDF导出 |

## 二、快速上手开发

### 2.1 添加新页面

1. 在 `src/pages/` 目录下创建新页面组件
2. 在 `src/App.tsx` 中配置路由

```typescript
// src/pages/NewPage.tsx
import React from 'react';

export default function NewPage() {
  return (
    <div className="min-h-screen bg-gray-50">
      <h1 className="text-2xl font-bold text-gray-800">新页面</h1>
    </div>
  );
}
```

```typescript
// src/App.tsx - 添加路由
<Route path="/new" element={<NewPage />} />
```

### 2.2 添加新组件

1. 在 `src/components/` 目录下创建新组件文件夹
2. 创建组件文件，遵循命名规范

```typescript
// src/components/NewComponent/NewComponent.tsx
import React from 'react';

interface NewComponentProps {
  title: string;
}

export const NewComponent: React.FC<NewComponentProps> = ({ title }) => {
  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800">{title}</h3>
    </div>
  );
};

export default NewComponent;
```

### 2.3 添加新状态管理

1. 在 `src/store/` 目录下创建新store文件
2. 使用Zustand创建状态管理

```typescript
// src/store/useNewStore.ts
import { create } from 'zustand';

interface NewStore {
  items: string[];
  addItem: (item: string) => void;
  removeItem: (item: string) => void;
}

export const useNewStore = create<NewStore>((set) => ({
  items: [],
  addItem: (item) => set((state) => ({ items: [...state.items, item] })),
  removeItem: (item) => set((state) => ({ items: state.items.filter(i => i !== item) })),
}));
```

### 2.4 添加新类型定义

1. 在 `src/types/index.ts` 中添加新接口

```typescript
export interface NewType {
  id: string;
  name: string;
}
```

## 三、关键文件说明

### 3.1 数据层

- [src/data/mockData.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/data/mockData.ts) - Mock数据，包含股票、财务、新闻、日历、K线数据
- 主要函数：`searchStocks()`、`getFinancialData()`、`getLatestFinancialData()`、`getKLineData()`、`getIndustryData()`、`getNews()`、`getCalendarEvents()`

### 3.2 状态管理

- [src/store/useCompareStore.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/store/useCompareStore.ts) - 对比列表状态
- [src/store/useFavoritesStore.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/store/useFavoritesStore.ts) - 收藏列表状态（localStorage持久化）

### 3.3 工具函数

- [src/utils/ratios.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/utils/ratios.ts) - 财务比率计算函数

### 3.4 页面组件

| 页面 | 文件 | 功能 |
|------|------|------|
| 首页 | [src/pages/HomePage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/HomePage.tsx) | 财务数据展示、图表分析 |
| 对比页 | [src/pages/ComparePage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/ComparePage.tsx) | 股票对比分析 |
| 收藏页 | [src/pages/FavoritesPage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/FavoritesPage.tsx) | 收藏列表 |
| 新闻页 | [src/pages/NewsPage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/NewsPage.tsx) | 新闻资讯 |
| 日历页 | [src/pages/CalendarPage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/CalendarPage.tsx) | 财务日历 |

## 四、调试技巧

### 4.1 React DevTools

安装React DevTools浏览器插件，可以：
- 查看组件树
- 检查组件props和state
- 时间旅行调试

### 4.2 Zustand DevTools

Zustand状态管理支持Redux DevTools：
1. 安装Redux DevTools浏览器插件
2. 在store中添加devtools配置

```typescript
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

export const useStore = create(
  devtools((set) => ({
    // ...
  }))
);
```

### 4.3 Console日志

在组件中使用 `console.log()` 进行调试：

```typescript
useEffect(() => {
  console.log('Data updated:', data);
}, [data]);
```

## 五、常见问题

### 5.1 lightweight-charts API问题

lightweight-charts v5 使用新的API格式：

```typescript
// 正确用法
import { createChart, CandlestickSeries, HistogramSeries } from 'lightweight-charts';

const candlestickSeries = chart.addSeries(CandlestickSeries, { ... });
const volumeSeries = chart.addSeries(HistogramSeries, { ... });
```

### 5.2 TypeScript类型错误

如果遇到类型错误，先检查：
1. Stock对象是否包含industry字段
2. BalanceSheet是否包含inventory和accountsReceivable字段
3. 所有接口字段是否完整

### 5.3 构建失败

```bash
# 先运行lint检查
npm run lint

# 运行类型检查
npx tsc --noEmit
```

### 5.4 测试失败

```bash
# 运行测试
npx vitest run

# 查看详细错误
npx vitest run --reporter=verbose
```

## 六、相关文档

- [PRD文档](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/prd.md) - 产品需求文档
- [技术架构文档](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/technical-architecture.md) - 技术架构设计
- [开发文档](file:///c:/fly_dev_8/TRAE_ws/01/DEVELOPMENT_DOC.md) - 完整开发文档
- [编码规范](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/coding-standards.md) - 编码规范与模式
- [功能开发工作流](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/feature-workflow.md) - 新功能开发指南
- [Mock数据替换指南](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/mock-to-api-guide.md) - API接入指南
