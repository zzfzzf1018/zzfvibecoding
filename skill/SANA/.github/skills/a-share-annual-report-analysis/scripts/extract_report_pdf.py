from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path
from typing import Iterable

try:
    import pdfplumber
except ImportError as exc:  # pragma: no cover - runtime dependency check
    raise SystemExit(
        "Missing dependency 'pdfplumber'. Install it with: "
        "pip install -r .github/skills/a-share-annual-report-analysis/scripts/requirements.txt"
    ) from exc


DEFAULT_KEYWORDS = [
    "合并资产负债表",
    "合并利润表",
    "合并现金流量表",
    "管理层讨论与分析",
    "分产品",
    "分地区",
    "毛利率",
    "非经常性损益",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract text, tables, and keyword page matches from an annual report PDF."
    )
    parser.add_argument("--pdf", required=True, help="Path to the source PDF file.")
    parser.add_argument("--out", required=True, help="Directory to write extracted files.")
    parser.add_argument("--start-page", type=int, default=1, help="1-based start page.")
    parser.add_argument("--end-page", type=int, help="1-based end page, inclusive.")
    parser.add_argument(
        "--keywords",
        help="Comma-separated custom keywords. Defaults to annual-report sections.",
    )
    return parser.parse_args()


def normalize_text(value: str | None) -> str:
    if not value:
        return ""
    value = value.replace("\x00", "")
    value = re.sub(r"\r\n?", "\n", value)
    value = re.sub(r"[ \t]+", " ", value)
    value = re.sub(r"\n{3,}", "\n\n", value)
    return value.strip()


def normalize_cell(value: object) -> str:
    if value is None:
        return ""
    return normalize_text(str(value)).replace("\n", " ")


def write_text_markdown(output_path: Path, pages: Iterable[tuple[int, str]]) -> None:
    chunks: list[str] = []
    for page_number, text in pages:
        chunks.append(f"## Page {page_number}\n\n{text or '[No text extracted]'}")
    output_path.write_text("\n\n".join(chunks) + "\n", encoding="utf-8")


def write_csv(output_path: Path, table: list[list[object]]) -> None:
    with output_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        for row in table:
            writer.writerow([normalize_cell(cell) for cell in row])


def main() -> None:
    args = parse_args()
    pdf_path = Path(args.pdf).expanduser().resolve()
    output_dir = Path(args.out).expanduser().resolve()
    tables_dir = output_dir / "tables"

    if not pdf_path.exists():
        raise SystemExit(f"PDF not found: {pdf_path}")

    keywords = [
        item.strip()
        for item in (args.keywords.split(",") if args.keywords else DEFAULT_KEYWORDS)
        if item.strip()
    ]

    output_dir.mkdir(parents=True, exist_ok=True)
    tables_dir.mkdir(parents=True, exist_ok=True)

    matches: dict[str, list[int]] = {keyword: [] for keyword in keywords}
    text_pages: list[tuple[int, str]] = []
    table_index_lines = ["# Extracted Tables", ""]
    extracted_tables = 0

    with pdfplumber.open(pdf_path) as pdf:
        start_page = max(args.start_page, 1)
        end_page = args.end_page or len(pdf.pages)
        end_page = min(end_page, len(pdf.pages))

        for page_number in range(start_page, end_page + 1):
            page = pdf.pages[page_number - 1]
            text = normalize_text(page.extract_text() or "")
            text_pages.append((page_number, text))

            for keyword in keywords:
                if keyword in text:
                    matches[keyword].append(page_number)

            for table_number, table in enumerate(page.extract_tables() or [], start=1):
                if not table:
                    continue
                file_name = f"page-{page_number:04d}-table-{table_number:02d}.csv"
                csv_path = tables_dir / file_name
                write_csv(csv_path, table)
                extracted_tables += 1
                table_index_lines.append(
                    f"- Page {page_number}, Table {table_number}: `tables/{file_name}`"
                )

    write_text_markdown(output_dir / "full_text.md", text_pages)
    (output_dir / "table_index.md").write_text(
        "\n".join(table_index_lines) + "\n", encoding="utf-8"
    )
    (output_dir / "matches.json").write_text(
        json.dumps(matches, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (output_dir / "summary.json").write_text(
        json.dumps(
            {
                "source_pdf": str(pdf_path),
                "page_range": {"start": args.start_page, "end": args.end_page},
                "keywords": keywords,
                "pages_extracted": len(text_pages),
                "tables_extracted": extracted_tables,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    print(f"Extracted text pages: {len(text_pages)}")
    print(f"Extracted tables: {extracted_tables}")
    print(f"Output directory: {output_dir}")


if __name__ == "__main__":
    main()