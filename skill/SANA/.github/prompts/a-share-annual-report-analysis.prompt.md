---
name: "A股年报分析"
description: "输入公司名称、代码、报告期和财务/年报材料，按A股单季度口径输出三大表、管理层讨论、成本毛利、ROE、估值与同行对比分析。"
argument-hint: "例如：宁德时代 300750 2025年年报，附三大表与管理层讨论文本"
agent: "agent"
model: "GPT-5 (copilot)"
---

请对我提供的 A 股公司年报或季报材料做结构化分析。

执行要求：

1. 先识别公司、股票代码、报告期、行业，以及当前材料是否足以支持完整分析。
2. 若半年报或三季报使用累计口径，先拆分为单季度口径，再做同比和环比分析。
3. 必须覆盖以下模块：
   - 业绩总览
   - 分季度财务拆解
   - 三大表分析
   - 管理层讨论提炼
   - 收入结构与成本毛利分析
   - ROE 与杜邦拆解
   - 估值与同行对比
   - 风险与跟踪指标
   - 最终判断
4. 不要照抄原文。每个重要结论都要写清数据依据、经营含义，以及是否存在一次性因素。
5. 如果缺少估值、同行或 ROE 计算所需数据，明确列出缺口，不要编造。
6. 如果我明确说“摘要版”或“券商风格”，优先按短版纪要方式输出；如果我明确说“同行对比”，优先展开可比公司横向表格。

输出格式优先参考：
- [Skill 入口](../skills/a-share-annual-report-analysis/SKILL.md)
- [分析框架](../skills/a-share-annual-report-analysis/references/analysis-framework.md)
- [输出模板](../skills/a-share-annual-report-analysis/assets/output-template.md)
- [券商风格摘要模板](../skills/a-share-annual-report-analysis/assets/broker-summary-template.md)
- [同行对比模板](../skills/a-share-annual-report-analysis/assets/peer-comparison-template.md)
- [虚构案例](../skills/a-share-annual-report-analysis/references/example-case.md)