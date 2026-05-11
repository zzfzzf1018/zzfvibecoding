# PDF OCR 指南

这份指南用于处理扫描版 PDF、图片型 PDF 或文字抽取质量很差的财报文件。

## 1. 什么时候要 OCR

以下情况优先 OCR：

- PDF 无法选中文本
- 复制出来是乱码或大段缺字
- 文字抽取后几乎找不到“三大表”“管理层讨论”等关键词

## 2. 脚本会输出什么

运行 [PDF OCR 脚本](../scripts/ocr_report_pdf.py) 后，会生成：

- `ocr_text.md`：按页 OCR 的全文文字
- `matches.json`：关键字命中的页码
- `summary.json`：页数、关键字、OCR 配置摘要

## 3. 推荐命令

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/ocr_report_pdf.py \
  --pdf "C:\reports\company-2025-annual-report.pdf" \
  --out ".\tmp\company-2025-ocr"
```

如果只做部分页：

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/ocr_report_pdf.py \
  --pdf "C:\reports\company-2025-annual-report.pdf" \
  --out ".\tmp\company-2025-ocr" \
  --start-page 100 \
  --end-page 220
```

## 4. Tesseract 要求

这个脚本依赖系统已安装 Tesseract OCR，并且命令 `tesseract` 可执行。

如果你的 Tesseract 不在 PATH，可以手动指定：

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/ocr_report_pdf.py \
  --pdf "C:\reports\company-2025-annual-report.pdf" \
  --out ".\tmp\company-2025-ocr" \
  --tesseract-cmd "C:\Program Files\Tesseract-OCR\tesseract.exe"
```

如果你使用的是自定义语言包目录，或系统默认 `tessdata` 不完整，可以显式指定：

```powershell
python .github/skills/a-share-annual-report-analysis/scripts/ocr_report_pdf.py \
  --pdf "C:\reports\company-2025-annual-report.pdf" \
  --out ".\tmp\company-2025-ocr" \
  --tesseract-cmd "C:\Program Files\Tesseract-OCR\tesseract.exe" \
  --tessdata-dir ".\tmp\tessdata"
```

## 5. 推荐语言

默认使用 `chi_sim+eng`。如果系统里没有中文语言包，可以临时改成 `eng`，但中文识别质量会明显下降。

如果 `chi_sim` 已经单独下载到本地目录，需要与 `--tessdata-dir` 一起使用。

## 6. 使用建议

OCR 完成后，优先检查：

- 三大表页附近数字是否明显错位
- 页眉页脚是否干扰正文
- “管理层讨论与分析”页是否命中成功

OCR 更适合先定位关键页和提取段落，不适合盲目相信所有表格数字。

## 7. 局限

- 低分辨率扫描件容易把数字 8/3、6/0、1/7 识别错。
- 表格结构不会自动还原成高质量 CSV。
- 图片、印章、斜排文本会影响识别效果。
