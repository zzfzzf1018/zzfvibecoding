# PDF 输入指南

这份指南用于处理本地 PDF 年报、季报或公告，目标是先把 PDF 转成适合分析的文字和表格，再交给年报分析 skill。

## 1. 什么时候适合自动抽取

以下场景适合直接用脚本：

- PDF 可以选中文本
- 年报页数较多，手工定位三大表很慢
- 需要先提取表格 CSV，再做分季度拆分和横向分析

以下场景建议先 OCR：

- 扫描版 PDF
- 图片型 PDF
- 文字复制后出现大量乱码

## 2. 脚本会输出什么

运行 [PDF 抽取脚本](../scripts/extract_report_pdf.py) 后，会生成：

- `full_text.md`：按页抽取的全文文字
- `table_index.md`：抽出的表格文件索引
- `matches.json`：关键字命中的页码
- `tables/*.csv`：每张表单独导出的 CSV

默认关键字会覆盖这些常见章节：

- 合并资产负债表
- 合并利润表
- 合并现金流量表
- 管理层讨论与分析
- 分产品
- 分地区
- 毛利率
- 非经常性损益

## 3. 推荐工作流

1. 用脚本先抽 PDF。
2. 先看 `matches.json`，快速定位三大表和管理层讨论页码。
3. 打开 `table_index.md` 和 `tables/*.csv`，优先检查三大表、主营分部、毛利率表。
4. 把关键表和关键段落喂给年报分析 skill。

## 4. 建议命令

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/extract_report_pdf.py \
  --pdf "C:\reports\company-2025-annual-report.pdf" \
  --out ".\tmp\company-2025-report"
```

如果只想抓一部分页：

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/extract_report_pdf.py \
  --pdf "C:\reports\company-2025-annual-report.pdf" \
  --out ".\tmp\company-2025-report" \
  --start-page 120 \
  --end-page 220
```

## 5. 依赖安装

脚本依赖写在 [requirements.txt](../scripts/requirements.txt)。

```powershell
pip install -r .github/skills/a-share-annual-report-analysis/scripts/requirements.txt
```

## 6. 使用后怎么喂给分析 skill

优先把这些内容提供给 skill：

- `matches.json` 里对应的关键页
- 三大表 CSV
- 分产品/分地区收入和毛利率表
- 管理层讨论与分析相关页文字

不要一上来把几百页全文都贴进去。优先给关键表和关键段落，分析质量通常更高。

## 7. 局限与复核建议

- 表格跨页时，可能会被拆成多张 CSV。
- 有些 PDF 的表格边框不规则，表头可能抽歪。
- 单位、币种、是否合并报表，仍建议人工确认一次。
- 涉及 ROE、估值、单季度拆分时，建议对核心数字做一次抽样复核。
