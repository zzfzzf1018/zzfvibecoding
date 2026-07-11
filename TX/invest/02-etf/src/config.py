"""全局配置常量"""

# akshare 数据请求超时时间（秒）
REQUEST_TIMEOUT = 30

# 页面配置
PAGE_TITLE = "中国股市 ETF 查询工具"
PAGE_LAYOUT = "wide"

# 历史分位数时间窗口
PERCENTILE_WINDOWS = {
    "3年": 3,
    "5年": 5,
    "10年": 10,
    "20年": 20,
}

# K线图默认显示天数
KLINE_DEFAULT_DAYS = 365

# 日志格式
LOG_FORMAT = "%(asctime)s [%(levelname)s] %(name)s: %(message)s"
LOG_LEVEL = "INFO"
