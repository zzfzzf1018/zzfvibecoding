# 中国股市 ETF 查询工具

基于 Streamlit + akshare 的 ETF 综合查询工具。

## 功能

- 🔍 **搜索**：支持 ETF 代码查询、名称模糊搜索
- 📋 **基本信息**：发行方、规模、费率、成立日期等
- 📊 **K 线图**：交互式 K 线（含成交量）
- 🏢 **成分股**：前十大持仓及比例
- 📈 **PE/PB 估值**：当前市盈率/市净率
- 📐 **历史分位数**：3 年/5 年/10 年/20 年 PE/PB 分位数
- 💰 **分红记录**：历史分红方案

## 一键启动

```bash
# Windows
双击运行 start.bat

# 或手动启动
pip install -r requirements.txt
streamlit run app.py
```

启动后访问 http://localhost:8501

## 项目结构

```
├── app.py                 # 主入口
├── start.bat              # 一键启动脚本
├── requirements.txt       # 依赖
├── src/
│   ├── config.py          # 全局配置
│   ├── api/
│   │   └── etf_data.py    # 数据获取层
│   ├── services/
│   │   ├── etf_service.py       # ETF 数据聚合
│   │   └── valuation_service.py # 估值计算
│   └── ui/
│       └── components.py  # UI 组件
└── docs/
    ├── architecture.md    # 架构设计
    ├── dev-constraints.md # 开发约束
    └── changelog.md       # 变更日志
```

## 数据源

数据来自东方财富、天天基金等公开金融数据接口（通过 akshare 封装）。
