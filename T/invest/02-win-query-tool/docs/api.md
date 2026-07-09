# API接口文档

## 基础信息

- **服务地址**: http://localhost:5000
- **协议**: HTTP
- **响应格式**: JSON

## 通用响应格式

### 成功响应

```json
{
    "success": true,
    "data": { ... },
    "cached": false,
    "warning": null
}
```

### 失败响应

```json
{
    "success": false,
    "error": "错误信息描述"
}
```

## 接口列表

### 1. 健康检查

**接口地址**: `GET /api/health`

**功能描述**: 检查服务状态和akshare可用性

**响应示例**:

```json
{
    "success": true,
    "akshare_available": true
}
```

---

### 2. 股票搜索

**接口地址**: `GET /api/stock/search`

**功能描述**: 搜索A股和港股股票

**请求参数**:

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| keyword | string | 是 | 股票代码或名称 |
| market | string | 否 | 市场类型，可选值：all/cn/hk，默认all |

**响应示例**:

```json
{
    "success": true,
    "data": [
        {
            "code": "600519",
            "name": "贵州茅台",
            "market": "cn",
            "full_code": "600519.SH"
        }
    ],
    "cached": false
}
```

**数据字段说明**:

| 字段名 | 类型 | 说明 |
|--------|------|------|
| code | string | 股票代码 |
| name | string | 股票名称 |
| market | string | 市场类型（cn/hk） |
| full_code | string | 完整股票代码（含交易所后缀） |

---

### 3. 财务报表

**接口地址**: `GET /api/stock/finance_report`

**功能描述**: 获取股票财务报表

**请求参数**:

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| symbol | string | 是 | 股票代码（如600519.SH） |
| type | string | 否 | 报表类型，可选值：balance/income/cash，默认balance |

**报表类型说明**:

| 类型 | 说明 |
|------|------|
| balance | 资产负债表 |
| income | 利润表 |
| cash | 现金流量表 |

**响应示例**:

```json
{
    "success": true,
    "columns": ["指标", "2023年", "2022年", "2021年"],
    "data": [
        ["总资产", "3210.00", "2980.00", "2750.00"],
        ["流动资产", "1850.00", "1720.00", "1580.00"]
    ],
    "cached": false
}
```

---

### 4. 公司分析

**接口地址**: `GET /api/stock/analysis`

**功能描述**: 获取股票分析数据

**请求参数**:

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| symbol | string | 是 | 股票代码（如600519.SH） |

**响应示例**:

```json
{
    "success": true,
    "data": {
        "valuation": {
            "pe": 25.5,
            "pb": 6.2,
            "ps": 8.5,
            "dividend_yield": 2.1
        },
        "financial_ratios": {
            "净资产收益率": 22.5,
            "总资产收益率": 15.8,
            "毛利率": 73.1
        }
    },
    "cached": false
}
```

**估值指标说明**:

| 指标 | 说明 |
|------|------|
| pe | 市盈率 |
| pb | 市净率 |
| ps | 市销率 |
| dividend_yield | 股息率（%） |

---

### 5. 下载财务报表

**接口地址**: `GET /api/stock/download_report`

**功能描述**: 下载财务报表为Excel文件

**请求参数**:

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| symbol | string | 是 | 股票代码 |
| type | string | 否 | 报表类型 |

**响应示例**:

```json
{
    "success": true,
    "filepath": "C:\\path\\to\\600519.SH_balance_20240101.xlsx",
    "filename": "600519.SH_balance_20240101.xlsx"
}
```

---

### 6. 招股书查询

**接口地址**: `GET /api/stock/prospectus`

**功能描述**: 查询招股书信息

**请求参数**:

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| symbol | string | 是 | 股票代码或名称 |

**响应示例**:

```json
{
    "success": true,
    "data": [
        {
            "code": "688981",
            "name": "中芯国际",
            "ipo_date": "2020-07-16",
            "prospectus_url": "https://example.com/prospectus.pdf"
        }
    ],
    "cached": false
}
```

---

### 7. 下载招股书

**接口地址**: `GET /api/stock/download_prospectus`

**功能描述**: 下载招股书PDF文件

**请求参数**:

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| url | string | 是 | 招股书PDF链接 |
| filename | string | 否 | 保存文件名 |

**响应示例**:

```json
{
    "success": true,
    "filepath": "C:\\path\\to\\中芯国际_招股书.pdf",
    "filename": "中芯国际_招股书.pdf"
}
```

---

### 8. 缓存统计

**接口地址**: `GET /api/cache/stats`

**功能描述**: 获取缓存统计信息

**响应示例**:

```json
{
    "success": true,
    "data": {
        "total": 10,
        "valid": 8,
        "expired": 2
    }
}
```

---

### 9. 清除缓存

**接口地址**: `POST /api/cache/clear`

**功能描述**: 清除所有缓存

**响应示例**:

```json
{
    "success": true,
    "message": "缓存已清除"
}
```

---

### 10. 清除过期缓存

**接口地址**: `POST /api/cache/clear_expired`

**功能描述**: 清除过期缓存（超过24小时）

**响应示例**:

```json
{
    "success": true,
    "message": "过期缓存已清除"
}
```

## 错误码说明

| 错误类型 | 说明 |
|----------|------|
| akshare未安装 | Python环境缺少akshare库 |
| 网络请求失败 | akshare无法获取数据 |
| 未找到匹配的股票 | 搜索结果为空 |
| 未获取到财务数据 | 财务报表获取失败 |
| 无效的symbol格式 | 股票代码格式不正确 |

## 缓存机制说明

### 缓存策略

- **缓存时间**: 24小时
- **缓存存储**: SQLite数据库
- **缓存键**: 接口名 + 参数的MD5哈希值

### 缓存流程

1. 请求到达时，先检查缓存
2. 如果缓存有效且未过期，返回缓存数据
3. 如果缓存无效或过期，调用akshare获取实时数据
4. 获取成功后更新缓存，返回实时数据
5. 获取失败时，如果有缓存则返回缓存数据，否则返回错误

### 缓存失效场景

- 缓存超过24小时
- 手动调用清除缓存接口
- 服务启动时自动清理过期缓存
