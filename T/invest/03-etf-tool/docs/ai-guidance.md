
# 大A ETF工具 - AI开发指导文档

## 1. 项目概述

本项目是一个基于 WPF 的大A ETF数据分析工具。此文档旨在帮助 AI 开发者快速理解项目结构和开发规范，以便高效地进行后续开发和维护。

## 2. 项目结构

```
EtfTool.sln
├── EtfTool.Core/           # 核心数据模型和接口
│   ├── Models/             # 数据模型（EtfInfo、EtfComponent等）
│   ├── Enums/              # 枚举定义（DataSource）
│   └── Interfaces/         # 接口定义（IEtfDataProvider、ICacheManager）
├── EtfTool.Data/           # 数据访问层
│   ├── ApiClients/         # API客户端（新浪财经、东方财富）
│   ├── Cache/              # 缓存管理（SQLite）
│   ├── Providers/          # 数据提供者工厂和缓存包装
│   └── Services/           # 核心业务服务（EtfService）
└── EtfTool.Wpf/            # WPF展示层
    ├── Views/              # 视图页面（MainWindow、EtfDetailView）
    ├── ViewModels/         # 视图模型（MVVM）
    └── Converters/         # 数据转换器
```

## 3. 开发规范

### 3.1 命名规范

| 类型 | 命名规则 | 示例 |
| :--- | :--- | :--- |
| 类名 | PascalCase | EtfInfo、EtfService |
| 方法名 | PascalCase | GetEtfInfoAsync、CalculateStatistics |
| 属性名 | PascalCase | LatestPrice、ChangePercent |
| 私有字段 | camelCase 加下划线前缀 | _etfService、_isLoading |
| 接口名 | I 前缀 + PascalCase | IEtfDataProvider、ICacheManager |
| 文件名 | 与类名一致 | EtfInfo.cs、EtfService.cs |

### 3.2 代码风格

- 使用 C# 顶级语句和隐式 using
- 启用可空引用类型（Nullable enable）
- 使用 async/await 进行异步操作
- 方法参数使用命名参数
- 异常处理使用 try-catch 块
- 避免魔法数字，使用常量或枚举

### 3.3 提交规范

每次提交必须遵循以下格式：

```
<类型>: <描述>

<详细说明>

影响文件：
- <文件路径1>
- <文件路径2>
```

类型包括：
- feat：新功能
- fix：Bug修复
- refactor：重构
- docs：文档更新
- style：代码风格调整
- test：测试
- build：构建相关

### 3.4 文档更新规则

**AI 约束：每次改动必须更新相关文档和改动记录！**

| 改动类型 | 需要更新的文档 |
| :--- | :--- |
| 新增功能 | requirements.md、design.md、CHANGELOG.md |
| 修改功能 | requirements.md、design.md、CHANGELOG.md |
| 修复 Bug | CHANGELOG.md |
| 代码重构 | design.md、CHANGELOG.md |
| 更新文档 | CHANGELOG.md |

## 4. 数据访问层开发指南

### 4.1 添加新数据源

1. 在 `EtfTool.Data/ApiClients/` 目录下创建新的 API 客户端
2. 实现 `IEtfDataProvider` 接口
3. 在 `EtfDataProviderFactory` 中注册新数据源
4. 更新 `DataSource` 枚举
5. 更新文档

### 4.2 添加新缓存类型

1. 在 `ICacheManager` 接口中添加新方法
2. 在 `SqliteCacheManager` 中实现
3. 更新数据库表结构（如果需要）
4. 更新 design.md 中的缓存策略

### 4.3 API 调用注意事项

- 设置合理的超时时间（建议 30 秒）
- 添加 User-Agent 伪装
- 处理网络异常和数据解析异常
- 避免高频请求，使用缓存机制

## 5. 业务逻辑层开发指南

### 5.1 在 EtfService 中添加新方法

1. 添加异步方法，返回 Task<T>
2. 调用 `_currentProvider` 获取数据
3. 处理数据转换和计算逻辑
4. 添加适当的错误处理
5. 更新文档

### 5.2 估值计算

估值计算位于 `EtfService.CalculateStatisticsAsync` 方法中：

- PE/PB 分位数计算：基于历史 K 线数据
- 成分股加权计算：基于成分股权重

## 6. UI 层开发指南

### 6.1 新增页面

1. 在 `Views/` 目录下创建 XAML 和代码隐藏文件
2. 在 `ViewModels/` 目录下创建对应的 ViewModel
3. 使用 MVVM 模式，通过 DataContext 绑定
4. 使用 RelayCommand 处理命令
5. 更新设计文档

### 6.2 数据绑定

- 使用 `{Binding PropertyName}` 进行数据绑定
- 使用 `UpdateSourceTrigger=PropertyChanged` 实时更新
- 使用 `StringFormat` 格式化显示
- 使用转换器处理复杂转换

### 6.3 图表开发

使用 LiveChartsCore 绑定数据：

```csharp
// 创建数据系列
var series = new LineSeries<KlineData>
{
    Values = data,
    Stroke = new SolidColorPaint(SKColors.Black),
    Fill = null
};

// 更新绑定属性
KlineSeries = new ISeries[] { series };
RaisePropertyChanged(nameof(KlineSeries));
```

## 7. 调试指南

### 7.1 常用调试技巧

1. 使用 `Debug.WriteLine` 输出调试信息
2. 设置断点查看数据流转
3. 使用网络工具（如 Fiddler）查看 API 请求
4. 检查 SQLite 缓存数据库内容

### 7.2 常见问题

| 问题 | 可能原因 | 解决方法 |
| :--- | :--- | :--- |
| API 返回空数据 | 网络问题或接口变更 | 检查网络连接，更新 API 接口 |
| 缓存不生效 | 缓存键错误或过期时间设置 | 检查缓存键和过期时间 |
| UI 不更新 | 忘记调用 RaisePropertyChanged | 添加 RaisePropertyChanged 调用 |
| 图表不显示 | 数据格式错误 | 检查数据绑定和格式 |

## 8. 测试指南

### 8.1 单元测试

在项目中添加测试项目并编写单元测试：

- 测试数据模型的序列化和反序列化
- 测试 API 客户端的响应解析
- 测试缓存管理的读写操作
- 测试估值计算的准确性

### 8.2 集成测试

- 测试完整的数据流（API → 缓存 → UI）
- 测试数据源切换功能
- 测试离线模式

## 9. 部署指南

### 9.1 构建项目

```bash
dotnet restore --source https://api.nuget.org/v3/index.json
dotnet build --configuration Release --source https://api.nuget.org/v3/index.json
```

### 9.2 一键启动

双击运行 `start.bat` 脚本，自动完成：
- 环境检查
- 依赖还原
- 项目构建
- 应用启动

### 9.3 发布项目

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## 10. 后续开发建议

### 10.1 待实现功能

| 优先级 | 功能 | 说明 |
| :--- | :--- | :--- |
| 高 | 自选 ETF 列表 | 用户可以收藏常用 ETF |
| 高 | 估值趋势图 | 展示 PE/PB 历史走势 |
| 中 | 多 ETF 对比 | 对比不同 ETF 的估值指标 |
| 中 | 数据导出 | 将数据导出为 Excel/CSV |
| 低 | 暗黑模式 | 支持明暗主题切换 |
| 低 | 快捷键支持 | 键盘快捷键操作 |

### 10.2 技术改进

| 优先级 | 改进项 | 说明 |
| :--- | :--- | :--- |
| 高 | 依赖注入 | 使用 Microsoft.Extensions.DependencyInjection |
| 高 | 日志系统 | 添加结构化日志记录 |
| 中 | 错误重试 | API 请求失败自动重试 |
| 中 | 请求限流 | 避免高频请求被限流 |
| 低 | 性能优化 | 使用虚拟列表优化大数据量显示 |

## 11. 注意事项

1. **API 稳定性**：公开 API 可能随时变更，需要定期检查和更新
2. **数据准确性**：数据仅供参考，不构成投资建议
3. **网络安全**：使用 HTTPS，避免敏感信息泄露
4. **缓存管理**：定期清理过期缓存，避免占用过多磁盘空间
5. **用户体验**：保持界面简洁，响应迅速

## 12. 版本历史

| 版本 | 日期 | 修改内容 | 作者 |
| :--- | :--- | :--- | :--- |
| 1.0 | 2026-07-09 | 初始版本 | AI Assistant |
