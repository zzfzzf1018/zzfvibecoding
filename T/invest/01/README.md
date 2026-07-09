# 财务数据对比分析工具

一款专业的A股和港股上市公司财务数据对比分析工具，帮助投资者快速获取、分析和对比不同市场上市公司的财务数据。

## 功能特性

- **股票搜索**：支持A股/港股市场切换，输入股票代码或名称搜索
- **财务数据展示**：资产负债表、利润表、现金流量表
- **数据对比**：多股票对比、关键指标对比、图表可视化
- **趋势分析**：2022-2024年多期财务数据趋势图
- **财务比率分析**：PE、PB、ROE、ROA等10项关键指标
- **行业对比**：同行业财务数据对比、行业平均指标参考
- **收藏功能**：收藏关注的股票，数据持久化存储
- **数据导出**：支持Excel/PDF格式导出
- **新闻资讯**：关联股票新闻、公告信息展示
- **财务日历**：财报发布日期提醒、重要事件标记
- **多图表类型**：雷达图、K线图、杜邦分析图、漏斗图

## 技术栈

- React 18 + TypeScript
- TailwindCSS 3
- Recharts（图表）
- lightweight-charts（K线图）
- Zustand（状态管理）
- React Router v7
- Vite（构建工具）
- xlsx（Excel导出）
- jspdf（PDF导出）

## 一键启动

### 环境要求

- Node.js >= 22.x
- npm（已随Node.js安装）

### 启动步骤

```bash
# 1. 进入项目目录
cd c:\fly_dev_8\TRAE_ws\01

# 2. 安装依赖（首次运行）
npm install

# 3. 启动开发服务器
npm run dev
```

### 访问地址

启动成功后，在浏览器中访问：
- **开发环境**：http://localhost:5173/

> 注意：如果端口5173被占用，Vite会自动使用其他端口（如5174、5175等），请以启动日志中显示的端口为准。

### 其他命令

```bash
# 构建生产版本
npm run build

# 预览生产版本
npm run preview

# 运行测试
npx vitest run

# 代码检查
npm run lint
npx tsc --noEmit
```

## 项目结构

```
src/
├── components/          # 组件
│   ├── Charts/          # 图表组件（K线图、雷达图等）
│   ├── Export/          # 数据导出
│   ├── FinancialOverview/ # 财务概览
│   ├── FinancialRatios/   # 财务比率
│   ├── FinancialTable/    # 财务表格
│   ├── FinancialWarning/  # 财务预警
│   ├── Industry/        # 行业对比
│   ├── Search/          # 搜索组件
│   └── StockCard/       # 股票卡片
├── pages/               # 页面
│   ├── HomePage.tsx     # 首页
│   ├── ComparePage.tsx  # 对比页面
│   ├── FavoritesPage.tsx # 收藏页面
│   ├── NewsPage.tsx     # 新闻页面
│   └── CalendarPage.tsx # 日历页面
├── store/               # 状态管理
│   ├── useCompareStore.ts  # 对比列表状态
│   └── useFavoritesStore.ts # 收藏状态
├── data/                # 数据
│   └── mockData.ts      # Mock数据
├── types/               # 类型定义
│   └── index.ts
└── utils/               # 工具函数
    └── ratios.ts        # 财务比率计算
```

## 数据源

本项目使用Mock数据，包含：
- 8只A股股票（贵州茅台、五粮液、中国平安等）
- 8只港股股票（腾讯控股、汇丰控股、工商银行等）
- 每只股票包含2022-2024年年度财务数据
- 2024年季度财务数据
- 新闻资讯数据
- 财务日历数据
- K线价格数据

## AI开发者快速入口

欢迎接手本项目开发！以下文档帮助你快速上手：

### 开发指南

| 文档 | 路径 | 说明 |
|------|------|------|
| AI开发入门指南 | `.trae/documents/ai-dev-guide.md` | 环境搭建、项目结构、开发命令速查 |
| 编码规范 | `.trae/documents/coding-standards.md` | 命名约定、组件开发模式、状态管理规范 |
| 新功能开发工作流 | `.trae/documents/feature-workflow.md` | 从需求分析到测试验证的标准步骤 |
| Mock数据替换指南 | `.trae/documents/mock-to-api-guide.md` | API接口设计、数据层改造、环境配置 |

### 设计文档

| 文档 | 路径 | 说明 |
|------|------|------|
| PRD文档 | `.trae/documents/prd.md` | 产品需求文档 |
| 技术架构文档 | `.trae/documents/technical-architecture.md` | 技术架构设计 |
| 开发文档 | `DEVELOPMENT_DOC.md` | 完整开发文档（功能清单、问题解决方案、数据模型） |

### 快速上手步骤

1. **阅读入门指南**：了解项目结构和开发命令
2. **学习编码规范**：熟悉命名约定和组件开发模式
3. **参考工作流**：按照标准流程开发新功能
4. **接入API**：根据Mock替换指南接入真实数据源

## 注意事项

- 数据仅供参考，不构成投资建议
- 如需接入真实数据源，请参考 `.trae/documents/mock-to-api-guide.md`
- lightweight-charts v5使用新API：`chart.addSeries(CandlestickSeries, options)`
- 收藏数据存储在localStorage中，清除浏览器数据会丢失
