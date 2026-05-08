# 年报下载指南

这份指南用于在只有股票代码和年份时，先自动查询并下载年报 PDF，再交给 PDF 抽取或 OCR 脚本处理。

## 1. 适用场景

- 你只有股票代码，如 `300750`
- 你知道年份，但手头没有 PDF 文件
- 你想先批量下载再统一抽取

## 2. 脚本会做什么

运行 [年报下载脚本](../scripts/download_cninfo_report.py) 后，会：

1. 根据股票代码判断市场
2. 调用巨潮公告查询接口
3. 过滤指定年份的年度报告公告
4. 把 PDF 下载到本地目录

## 3. 推荐命令

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/download_cninfo_report.py \
  --code 300750 \
  --year 2025 \
  --out ".\tmp\reports"
```

如果想保留查询结果 JSON：

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/download_cninfo_report.py \
  --code 300750 \
  --year 2025 \
  --out ".\tmp\reports" \
  --save-metadata
```

## 4. 过滤逻辑

脚本优先保留标题中包含这些关键词的公告：

- 年度报告
- 年报

同时尽量排除：

- 摘要
- 英文版
- 更正后
- 已取消

## 5. 注意事项

- 公告站点的接口、反爬规则、命名方式可能变化。
- 同一公司同一年可能有“年报”“年报摘要”“修订版”等多个版本，脚本会优先选主年报 PDF，但仍建议人工看一眼标题。
- 如果自动下载失败，可以回退到手工下载 PDF 或手工提供公告链接。

## 6. 下载后怎么接下一步

下载完成后：

1. 文本型 PDF：用 [PDF 抽取脚本](../scripts/extract_report_pdf.py)
2. 扫描版 PDF：用 [PDF OCR 脚本](../scripts/ocr_report_pdf.py)

这样就能把“股票代码 -> PDF -> 可分析文本”的流程连起来。
