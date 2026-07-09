
# 大A ETF工具 - 改动记录

## [1.0.0] - 2026-07-09

### 新增

- feat: 实现 ETF 基本信息查询功能
- feat: 实现成分股分析功能
- feat: 实现 K 线图表展示（日线/周线/月线）
- feat: 实现 PE/PB 估值分析和历史分位数计算
- feat: 实现费率和分红信息查询
- feat: 支持新浪财经和东方财富双数据源切换
- feat: 实现 SQLite 本地缓存，支持离线模式
- feat: 实现 ETF 搜索功能（按代码/名称）
- docs: 添加需求文档（requirements.md）
- docs: 添加设计文档（design.md）
- docs: 添加 AI 开发指导文档（ai-guidance.md）
- docs: 添加 README.md 项目说明文档
- docs: 添加 CHANGELOG.md 改动记录
- build: 添加一键启动脚本（start.bat）
- build: 添加 PowerShell 启动脚本（start.ps1）
- build: 添加 .gitignore 文件

### 技术实现

- 使用 .NET 6.0 WPF 框架
- 使用 MahApps.Metro 美化界面
- 使用 LiveChartsCore 绑定图表
- 使用 Dapper + SQLite 实现数据访问
- 使用 MvvmLight 实现 MVVM 模式
- 使用 Newtonsoft.Json 解析 API 响应

### 文件结构

```
EtfTool.sln
├── EtfTool.Core/
│   ├── Models/
│   │   ├── EtfInfo.cs
│   │   ├── EtfComponent.cs
│   │   ├── KlineData.cs
│   │   ├── EtfStatistics.cs
│   │   └── EtfDividend.cs
│   ├── Enums/
│   │   └── DataSource.cs
│   └── Interfaces/
│       ├── IEtfDataProvider.cs
│       └── ICacheManager.cs
├── EtfTool.Data/
│   ├── ApiClients/
│   │   ├── SinaApiClient.cs
│   │   └── EastMoneyApiClient.cs
│   ├── Cache/
│   │   └── SqliteCacheManager.cs
│   ├── Providers/
│   │   ├── EtfDataProviderFactory.cs
│   │   └── CachedEtfDataProvider.cs
│   └── Services/
│       └── EtfService.cs
├── EtfTool.Wpf/
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── EtfDetailView.xaml
│   │   └── EtfDetailView.xaml.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   └── EtfDetailViewModel.cs
│   ├── Converters/
│   │   └── ChangeColorConverter.cs
│   ├── App.xaml
│   ├── App.xaml.cs
│   └── EtfTool.Wpf.csproj
├── docs/
│   ├── requirements.md
│   ├── design.md
│   └── ai-guidance.md
├── .gitignore
├── README.md
├── start.bat
└── CHANGELOG.md
```

### 影响文件

所有文件均为新增。

---

## 格式说明

### 改动类型

- **feat**: 新功能
- **fix**: Bug 修复
- **refactor**: 代码重构
- **docs**: 文档更新
- **style**: 代码风格调整
- **test**: 测试相关
- **build**: 构建和部署相关
- **perf**: 性能优化
- **chore**: 杂项改动

### 记录规则

每次代码改动必须在此文件中添加记录，包括：
1. 改动类型和描述
2. 详细说明
3. 影响的文件列表

**AI 约束：每次改动必须更新此文件！**
