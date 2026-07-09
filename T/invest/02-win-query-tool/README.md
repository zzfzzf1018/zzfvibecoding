# 股票财务查询系统

一款基于Electron的Windows桌面端软件，用于查询A股和港股的财务数据、公司分析、财务报表下载、招股书下载等功能。

## 功能特性

- **股票搜索** - 支持A股和港股的代码/名称搜索，支持市场筛选
- **财务报表** - 资产负债表、利润表、现金流量表查询与Excel下载
- **公司分析** - 估值指标（PE/PB/PS/股息率）、财务比率分析、分析总结
- **招股书下载** - 招股书列表展示和PDF下载
- **本地缓存** - 查询过的数据自动缓存，支持离线查看
- **一键启动** - 自动启动数据服务，无需手动配置

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    Electron 主进程                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │  Window管理  │  │  Python进程  │  │   IPC通信处理   │  │
│  └──────────────┘  └──────────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Flask API 服务 (Python)                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │  akshare数据 │  │  SQLite缓存  │  │   RESTful API    │  │
│  │    获取      │  │   管理       │  │                  │  │
│  └──────────────┘  └──────────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    React 前端界面                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │  股票搜索    │  │  财务报表    │  │  公司分析/招股书 │  │
│  │  StockSearch │  │FinanceReport│  │  Analysis       │  │
│  └──────────────┘  └──────────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## 项目结构

```
02-win-query-tool/
├── .gitignore                  # Git忽略配置
├── package.json                # Electron主应用配置
├── README.md                   # 项目说明文档
├── public/
│   └── index.html              # 主HTML入口
└── src/
    ├── main/
    │   └── index.js            # Electron主进程
    ├── python/
    │   ├── app.py              # Flask数据服务API
    │   ├── cache.py            # SQLite缓存模块
    │   ├── requirements.txt    # Python依赖
    │   ├── cache/              # 缓存数据库目录
    │   └── downloads/          # 下载文件目录
    └── renderer/
        ├── src/
        │   ├── App.jsx         # 主应用组件
        │   ├── index.jsx       # React入口
        │   └── components/
        │       ├── StockSearch.jsx      # 股票搜索组件
        │       ├── FinanceReport.jsx    # 财务报表组件
        │       ├── CompanyAnalysis.jsx  # 公司分析组件
        │       └── Prospectus.jsx       # 招股书下载组件
        ├── package.json        # 前端依赖配置
        ├── webpack.config.js   # Webpack配置
        └── .babelrc            # Babel配置
```

## 环境要求

- **操作系统**: Windows 10/11
- **Node.js**: >= 18.x
- **Python**: >= 3.10
- **Electron**: 28.x

## 安装步骤

### 1. 安装Python依赖

```bash
pip install -r src/python/requirements.txt
```

主要依赖：
- `akshare` - 金融数据接口
- `flask` - Web服务框架
- `flask-cors` - 跨域支持
- `pandas` - 数据处理
- `openpyxl` - Excel文件读写
- `xlsxwriter` - Excel文件写入

### 2. 安装前端依赖

```bash
cd src/renderer
npm install
```

### 3. 构建前端

```bash
cd src/renderer
npm run build
```

## 启动方式

### 开发模式

```bash
# 先启动Python数据服务（可选，Electron会自动启动）
python src/python/app.py

# 启动Electron应用
npm start
```

### 一键启动（推荐）

```bash
npm start
```

Electron主进程会自动：
1. 创建userData目录（修复沙箱问题）
2. 启动Python Flask服务
3. 轮询健康检查接口等待服务就绪（最多10秒）
4. 服务就绪后显示主窗口

## API接口说明

### 健康检查

```
GET /api/health
```

返回服务状态和akshare可用性。

### 股票搜索

```
GET /api/stock/search?keyword=<关键词>&market=<市场>
```

- `keyword`: 股票代码或名称
- `market`: all/cn/hk

### 财务报表

```
GET /api/stock/finance_report?symbol=<股票代码>&type=<报表类型>
```

- `symbol`: 股票代码（如600519.SH）
- `type`: balance/income/cash

### 公司分析

```
GET /api/stock/analysis?symbol=<股票代码>
```

- `symbol`: 股票代码

### 报表下载

```
GET /api/stock/download_report?symbol=<股票代码>&type=<报表类型>
```

### 招股书查询

```
GET /api/stock/prospectus?symbol=<股票代码>
```

### 招股书下载

```
GET /api/stock/download_prospectus?url=<PDF链接>&filename=<文件名>
```

### 缓存管理

```
GET /api/cache/stats                    # 获取缓存统计
POST /api/cache/clear                   # 清除所有缓存
POST /api/cache/clear_expired           # 清除过期缓存
```

## 缓存机制

### 工作流程

1. **有网络时**: 优先使用akshare实时数据，成功后写入SQLite缓存
2. **网络失败时**: 检查本地缓存，有缓存则返回并标记 `cached: true`
3. **无网络无缓存时**: 返回错误信息

### 缓存配置

- **缓存有效期**: 24小时
- **缓存路径**: `src/python/cache/stock_data.db`
- **自动清理**: 服务启动时自动清理过期缓存

### 缓存接口

| 接口 | 缓存键 | 缓存内容 |
|------|--------|----------|
| stock_search | keyword + market | 搜索结果列表 |
| finance_report | symbol + type | 财务报表数据 |
| stock_analysis | symbol | 分析数据 |
| prospectus | symbol | 招股书列表 |

## 前端组件说明

### StockSearch

- 股票搜索和选择功能
- 支持市场筛选（全部/A股/港股）
- 热门股票自动加载
- 选择股票后通知父组件

### FinanceReport

- 财务报表展示（资产负债表/利润表/现金流量表）
- 支持Excel下载
- 缓存数据标记显示

### CompanyAnalysis

- 估值指标展示（PE/PB/PS/股息率）
- 财务比率分析
- 分析总结标签
- 缓存数据标记显示

### Prospectus

- 招股书列表查询
- 支持搜索和下载
- 缓存数据标记显示

## 开发说明

### 代码规范

- JavaScript/React: 使用ES6+语法
- Python: 使用PEP 8规范
- CSS: 使用内联样式或Ant Design组件

### 调试方式

1. **Python服务调试**:
   ```bash
   python src/python/app.py
   ```

2. **前端开发模式**:
   ```bash
   cd src/renderer
   npm start
   ```

3. **Electron调试**:
   - 打开开发者工具: Ctrl+Shift+I
   - 主进程日志: 终端输出

### 常见问题

#### Q: 启动失败，提示"无法启动数据服务"
A: 请确保已安装Python和相关依赖，检查Python路径是否在系统环境变量中。

#### Q: 网络请求失败
A: akshare需要网络连接获取实时数据，网络异常时会返回缓存数据或错误信息。

#### Q: 缓存数据不更新
A: 缓存有效期为24小时，可手动清除缓存：
   ```bash
   curl -X POST http://localhost:5000/api/cache/clear
   ```

#### Q: 沙箱权限问题
A: Electron已配置userData指向项目目录，无需额外配置。

## 后续维护

### 添加新功能

1. 在Python端添加新API接口（app.py）
2. 在前端添加新组件（src/renderer/src/components/）
3. 在App.jsx中注册路由和菜单

### 更新依赖

```bash
# Python依赖
pip install --upgrade akshare flask pandas

# 前端依赖
cd src/renderer
npm update
```

### 打包发布

```bash
npm run build
```

使用electron-builder打包成Windows安装包。

## 许可证

MIT License

## 联系方式

如有问题或建议，请提交Issue或联系开发者。
