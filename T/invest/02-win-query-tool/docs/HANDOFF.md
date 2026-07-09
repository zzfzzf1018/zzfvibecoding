# 项目交接文档

## 项目概述

这是一个基于Electron的股票财务查询系统，用于查询A股和港股的财务数据、公司分析、财务报表下载、招股书下载等功能。

## 当前状态

### 已完成功能

| 功能模块 | 状态 | 说明 |
|----------|------|------|
| 一键启动 | ✅ 完成 | Electron自动启动Python服务并等待就绪 |
| 股票搜索 | ✅ 完成 | 支持A股和港股搜索，市场筛选 |
| 财务报表 | ✅ 完成 | 资产负债表、利润表、现金流量表 |
| 公司分析 | ✅ 完成 | 估值指标、财务比率分析 |
| 招股书下载 | ✅ 完成 | 招股书查询和PDF下载 |
| SQLite缓存 | ✅ 完成 | 24小时缓存，网络失败时返回缓存 |
| 缓存标记显示 | ✅ 完成 | 四个组件均已添加缓存标记 |
| 文档完善 | ✅ 完成 | README、API、架构、开发指南、组件说明 |

### 已知问题

#### 1. akshare网络不稳定

**问题描述**: akshare数据源连接不稳定，经常出现 `RemoteDisconnected('Remote end closed connection without response')` 错误。

**影响范围**: 所有依赖akshare的API接口（股票搜索、财务报表、公司分析、招股书）

**当前处理**: 已实现缓存机制，网络失败时返回缓存数据，无缓存时返回错误信息。

**建议**: 
- 考虑切换到更稳定的数据源（如tushare）
- 增加重试机制和超时设置
- 添加数据源健康监控

#### 2. 缓存统计API首次启动404

**问题描述**: 首次启动Python服务后，访问 `/api/cache/stats` 返回404，需要重启服务才能正常工作。

**影响范围**: 缓存管理功能

**当前处理**: 重启服务后正常

**建议**: 检查Flask路由注册顺序，确保缓存API在其他API之前注册

#### 3. Electron沙箱权限问题

**问题描述**: Electron默认userData目录在AppData，可能因权限问题导致沙箱错误（exit code -1073741819）

**当前处理**: 已通过 `app.setPath('userData', path.join(app.getAppPath(), 'user_data'))` 解决，将用户数据目录指向项目目录

**建议**: 保持当前方案，确保项目目录有读写权限

#### 4. akshare版本兼容性

**问题描述**: 早期使用 `akshare==1.11.64` 版本不存在，已改为 `akshare>=1.18.0`

**当前处理**: 在requirements.txt中使用宽松版本约束

**建议**: 定期更新akshare版本，测试兼容性

### 未完成事项

| 事项 | 优先级 | 说明 |
|------|--------|------|
| 数据源稳定性优化 | 高 | 当前akshare不稳定，需考虑备选方案 |
| 数据可视化图表 | 中 | 添加K线图、趋势图等可视化功能 |
| 投资组合管理 | 中 | 支持用户创建和管理投资组合 |
| 数据导出功能 | 中 | 支持PDF/CSV格式导出 |
| 多语言支持 | 低 | 支持中英文切换 |
| 性能优化 | 中 | 大数据量处理和渲染优化 |

## 关键文件路径

### 核心文件

| 文件路径 | 说明 |
|----------|------|
| `src/main/index.js` | Electron主进程，窗口管理和Python进程管理 |
| `src/python/app.py` | Flask API服务，所有业务逻辑 |
| `src/python/cache.py` | SQLite缓存模块 |
| `src/python/requirements.txt` | Python依赖 |
| `src/renderer/src/App.jsx` | React主应用组件 |
| `src/renderer/src/index.jsx` | React入口文件 |
| `src/renderer/package.json` | 前端依赖 |

### 组件文件

| 文件路径 | 说明 |
|----------|------|
| `src/renderer/src/components/StockSearch.jsx` | 股票搜索组件 |
| `src/renderer/src/components/FinanceReport.jsx` | 财务报表组件 |
| `src/renderer/src/components/CompanyAnalysis.jsx` | 公司分析组件 |
| `src/renderer/src/components/Prospectus.jsx` | 招股书组件 |

### 文档文件

| 文件路径 | 说明 |
|----------|------|
| `README.md` | 项目说明文档 |
| `docs/api.md` | API接口文档 |
| `docs/architecture.md` | 架构设计文档 |
| `docs/development.md` | 开发指南 |
| `docs/components.md` | 组件详细说明 |
| `docs/HANDOFF.md` | 项目交接文档 |

## 关键设计决策

### 1. 技术栈选择

**决策**: Electron + React + Flask + SQLite

**理由**:
- Electron: 跨平台桌面应用，使用HTML/CSS/JS开发
- React: 组件化开发，虚拟DOM，生态丰富
- Flask: 轻量级Python Web框架，易于开发
- SQLite: 嵌入式数据库，无需额外服务，适合桌面应用

**替代方案**:
- Electron + Vue + FastAPI + PostgreSQL
- 纯Python桌面应用（PyQt/Tkinter）

### 2. 进程通信设计

**决策**: Electron ↔ Flask使用HTTP RESTful API

**理由**:
- 简单易用，无需复杂的IPC机制
- Flask作为独立服务，可单独测试和部署
- 便于未来扩展为Web服务

**替代方案**:
- IPC通信
- WebSocket实时通信

### 3. 缓存策略设计

**决策**: SQLite本地缓存，24小时有效期

**理由**:
- 财务数据变化不频繁，24小时有效期足够
- SQLite轻量级，适合本地存储
- 支持离线查看，提升用户体验

**替代方案**:
- Redis缓存（需要额外服务）
- 文件缓存（JSON文件）

### 4. 错误处理设计

**决策**: 网络失败时返回缓存数据，无缓存时返回错误

**理由**:
- 提升用户体验，网络不稳定时仍可查看历史数据
- 明确告知用户数据来源（缓存标记）
- 避免使用模拟数据误导用户

**替代方案**:
- 使用模拟数据作为fallback
- 直接返回错误

## 启动方式

### 开发模式

```bash
# 安装依赖
pip install -r src/python/requirements.txt
cd src/renderer && npm install

# 构建前端
cd src/renderer && npm run build

# 启动Python服务（可选）
python src/python/app.py

# 启动Electron
npm start
```

### 一键启动（推荐）

```bash
npm start
```

## API接口列表

| 接口 | 方法 | 功能 |
|------|------|------|
| `/api/health` | GET | 健康检查 |
| `/api/stock/search` | GET | 股票搜索 |
| `/api/stock/finance_report` | GET | 财务报表 |
| `/api/stock/analysis` | GET | 公司分析 |
| `/api/stock/download_report` | GET | 下载报表 |
| `/api/stock/prospectus` | GET | 招股书查询 |
| `/api/stock/download_prospectus` | GET | 下载招股书 |
| `/api/cache/stats` | GET | 缓存统计 |
| `/api/cache/clear` | POST | 清除缓存 |
| `/api/cache/clear_expired` | POST | 清除过期缓存 |

## 缓存机制

### 工作流程

1. 请求到达时检查缓存
2. 缓存有效且未过期 → 返回缓存数据（cached: true）
3. 缓存无效或过期 → 调用akshare获取实时数据
4. 获取成功 → 更新缓存，返回新数据（cached: false）
5. 获取失败 → 有缓存返回缓存数据，无缓存返回错误

### 缓存配置

- 缓存有效期: 24小时
- 缓存路径: `src/python/cache/stock_data.db`
- 自动清理: 服务启动时清理过期缓存

## 注意事项

### 环境要求

- Node.js >= 18.x
- Python >= 3.10
- Windows 10/11

### 网络要求

- 需要网络连接获取实时数据
- 网络不稳定时使用缓存数据
- 完全离线时只能查看已缓存的数据

### 权限要求

- 项目目录需要读写权限（缓存数据库和下载文件）
- 首次启动时需要确认用户数据目录权限

### 数据更新

- 财务报表按季度更新
- 招股书数据按IPO时间更新
- 缓存数据24小时后自动失效

## 后续开发建议

### 短期（1-2周）

1. 优化akshare数据获取的稳定性
2. 添加请求重试机制
3. 完善错误日志记录
4. 测试各功能模块的边界情况

### 中期（1-2月）

1. 添加数据可视化图表
2. 实现投资组合管理功能
3. 添加数据导出功能（PDF/CSV）
4. 优化性能（大数据量处理）

### 长期（3-6月）

1. 支持多语言切换
2. 添加用户认证和数据同步
3. 扩展更多数据源
4. 支持移动端访问

## 联系方式

如有问题或需要帮助，请联系项目负责人。
