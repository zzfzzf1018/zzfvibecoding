# 组件详细说明

## 组件概览

| 组件名称 | 文件路径 | 功能描述 | 依赖 |
|----------|----------|----------|------|
| StockSearch | components/StockSearch.jsx | 股票搜索和选择 | antd, axios |
| FinanceReport | components/FinanceReport.jsx | 财务报表展示和下载 | antd, axios, xlsx |
| CompanyAnalysis | components/CompanyAnalysis.jsx | 公司分析数据展示 | antd, axios |
| Prospectus | components/Prospectus.jsx | 招股书查询和下载 | antd, axios |

---

## StockSearch组件

### 文件路径

```
src/renderer/src/components/StockSearch.jsx
```

### 功能描述

股票搜索组件，支持：
- 按代码或名称搜索股票
- 市场筛选（全部/A股/港股）
- 热门股票展示
- 选择股票后通知父组件

### 组件结构

```jsx
function StockSearch({ onSelectStock }) {
    const [keyword, setKeyword] = useState('');
    const [market, setMarket] = useState('all');
    const [data, setData] = useState([]);
    const [loading, setLoading] = useState(false);
    const [hotStocks, setHotStocks] = useState([]);
}
```

### 状态管理

| 状态 | 类型 | 初始值 | 说明 |
|------|------|--------|------|
| keyword | string | '' | 搜索关键词 |
| market | string | 'all' | 市场类型 |
| data | array | [] | 搜索结果 |
| loading | boolean | false | 加载状态 |
| hotStocks | array | [] | 热门股票 |

### API调用

```
GET /api/stock/search?keyword=<keyword>&market=<market>
```

### 生命周期

```
component mount
    │
    └── useEffect → fetchHotStocks() → 设置热门股票

用户输入/筛选变化
    │
    └── useEffect → debounce → fetchStocks() → 更新搜索结果

用户点击股票
    │
    └── onSelectStock(stock) → 通知父组件
```

### 核心方法

| 方法 | 功能 | 参数 |
|------|------|------|
| fetchHotStocks | 获取热门股票 | 无 |
| fetchStocks | 搜索股票 | keyword, market |
| handleSelect | 选择股票 | stock |

---

## FinanceReport组件

### 文件路径

```
src/renderer/src/components/FinanceReport.jsx
```

### 功能描述

财务报表组件，支持：
- 资产负债表、利润表、现金流量表展示
- 报表类型切换
- Excel文件下载
- 缓存数据标记显示

### 组件结构

```jsx
function FinanceReport({ stock }) {
    const [activeTab, setActiveTab] = useState('balance');
    const [reports, setReports] = useState({
        balance: { data: [], columns: [], cached: false },
        income: { data: [], columns: [], cached: false },
        cash: { data: [], columns: [], cached: false }
    });
    const [loading, setLoading] = useState(false);
}
```

### 状态管理

| 状态 | 类型 | 初始值 | 说明 |
|------|------|--------|------|
| activeTab | string | 'balance' | 当前显示的报表类型 |
| reports | object | {} | 三种报表数据 |
| loading | boolean | false | 加载状态 |

### API调用

```
GET /api/stock/finance_report?symbol=<symbol>&type=<type>
GET /api/stock/download_report?symbol=<symbol>&type=<type>
```

### 报表类型

| 类型 | 标签 | 说明 |
|------|------|------|
| balance | 资产负债表 | 反映公司资产状况 |
| income | 利润表 | 反映公司盈利能力 |
| cash | 现金流量表 | 反映公司现金流状况 |

### 核心方法

| 方法 | 功能 | 参数 |
|------|------|------|
| fetchReport | 获取财务报表 | type |
| downloadReport | 下载财务报表 | type |
| renderTable | 渲染报表表格 | reportType |

### 数据格式

```javascript
{
    columns: ["指标", "2023年", "2022年", "2021年"],
    data: [
        ["总资产", "3210.00", "2980.00", "2750.00"],
        ["流动资产", "1850.00", "1720.00", "1580.00"]
    ],
    cached: false
}
```

---

## CompanyAnalysis组件

### 文件路径

```
src/renderer/src/components/CompanyAnalysis.jsx
```

### 功能描述

公司分析组件，支持：
- 估值指标展示（PE/PB/PS/股息率）
- 财务比率分析
- 分析总结标签
- 缓存数据标记显示

### 组件结构

```jsx
function CompanyAnalysis({ stock }) {
    const [loading, setLoading] = useState(false);
    const [analysis, setAnalysis] = useState(null);
    const [isCached, setIsCached] = useState(false);
}
```

### 状态管理

| 状态 | 类型 | 初始值 | 说明 |
|------|------|--------|------|
| loading | boolean | false | 加载状态 |
| analysis | object | null | 分析数据 |
| isCached | boolean | false | 是否缓存数据 |

### API调用

```
GET /api/stock/analysis?symbol=<symbol>
```

### 数据结构

```javascript
{
    valuation: {
        pe: 25.5,          // 市盈率
        pb: 6.2,           // 市净率
        ps: 8.5,           // 市销率
        dividend_yield: 2.1 // 股息率
    },
    financial_ratios: {
        "净资产收益率": 22.5,
        "总资产收益率": 15.8,
        "毛利率": 73.1,
        "净利率": 45.2,
        "资产负债率": 21.5,
        "流动比率": 3.2,
        "速动比率": 2.8,
        "应收账款周转天数": 28.5,
        "存货周转天数": 35.2,
        "营业收入增长率": 15.3,
        "净利润增长率": 18.7
    }
}
```

### 核心方法

| 方法 | 功能 | 参数 |
|------|------|------|
| fetchAnalysis | 获取分析数据 | 无 |
| renderValuation | 渲染估值指标 | 无 |
| renderFinancialRatios | 渲染财务比率 | 无 |
| renderSummary | 渲染分析总结 | 无 |

### 指标说明

**估值指标**:
- PE（市盈率）: 股价与每股收益的比率
- PB（市净率）: 股价与每股净资产的比率
- PS（市销率）: 股价与每股销售额的比率
- 股息率: 每股股息与股价的比率

**财务比率**:
- 净资产收益率: 衡量股东权益回报
- 总资产收益率: 衡量资产盈利能力
- 毛利率: 销售收入减去成本的比例
- 净利率: 净利润与销售收入的比例
- 资产负债率: 负债与资产的比例
- 流动比率: 流动资产与流动负债的比例
- 速动比率: 速动资产与流动负债的比例

---

## Prospectus组件

### 文件路径

```
src/renderer/src/components/Prospectus.jsx
```

### 功能描述

招股书组件，支持：
- 招股书列表查询
- 搜索功能
- PDF下载
- 缓存数据标记显示

### 组件结构

```jsx
function Prospectus({ stock }) {
    const [data, setData] = useState([]);
    const [loading, setLoading] = useState(false);
    const [keyword, setKeyword] = useState('');
}
```

### 状态管理

| 状态 | 类型 | 初始值 | 说明 |
|------|------|--------|------|
| data | array | [] | 招股书列表 |
| loading | boolean | false | 加载状态 |
| keyword | string | '' | 搜索关键词 |

### API调用

```
GET /api/stock/prospectus?symbol=<symbol>
GET /api/stock/download_prospectus?url=<url>&filename=<filename>
```

### 数据格式

```javascript
[
    {
        code: "688981",
        name: "中芯国际",
        ipo_date: "2020-07-16",
        prospectus_url: "https://example.com/prospectus.pdf"
    }
]
```

### 核心方法

| 方法 | 功能 | 参数 |
|------|------|------|
| fetchProspectus | 获取招股书列表 | 无 |
| handleDownload | 下载招股书 | item |
| handleSearch | 搜索招股书 | 无 |

---

## App主组件

### 文件路径

```
src/renderer/src/App.jsx
```

### 功能描述

主应用组件，负责：
- 整体布局（Header + Sider + Content）
- 菜单导航
- 选中股票状态管理
- 组件路由

### 组件结构

```jsx
function App() {
    const [selectedStock, setSelectedStock] = useState(null);
    const [activeMenu, setActiveMenu] = useState('search');
}
```

### 状态管理

| 状态 | 类型 | 初始值 | 说明 |
|------|------|--------|------|
| selectedStock | object | null | 选中的股票信息 |
| activeMenu | string | 'search' | 当前激活的菜单 |

### 菜单配置

```javascript
const menuItems = [
    { key: 'search', icon: <SearchOutlined />, label: '股票搜索' },
    { key: 'finance', icon: <BarChartOutlined />, label: '财务报表' },
    { key: 'analysis', icon: <PieChartOutlined />, label: '公司分析' },
    { key: 'prospectus', icon: <ReadOutlined />, label: '招股书' }
];
```

### 路由映射

| 菜单key | 组件 | 条件 |
|---------|------|------|
| search | StockSearch | 无 |
| finance | FinanceReport | 需要selectedStock |
| analysis | CompanyAnalysis | 需要selectedStock |
| prospectus | Prospectus | 无 |

### 核心方法

| 方法 | 功能 | 参数 |
|------|------|------|
| handleSelectStock | 选择股票 | stock |
| handleMenuClick | 点击菜单 | item |

---

## 组件通信

### 数据流

```
StockSearch (子) ──onSelectStock──→ App (父) ──selectedStock──→ FinanceReport (子)
                                                      ├──→ CompanyAnalysis (子)
                                                      └──→ Prospectus (子)
```

### 通信方式

- **父子通信**: props传递和回调函数
- **兄弟通信**: 通过父组件作为中介
- **API通信**: 通过HTTP请求与后端交互

---

## 缓存标记显示

所有组件在接收到`cached: true`时，会显示橙色「缓存数据」标签：

```jsx
{isCached && (
    <Tag color="orange">缓存数据</Tag>
)}
```

这帮助用户识别数据来源，避免误以为是实时数据。

---

## 错误处理

### 全局错误处理

```jsx
try {
    const response = await axios.get(url, params);
    if (response.data.success) {
        // 成功处理
    } else {
        message.error(response.data.error);
    }
} catch (error) {
    message.error('请求失败，请检查网络连接');
}
```

### 组件级错误处理

- 显示加载状态（Spin）
- 请求失败时显示错误提示
- 有缓存数据时显示缓存数据
- 无数据时显示空状态

---

## 性能优化

### React.memo

对纯展示组件使用React.memo减少不必要的重渲染：

```jsx
export default React.memo(FinanceReport);
```

### useMemo

对复杂计算使用useMemo缓存结果：

```jsx
const filteredData = useMemo(() => {
    return data.filter(item => item.name.includes(keyword));
}, [data, keyword]);
```

### 防抖

搜索输入使用防抖减少API调用：

```jsx
const debouncedFetch = useMemo(() => {
    return debounce(fetchStocks, 300);
}, [fetchStocks]);
```
