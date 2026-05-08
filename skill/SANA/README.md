# SANA

这是一个面向 A 股上市公司年报与季报分析的 Copilot 自定义工作区，核心目标是让分析过程从“摘抄财报”变成“结构化投研判断”。

## 已包含内容

- Skill 入口：[.github/skills/a-share-annual-report-analysis/SKILL.md](.github/skills/a-share-annual-report-analysis/SKILL.md)
- 分析框架：[.github/skills/a-share-annual-report-analysis/references/analysis-framework.md](.github/skills/a-share-annual-report-analysis/references/analysis-framework.md)
- 输出模板：[.github/skills/a-share-annual-report-analysis/assets/output-template.md](.github/skills/a-share-annual-report-analysis/assets/output-template.md)
- 券商风格摘要模板：[.github/skills/a-share-annual-report-analysis/assets/broker-summary-template.md](.github/skills/a-share-annual-report-analysis/assets/broker-summary-template.md)
- 买方投研纪要模板：[.github/skills/a-share-annual-report-analysis/assets/buy-side-memo-template.md](.github/skills/a-share-annual-report-analysis/assets/buy-side-memo-template.md)
- 行业化同行对比模板：[.github/skills/a-share-annual-report-analysis/assets/peer-comparison-template.md](.github/skills/a-share-annual-report-analysis/assets/peer-comparison-template.md)
- 示例案例：[.github/skills/a-share-annual-report-analysis/references/example-case.md](.github/skills/a-share-annual-report-analysis/references/example-case.md)
- 预期差案例示例：[.github/skills/a-share-annual-report-analysis/references/buy-side-expectation-gap-case.md](.github/skills/a-share-annual-report-analysis/references/buy-side-expectation-gap-case.md)
- 制造业买方案例：[.github/skills/a-share-annual-report-analysis/references/buy-side-manufacturing-case.md](.github/skills/a-share-annual-report-analysis/references/buy-side-manufacturing-case.md)
- 消费买方案例：[.github/skills/a-share-annual-report-analysis/references/buy-side-consumer-case.md](.github/skills/a-share-annual-report-analysis/references/buy-side-consumer-case.md)
- 医药买方案例：[.github/skills/a-share-annual-report-analysis/references/buy-side-healthcare-case.md](.github/skills/a-share-annual-report-analysis/references/buy-side-healthcare-case.md)
- PDF 输入指南：[.github/skills/a-share-annual-report-analysis/references/pdf-input-guide.md](.github/skills/a-share-annual-report-analysis/references/pdf-input-guide.md)
- PDF 抽取脚本：[.github/skills/a-share-annual-report-analysis/scripts/extract_report_pdf.py](.github/skills/a-share-annual-report-analysis/scripts/extract_report_pdf.py)
- PDF OCR 指南：[.github/skills/a-share-annual-report-analysis/references/pdf-ocr-guide.md](.github/skills/a-share-annual-report-analysis/references/pdf-ocr-guide.md)
- PDF OCR 脚本：[.github/skills/a-share-annual-report-analysis/scripts/ocr_report_pdf.py](.github/skills/a-share-annual-report-analysis/scripts/ocr_report_pdf.py)
- 年报下载指南：[.github/skills/a-share-annual-report-analysis/references/report-download-guide.md](.github/skills/a-share-annual-report-analysis/references/report-download-guide.md)
- 年报下载脚本：[.github/skills/a-share-annual-report-analysis/scripts/download_cninfo_report.py](.github/skills/a-share-annual-report-analysis/scripts/download_cninfo_report.py)
- 快速调用 Prompt：[.github/prompts/a-share-annual-report-analysis.prompt.md](.github/prompts/a-share-annual-report-analysis.prompt.md)

## 能分析什么

- 分季度财务拆解，尤其适配 A 股半年报、三季报累计口径转单季度口径
- 三大表联动分析：利润表、资产负债表、现金流量表
- 管理层讨论与分析提炼，并和财务数据做交叉验证
- 收入结构、成本结构、毛利率与费用率变化
- 业绩质量、现金流匹配度、资产质量和风险提示
- ROE 与杜邦拆解
- 估值与同行对比

## 使用方式

### 方式一：直接调用 Skill

在 VS Code Chat 中输入 `/`，选择 `a-share-annual-report-analysis`，再提供：

- 公司名称
- 股票代码
- 报告期
- 年报、季报、财务表格、管理层讨论文本等材料

### 方式二：直接调用 Prompt

在 VS Code Chat 中输入 `/`，选择 `A股年报分析`，然后直接粘贴你的材料。

Prompt 会要求模型按固定模块输出，包括：

- 业绩总览
- 分季度拆解
- 三大表分析
- 管理层讨论提炼
- 收入结构与成本毛利
- ROE 与杜邦拆解
- 估值与同行对比
- 风险与最终判断

如果你只想要适合晨会、路演纪要或卖方点评的短版结果，可以直接要求“券商风格摘要版”；如果你重点想看横向比较，可以直接要求“同行对比版”。
如果你更关心市场是否低估、核心预期差在哪里、什么因素会触发重估，可以直接要求“买方投研纪要版”。

## 建议输入材料

优先级从高到低：

1. 最近三年年报和最近四个季度季报
2. 整理好的三大表与附注关键数据
3. 管理层讨论与分析、分产品分地区收入、毛利率表
4. 市值、股价、总股本、同行公司列表

## PDF 自动抽取

如果你拿到的是本地 PDF 年报，可以先用脚本自动抽取：

1. 文字内容
2. 页面级关键字命中位置
3. PDF 中能识别出的表格 CSV

优先查看：[.github/skills/a-share-annual-report-analysis/references/pdf-input-guide.md](.github/skills/a-share-annual-report-analysis/references/pdf-input-guide.md)

脚本位置：

- [.github/skills/a-share-annual-report-analysis/scripts/extract_report_pdf.py](.github/skills/a-share-annual-report-analysis/scripts/extract_report_pdf.py)

适用场景：

- 文本型 PDF 年报
- 需要先定位三大表、管理层讨论、分产品分地区表页码
- 需要把 PDF 内容转成更方便喂给 Copilot 的 Markdown 和 CSV

限制：

- 扫描版 PDF 仍建议先 OCR
- 复杂跨页表格可能需要手工复核
- 图片、图表和图注不会完整还原成结构化数据

## 扫描版 PDF OCR

如果 PDF 是扫描版，或复制出来的文字严重乱码，可以先走 OCR：

1. 把 PDF 渲染成页面图像
2. 对每一页做 OCR 识别
3. 输出按页文本和关键字命中页码

优先查看：[.github/skills/a-share-annual-report-analysis/references/pdf-ocr-guide.md](.github/skills/a-share-annual-report-analysis/references/pdf-ocr-guide.md)

## 按股票代码自动下载年报

如果你手头没有 PDF，只有股票代码和年份，可以先尝试自动下载：

1. 按股票代码和年份查询巨潮公告列表
2. 过滤“年度报告”相关公告
3. 自动下载 PDF 到本地目录

优先查看：[.github/skills/a-share-annual-report-analysis/references/report-download-guide.md](.github/skills/a-share-annual-report-analysis/references/report-download-guide.md)

## 注意事项

- 不提供的数据不会被编造，缺口会被单独标注。
- 半年报和三季报通常要先拆成单季度，不能直接拿累计值比较环比。
- 估值与同行对比依赖市场数据，若未提供则只能做定性判断。
- 制造业、消费、医药的横向比较指标差异很大，同行对比模板已按行业拆开，不建议混用一套表头。
- PDF 自动抽取能明显减少手工整理，但抽取后的数字和表格仍建议抽样复核。
- OCR 对扫描质量敏感，低清晰度 PDF 的数字仍可能识别错。
- 自动下载依赖公告站点接口和命名规则，若下载失败可回退到手工提供 PDF 或公告链接。

## 后续可扩展方向

- 接入 A 股财务数据抓取脚本
- 增加更偏量化的因子与横向打分框架
