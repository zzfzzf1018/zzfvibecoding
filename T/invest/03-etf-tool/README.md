
# 大A ETF工具

一个基于 WPF 的 ETF 数据分析工具，支持查询 ETF 基本信息、成分股、K线、估值指标等。

## 功能特性

### 1. ETF基本信息查询
- 基金代码、名称、全称
- 最新净值、涨跌幅
- 总资产、基金份额
- PE、PB 估值指标

### 2. 成分股分析
- 成分股列表（支持按权重排序）
- 各成分股权重占比
- 成分股 PE、PB 数据

### 3. K线图表
- 日线、周线、月线切换
- 蜡烛图展示
- MA5、MA10 均线指标

### 4. 估值分析
- 当前 PE、PB 值
- PE、PB 历史分位数计算
- PE、PB 均值、最小值、最大值
- 估值区间展示

### 5. 费率与分红
- 管理费、托管费信息
- 分红记录查询

### 6. 数据源切换
- 支持新浪财经、东方财富双数据源
- 自动切换备用数据源
- 本地数据缓存（离线可用）

## 技术栈

- **框架**: .NET 6.0 WPF
- **UI库**: MahApps.Metro
- **图表库**: LiveChartsCore.SkiaSharpView
- **ORM**: Dapper + SQLite
- **JSON解析**: Newtonsoft.Json
- **MVVM框架**: MvvmLight

## 项目结构

```
EtfTool.sln
├── EtfTool.Core/           # 核心业务层
│   ├── Models/             # 数据模型
│   ├── Enums/              # 枚举定义
│   └── Interfaces/         # 接口定义
├── EtfTool.Data/           # 数据访问层
│   ├── ApiClients/         # API客户端
│   ├── Cache/              # 缓存管理
│   ├── Providers/          # 数据提供者
│   └── Services/           # 业务服务
├── EtfTool.Wpf/            # WPF展示层
│   ├── Views/              # 视图页面
│   ├── ViewModels/         # 视图模型
│   └── Converters/         # 数据转换器
├── docs/                   # 项目文档
│   ├── requirements.md     # 需求文档
│   ├── design.md           # 设计文档
│   └── ai-guidance.md      # AI开发指导文档
├── .gitignore              # Git忽略配置
├── CHANGELOG.md            # 改动记录
├── README.md               # 项目说明
└── start.bat               # 一键启动脚本
```

## 文档目录

| 文档 | 说明 |
| :--- | :--- |
| [requirements.md](docs/requirements.md) | 完整的需求文档，包含功能需求、非功能需求、数据需求等 |
| [design.md](docs/design.md) | 详细的设计文档，包含架构设计、数据模型、接口设计、缓存策略等 |
| [ai-guidance.md](docs/ai-guidance.md) | AI开发指导文档，帮助后续AI开发者快速上手 |
| [CHANGELOG.md](CHANGELOG.md) | 改动记录，记录每次代码变更 |

## 快速开始

### 环境要求

- Windows 10 或更高版本
- .NET 6.0 SDK 或更高版本

### 一键启动

1. 双击运行 `start.bat` 脚本
2. 脚本会自动检查环境、还原依赖、构建并启动应用

### 手动启动

```bash
# 还原依赖
dotnet restore

# 构建项目
dotnet build --configuration Release

# 启动应用
dotnet run --project EtfTool.Wpf
```

## 使用说明

### 搜索 ETF

1. 在搜索框中输入 ETF 代码或名称
2. 按回车键或点击搜索按钮
3. 在搜索结果列表中双击选择需要查看的 ETF

### 切换数据源

在顶部工具栏的下拉菜单中选择数据源：
- Sina：新浪财经
- EastMoney：东方财富

### 查看 K线

1. 切换到 K线标签页
2. 点击日线/周线/月线按钮切换时间周期

### 清除缓存

点击清除缓存按钮可以清除本地缓存数据，下次加载时会重新从网络获取。

## 数据缓存

应用使用 SQLite 数据库进行本地缓存，缓存文件位于：
```
EtfTool.Wpf\bin\Release\net6.0-windows\Cache\etf_cache.db
```

缓存策略：
- ETF基本信息：缓存24小时
- 成分股数据：缓存7天
- K线数据：长期缓存

## API接口说明

### 新浪财经API
- 实时行情：`https://hq.sinajs.cn/list={market}{code}`
- K线数据：`https://finance.sina.com.cn/stock/api/jsonp.php/...`
- 搜索建议：`https://suggest.sinajs.cn/suggest/type=11&key={keyword}`

### 东方财富API
- 实时行情：`https://push2.eastmoney.com/api/qt/stock/get`
- K线数据：`https://push2his.eastmoney.com/api/qt/stock/kline/get`
- 成分股：`https://datacenter.eastmoney.com/api/data/v1/get`
- 分红信息：`https://datacenter.eastmoney.com/api/data/v1/get`

## 注意事项

1. 本工具使用公开API获取数据，请勿高频请求
2. 数据仅供参考，不构成投资建议
3. 部分数据可能存在延迟或不准确
4. 建议定期清除缓存以获取最新数据

## 许可证

MIT License
