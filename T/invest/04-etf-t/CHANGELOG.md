# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-07-09

### Added

- 实现多数据源架构，支持模拟数据、新浪财经、东方财富、腾讯财经四种数据源
- 创建数据源抽象接口 (DataSource) 和本地缓存模块 (LocalCache)
- 实现新浪财经数据源 (SinaDataSource)，通过真实API获取35只ETF实时行情
- 实现东方财富数据源 (EastMoneyDataSource)，通过真实API获取最多100只ETF数据
- 实现腾讯财经数据源 (TencentDataSource)，通过真实API获取42只ETF实时行情（推荐）
- 创建数据源切换组件 (DataSourceSelector)，支持在头部切换数据源，显示真实/mock标识
- 实现查询数据的本地缓存功能，缓存时间30分钟
- 支持手动刷新缓存功能
- 数据源选择状态持久化到localStorage
- 配置Vite代理解决CORS问题

### Changed

- 更新ETF API层，支持多数据源切换
- 更新Header组件，添加数据源选择器
- 更新MockDataSource，添加缓存支持
- 更新README.md，添加数据源说明

## [1.0.0] - 2026-07-09

### Added

- 初始化项目结构，使用React 18 + TypeScript + Vite 6
- 创建产品需求文档 (.trae/documents/prd.md)
- 创建技术架构文档 (.trae/documents/tech-arch.md)
- 实现ETF列表页面 (ETFListPage.tsx)
- 实现ETF详情页面 (ETFDetailPage.tsx)
- 创建Header组件，包含搜索功能
- 创建CategoryFilter组件，支持按类型筛选
- 创建ETFCard组件，展示ETF卡片信息
- 创建ETFHeader组件，详情页头部和标签切换
- 创建BasicInfo组件，展示ETF基本信息
- 创建Holdings组件，展示前十大重仓股
- 创建Fees组件，展示费率信息
- 创建Dividends组件，展示分红记录
- 创建Valuation组件，展示估值指标
- 创建QuantileChart组件，PE/PB走势图
- 创建Quantiles组件，历史分位数数据
- 实现useETF自定义Hook
- 实现ETF API接口 (mock数据)
- 添加16只ETF的mock数据列表
- 添加6只ETF的详细mock数据（510050, 510300, 510500, 159919, 512880, 513100）
- 创建一键启动脚本 (start.bat)
- 创建README.md文档

### Fixed

- 修复TypeScript配置问题 (moduleResolution, allowSyntheticDefaultImports)
- 修复main.tsx导入方式
- 修复QuantileChart组件类型定义
- 修复未使用的导入
- 修复CSS @import位置问题

### Changed

- 更新package.json依赖版本，解决vite版本兼容性问题
- 更新vite版本为6.4.3
- 更新@vitejs/plugin-react版本为4.3.0

## [Unreleased]

### Planned

- 添加用户收藏功能
- 添加ETF对比功能