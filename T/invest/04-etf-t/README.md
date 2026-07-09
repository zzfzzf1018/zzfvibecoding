# 中国股市ETF查询工具

一款专业的中国股市ETF数据查询应用，提供ETF列表浏览、详情查看、估值分析等功能。

## 功能特性

- **ETF列表**：浏览全部ETF，支持按类型筛选（宽基/行业/主题/债券/跨境）
- **搜索功能**：支持按代码、名称搜索ETF
- **基本信息**：查看ETF代码、名称、基金公司、成立日期、规模、跟踪指数等
- **成分股**：查看前十大重仓股及持仓比例
- **费率信息**：管理费、托管费、销售服务费、申购赎回费
- **分红情况**：分红记录、分红金额、除息日等
- **估值指标**：PE、PB、PS、盈利收益率、股息率
- **历史分位数**：PE/PB历史分位走势图及详细数据

## 技术栈

- React 18 + TypeScript
- Vite 6
- Tailwind CSS 3
- React Router 6
- Chart.js + react-chartjs-2（图表）
- Lucide React（图标）

## 快速开始

### 环境要求

- Node.js >= 18.0.0
- npm >= 9.0.0

### 安装依赖

```bash
npm install
```

### 启动开发服务器

```bash
npm run dev
```

访问 http://localhost:5173 查看应用。

### 构建生产版本

```bash
npm run build
```

### 预览生产版本

```bash
npm run preview
```

## 项目结构

```
├── .trae/documents/       # 需求和技术文档
│   ├── prd.md            # 产品需求文档
│   └── tech-arch.md      # 技术架构文档
├── src/
│   ├── components/       # React组件
│   ├── pages/            # 页面组件
│   ├── data/             # Mock数据
│   ├── api/              # API接口
│   ├── hooks/            # 自定义Hooks
│   ├── types/            # TypeScript类型定义
│   ├── App.tsx           # 应用入口
│   ├── main.tsx          # 主入口
│   └── index.css         # 全局样式
├── package.json
├── vite.config.ts
├── tsconfig.json
└── tailwind.config.js
```

## 数据说明

当前版本使用Mock数据，包含16只常见ETF的完整数据。后续可接入真实数据源。

## License

MIT