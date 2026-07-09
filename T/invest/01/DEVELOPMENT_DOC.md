# 财务数据分析平台 - 开发文档

## 一、项目概述

本项目是一个A股和港股上市公司财务数据对比分析平台，支持财务比率分析、数据导出、收藏股票、行业对比、多图表展示等功能。

**相关设计文档**：
- [PRD文档](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/prd.md) - 产品需求文档，包含功能需求和UI设计规范
- [技术架构文档](file:///c:/fly_dev_8/TRAE_ws/01/.trae/documents/technical-architecture.md) - 技术架构设计，包含架构图和API定义

## 二、技术栈

| 分类 | 技术 | 版本 |
|------|------|------|
| 前端框架 | React | 18.3.1 |
| 类型系统 | TypeScript | ~5.8.3 |
| 构建工具 | Vite | 6.3.5 |
| 样式框架 | TailwindCSS | 3.4.17 |
| 状态管理 | Zustand | 5.0.3 |
| 路由 | React Router | 7.3.0 |
| 图表库 | Recharts | 3.9.2 |
| K线图 | lightweight-charts | 5.2.0 |
| 数据导出 | xlsx | 0.18.5 |
| PDF导出 | jspdf | 4.2.1 |
| 图标 | lucide-react | 0.511.0 |
| 测试 | Vitest | 4.1.10 |

## 三、功能实现清单

### 1. 财务比率分析
- **估值指标**：PE（市盈率）、PB（市净率）、PS（市销率）
- **盈利能力指标**：ROE（净资产收益率）、ROA（总资产收益率）
- **偿债能力指标**：资产负债率、流动比率、速动比率
- **运营能力指标**：应收账款周转率、存货周转率

**实现文件**：
- [src/utils/ratios.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/utils/ratios.ts) - 财务比率计算函数
- [src/components/FinancialRatios/FinancialRatios.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/FinancialRatios/FinancialRatios.tsx) - 财务比率展示组件

### 2. 数据导出功能
- 支持导出Excel/PDF格式的财务报表
- 支持导出对比分析报告
- 支持自定义导出字段

**实现文件**：
- [src/components/Export/ExportPanel.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Export/ExportPanel.tsx) - 导出面板组件

### 3. 收藏股票功能
- 用户可以收藏关注的股票
- 收藏列表快速访问
- 使用localStorage持久化存储

**实现文件**：
- [src/store/useFavoritesStore.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/store/useFavoritesStore.ts) - 收藏状态管理
- [src/pages/FavoritesPage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/FavoritesPage.tsx) - 收藏页面

### 4. 行业对比分析
- 按行业分类展示股票
- 同行业财务数据对比
- 行业平均指标参考线

**实现文件**：
- [src/components/Industry/IndustryCompare.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Industry/IndustryCompare.tsx) - 行业对比组件

### 5. 更多图表类型
- **雷达图**：多维度财务指标对比
- **K线图**：股票价格走势（lightweight-charts）
- **杜邦分析图**：财务指标拆解
- **漏斗图**：利润构成分析

**实现文件**：
- [src/components/Charts/RadarChart.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Charts/RadarChart.tsx) - 雷达图组件
- [src/components/Charts/KLineChart.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Charts/KLineChart.tsx) - K线图组件
- [src/components/Charts/DuPontChart.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Charts/DuPontChart.tsx) - 杜邦分析图组件
- [src/components/Charts/FunnelChart.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Charts/FunnelChart.tsx) - 漏斗图组件

### 6. 数据筛选与排序
- 按财务指标排序（如净利润、增长率）
- 按行业/板块筛选
- 自定义筛选条件

**实现文件**：
- [src/components/Search/SearchBar.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Search/SearchBar.tsx) - 搜索栏组件

### 7. 财务预警功能
- 关键指标异常波动提醒
- 财务健康评分
- 风险提示

**实现文件**：
- [src/components/FinancialWarning/FinancialWarning.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/FinancialWarning/FinancialWarning.tsx) - 财务预警组件

### 8. 历史数据回溯
- 查看更多历史年份数据（2022-2024）
- 季度数据展示
- 同比/环比分析

**实现文件**：
- [src/data/mockData.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/data/mockData.ts) - 包含年度和季度财务数据
- [src/components/FinancialTable/FinancialTable.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/FinancialTable/FinancialTable.tsx) - 财务表格组件

### 9. 新闻资讯整合
- 关联股票新闻
- 公告信息展示
- 市场热点追踪

**实现文件**：
- [src/pages/NewsPage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/NewsPage.tsx) - 新闻资讯页面

### 10. 财务日历
- 财报发布日期提醒
- 重要财务事件标记

**实现文件**：
- [src/pages/CalendarPage.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/pages/CalendarPage.tsx) - 财务日历页面

## 四、文件结构说明

```
src/
├── components/           # 组件目录
│   ├── Charts/          # 图表组件
│   │   ├── KLineChart.tsx       # K线图
│   │   ├── RadarChart.tsx       # 雷达图
│   │   ├── DuPontChart.tsx      # 杜邦分析图
│   │   └── FunnelChart.tsx      # 漏斗图
│   ├── Export/          # 导出功能
│   │   └── ExportPanel.tsx      # 导出面板
│   ├── FinancialOverview/       # 财务概览
│   │   └── FinancialOverview.tsx
│   ├── FinancialRatios/         # 财务比率
│   │   └── FinancialRatios.tsx
│   ├── FinancialTable/          # 财务表格
│   │   └── FinancialTable.tsx
│   ├── FinancialWarning/        # 财务预警
│   │   └── FinancialWarning.tsx
│   ├── Industry/        # 行业对比
│   │   └── IndustryCompare.tsx
│   ├── Search/          # 搜索功能
│   │   └── SearchBar.tsx
│   └── StockCard/       # 股票卡片
│       └── StockCard.tsx
├── data/                # 数据层
│   ├── mockData.ts      # Mock数据（股票、财务、新闻、日历、K线）
│   └── mockData.test.ts # 数据测试
├── pages/               # 页面组件
│   ├── HomePage.tsx     # 首页
│   ├── ComparePage.tsx  # 对比分析页
│   ├── FavoritesPage.tsx # 收藏页
│   ├── NewsPage.tsx     # 新闻页
│   └── CalendarPage.tsx # 日历页
├── store/               # 状态管理
│   ├── useCompareStore.ts      # 对比列表状态
│   ├── useFavoritesStore.ts    # 收藏状态
│   └── useCompareStore.test.ts # 状态管理测试
├── types/               # TypeScript类型定义
│   └── index.ts
├── utils/               # 工具函数
│   └── ratios.ts        # 财务比率计算
├── App.tsx              # 应用入口
└── main.tsx             # 主入口
```

## 五、关键技术决策与问题解决方案

### 5.1 lightweight-charts v5 API变更问题

**问题**：lightweight-charts v5 对系列创建API进行了重大变更，原v4的 `addCandlestickSeries()` 和 `addHistogramSeries()` 方法被移除。

**解决方案**：使用新的统一API `addSeries(SeriesType, options)`

```typescript
// v5正确用法
import { createChart, CandlestickSeries, HistogramSeries } from 'lightweight-charts';

const candlestickSeries = chart.addSeries(CandlestickSeries, {
  upColor: '#ef4444',
  downColor: '#22c55e',
});

const volumeSeries = chart.addSeries(HistogramSeries, {
  color: '#6b7280',
  priceFormat: { type: 'volume' },
});
```

**相关文件**：[src/components/Charts/KLineChart.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Charts/KLineChart.tsx)

### 5.2 Stock类型缺少industry字段问题

**问题**：Stock接口定义了industry字段，但在多处使用时未传入，导致TypeScript编译错误。

**解决方案**：确保所有创建Stock对象的地方都包含industry字段。

**相关文件**：[src/types/index.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/types/index.ts)

### 5.3 BalanceSheet类型缺少字段问题

**问题**：BalanceSheet接口定义了inventory和accountsReceivable字段，但测试文件中未提供这些字段。

**解决方案**：在测试数据中补充缺失字段。

**相关文件**：[src/store/useCompareStore.test.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/store/useCompareStore.test.ts)

### 5.4 Recharts Radar组件dataKey类型问题

**问题**：Recharts的Radar组件dataKey属性不接受字符串数组类型。

**解决方案**：遍历每个指标单独创建Radar组件。

**相关文件**：[src/components/Charts/RadarChart.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Charts/RadarChart.tsx)

### 5.5 Recharts Bar组件fill属性类型问题

**问题**：Recharts的Bar组件fill属性不接受函数类型，只能使用字符串。

**解决方案**：使用Cell组件实现动态颜色。

**相关文件**：[src/components/Industry/IndustryCompare.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/Industry/IndustryCompare.tsx)

### 5.6 组件命名冲突问题

**问题**：FinancialRatios组件名与FinancialRatios类型名冲突。

**解决方案**：将组件重命名为FinancialRatiosComponent，类型导入时使用别名。

**相关文件**：[src/components/FinancialRatios/FinancialRatios.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/FinancialRatios/FinancialRatios.tsx)

### 5.7 mockData返回值类型问题

**问题**：getFinancialData函数返回空数组而非undefined，导致测试用例失败。

**解决方案**：更新测试用例以适应新的返回值类型。

**相关文件**：[src/data/mockData.test.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/data/mockData.test.ts)

### 5.8 getHotStocks函数参数变更

**问题**：getHotStocks函数不再接受market参数，但测试用例仍传入参数。

**解决方案**：更新测试用例以匹配新的函数签名。

**相关文件**：[src/data/mockData.test.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/data/mockData.test.ts)

### 5.9 FinancialOverview组件addItem调用问题

**问题**：FinancialOverview组件直接创建Stock对象缺少industry字段，导致类型错误。

**解决方案**：修改组件props，接受完整的Stock对象作为可选参数。

**相关文件**：[src/components/FinancialOverview/FinancialOverview.tsx](file:///c:/fly_dev_8/TRAE_ws/01/src/components/FinancialOverview/FinancialOverview.tsx)

## 六、数据模型说明

### 6.1 Stock（股票）

```typescript
interface Stock {
  code: string;           // 股票代码
  name: string;           // 股票名称
  market: MarketType;     // 市场类型（a股/港股）
  industry: string;       // 行业分类
  price: number;          // 当前价格
  change: number;         // 涨跌额
  changePercent: number;  // 涨跌幅
}
```

### 6.2 FinancialData（财务数据）

```typescript
interface FinancialData {
  stockCode: string;      // 股票代码
  stockName: string;      // 股票名称
  market: MarketType;     // 市场类型
  reportDate: string;     // 报告日期
  periodType: PeriodType; // 期间类型（annual/quarter）
  balanceSheet: BalanceSheet;   // 资产负债表
  incomeStatement: IncomeStatement; // 利润表
  cashFlow: CashFlow;     // 现金流量表
}
```

### 6.3 FinancialRatios（财务比率）

```typescript
interface FinancialRatios {
  pe: number;             // 市盈率
  pb: number;             // 市净率
  ps: number;             // 市销率
  roe: number;            // 净资产收益率
  roa: number;            // 总资产收益率
  debtRatio: number;      // 资产负债率
  currentRatio: number;   // 流动比率
  quickRatio: number;     // 速动比率
  arTurnover: number;     // 应收账款周转率
  inventoryTurnover: number; // 存货周转率
}
```

### 6.4 BalanceSheet（资产负债表）

```typescript
interface BalanceSheet {
  totalAssets: number;        // 总资产
  totalLiabilities: number;   // 总负债
  totalEquity: number;        // 净资产
  currentAssets: number;      // 流动资产
  currentLiabilities: number; // 流动负债
  nonCurrentAssets: number;   // 非流动资产
  nonCurrentLiabilities: number; // 非流动负债
  inventory: number;          // 存货
  accountsReceivable: number; // 应收账款
}
```

### 6.5 IncomeStatement（利润表）

```typescript
interface IncomeStatement {
  revenue: number;           // 营业收入
  grossProfit: number;       // 毛利润
  netProfit: number;         // 净利润
  operatingProfit: number;   // 营业利润
  eps: number;               // 每股收益
  grossMargin: number;       // 毛利率
  netMargin: number;         // 净利率
}
```

### 6.6 CashFlow（现金流量表）

```typescript
interface CashFlow {
  operatingCashFlow: number;  // 经营活动现金流
  investingCashFlow: number;  // 投资活动现金流
  financingCashFlow: number;  // 筹资活动现金流
  netCashFlow: number;        // 净现金流
}
```

### 6.7 News（新闻）

```typescript
interface News {
  id: string;           // 新闻ID
  stockCode: string;    // 股票代码
  stockName: string;    // 股票名称
  title: string;        // 标题
  content: string;      // 内容
  date: string;         // 发布日期
  type: 'news' | 'announcement'; // 类型：新闻/公告
}
```

### 6.8 CalendarEvent（日历事件）

```typescript
interface CalendarEvent {
  id: string;           // 事件ID
  date: string;         // 日期
  stockCode: string;    // 股票代码
  stockName: string;    // 股票名称
  eventType: 'earnings' | 'dividend' | 'meeting'; // 事件类型
  title: string;        // 事件标题
}
```

### 6.9 IndustryData（行业数据）

```typescript
interface IndustryData {
  name: string;         // 行业名称
  stocks: Stock[];      // 行业内股票列表
  averages: {           // 行业平均指标
    pe: number;         // 平均市盈率
    pb: number;         // 平均市净率
    roe: number;        // 平均ROE
    debtRatio: number;  // 平均资产负债率
    revenue: number;    // 平均营业收入
    netProfit: number;  // 平均净利润
  };
}
```

### 6.10 KLineData（K线数据）

```typescript
interface KLineData {
  time: number;   // 时间戳
  open: number;   // 开盘价
  high: number;   // 最高价
  low: number;    // 最低价
  close: number;  // 收盘价
  volume: number; // 成交量
}
```

### 6.11 FinancialHealthScore（财务健康评分）

```typescript
interface FinancialHealthScore {
  score: number;           // 综合评分
  level: 'excellent' | 'good' | 'fair' | 'poor'; // 健康等级
  warnings: string[];      // 风险警告列表
  details: {
    indicator: string;     // 指标名称
    score: number;         // 单项评分
    status: 'pass' | 'warning' | 'danger'; // 状态
  }[];
}
```

### 6.12 财务比率计算公式

| 指标 | 计算公式 |
|------|----------|
| PE（市盈率） | 股价 / 每股收益(EPS) |
| PB（市净率） | 股价 / 每股净资产 |
| PS（市销率） | 股价 / 每股销售额 |
| ROE（净资产收益率） | 净利润 / 平均净资产 × 100% |
| ROA（总资产收益率） | 净利润 / 平均总资产 × 100% |
| 资产负债率 | 总负债 / 总资产 × 100% |
| 流动比率 | 流动资产 / 流动负债 |
| 速动比率 | (流动资产 - 存货) / 流动负债 |
| 应收账款周转率 | 营业收入 / 平均应收账款 |
| 存货周转率 | 营业成本 / 平均存货 |

## 七、状态管理说明

### 7.1 useCompareStore（对比列表）

- **items**: CompareItem[] - 对比列表
- **addItem**: (stock: Stock, financial: FinancialData) => void - 添加对比项
- **removeItem**: (stockCode: string) => void - 移除对比项
- **clearAll**: () => void - 清空列表
- **isInCompare**: (stockCode: string) => boolean - 检查是否在列表中

### 7.2 useFavoritesStore（收藏列表）

- **favorites**: Stock[] - 收藏列表
- **addFavorite**: (stock: Stock) => void - 添加收藏
- **removeFavorite**: (stockCode: string) => void - 移除收藏
- **isFavorite**: (stockCode: string) => boolean - 检查是否已收藏
- 使用localStorage持久化存储

## 八、构建与运行

### 8.1 安装依赖

```bash
npm install
```

### 8.2 开发模式

```bash
npm run dev
```

访问 http://localhost:5173/（或自动分配的其他端口）

### 8.3 生产构建

```bash
npm run build
```

### 8.4 运行测试

```bash
npx vitest run
```

### 8.5 代码检查

```bash
npm run lint
```

## 九、Mock数据说明

项目使用Mock数据，包含：
- **16只股票**：8只A股 + 8只港股
- **行业分类**：白酒、互联网、新能源、医药、银行等
- **年度数据**：2022-2024年财务数据
- **季度数据**：2024年季度财务数据
- **新闻数据**：每只股票关联新闻和公告
- **日历数据**：财报发布、分红、股东大会等事件
- **K线数据**：部分股票的K线价格数据

**相关文件**：[src/data/mockData.ts](file:///c:/fly_dev_8/TRAE_ws/01/src/data/mockData.ts)

## 十、路由配置

| 路径 | 组件 | 功能 |
|------|------|------|
| / | HomePage | 首页（财务数据展示） |
| /compare | ComparePage | 对比分析页 |
| /favorites | FavoritesPage | 收藏列表页 |
| /news | NewsPage | 新闻资讯页 |
| /calendar | CalendarPage | 财务日历页 |

## 十一、注意事项

1. **lightweight-charts v5兼容性**：注意使用新的API格式 `chart.addSeries(SeriesType, options)`
2. **TypeScript类型检查**：所有Stock对象必须包含industry字段
3. **localStorage持久化**：收藏数据存储在localStorage中，清除浏览器数据会丢失收藏
4. **Mock数据限制**：当前使用Mock数据，实际部署时需要接入真实数据源
5. **图表性能**：K线图组件在数据量大时需要注意性能优化

## 十二、已知问题与限制

### 12.1 代码层面问题

1. **lightweight-charts类型转换**：由于lightweight-charts v5的类型系统限制，在KLineChart组件中曾使用过 `as any` 类型转换来绕过类型检查。最终通过正确导入 `CandlestickSeries` 和 `HistogramSeries` 类型解决了该问题。

2. **Recharts组件限制**：
   - Radar组件的dataKey属性不接受字符串数组，需要遍历每个指标单独创建组件
   - Bar组件的fill属性不接受函数类型，需要使用Cell组件实现动态颜色

3. **函数签名变更**：`getHotStocks()` 函数不再接受market参数，调用时无需传入参数。

### 12.2 构建层面问题

1. **构建警告**：生产构建时出现chunk size警告（部分chunk超过500KB），主要是因为lightweight-charts库较大。建议后续使用动态导入进行代码分割优化。

```
(!) Some chunks are larger than 500 kB after minification. Consider:
- Using dynamic import() to code-split the application
- Use build.rollupOptions.output.manualChunks to improve chunking
```

### 12.3 数据层面限制

1. **Mock数据范围**：当前仅包含16只股票（8只A股 + 8只港股），数据覆盖2022-2024年度及2024年季度数据。实际使用时需要接入真实数据源。

2. **财务比率计算假设**：计算PB（市净率）时假设每股净资产 = 净资产 / 22（假设总股本为22亿股），实际计算应使用真实的总股本数据。

3. **K线数据限制**：仅部分股票包含K线数据，数据量有限。

### 12.4 功能层面限制

1. **收藏数据更新**：收藏股票的数据更新提醒功能尚未实现，当前仅支持收藏和展示。

2. **财务预警**：财务健康评分和风险提示基于简单规则，未考虑复杂的财务分析模型。

3. **数据导出**：导出功能基于前端生成，对于大量数据可能存在性能问题。

## 十三、后续开发建议

1. **接入真实数据**：接入股票数据API（如东方财富、雪球、新浪财经等）获取实时财务数据
2. **用户认证**：添加用户注册、登录、权限管理功能
3. **数据推送**：实现收藏股票的数据更新提醒和实时推送功能
4. **技术分析指标**：添加MACD、KDJ、RSI等技术分析指标
5. **移动端适配**：优化移动端界面和交互体验
6. **数据缓存**：实现数据缓存策略，减少API请求次数
7. **多语言支持**：实现中文/英文多语言切换
8. **代码分割**：对lightweight-charts等大型库使用动态导入进行代码分割，优化首屏加载速度
9. **财务模型优化**：引入更复杂的财务分析模型，提升财务预警准确性
10. **批量导出**：优化数据导出功能，支持批量导出和大数据量处理
