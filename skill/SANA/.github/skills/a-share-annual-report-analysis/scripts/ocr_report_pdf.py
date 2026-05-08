from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
from io import BytesIO
from pathlib import Path
from tempfile import mkstemp

try:
    import fitz
    from PIL import Image
except ImportError as exc:  # pragma: no cover - runtime dependency check
    raise SystemExit(
        "Missing dependencies. Install them with: "
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
        description="OCR a scanned annual report PDF into page-level markdown text."
    )
    parser.add_argument("--pdf", required=True, help="Path to the source PDF file.")
    parser.add_argument("--out", required=True, help="Directory to write OCR files.")
    parser.add_argument("--start-page", type=int, default=1, help="1-based start page.")
    parser.add_argument("--end-page", type=int, help="1-based end page, inclusive.")
    parser.add_argument("--dpi", type=int, default=220, help="Rendering DPI for OCR.")
    parser.add_argument(
        "--lang",
        default="chi_sim+eng",
        help="Tesseract language code, default is chi_sim+eng.",
    )
    parser.add_argument(
        "--tesseract-cmd",
        help="Optional explicit path to tesseract executable.",
    )
    parser.add_argument(
        "--tessdata-dir",
        help="Optional explicit path to the Tesseract tessdata directory.",
    )
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


def main() -> None:
    args = parse_args()
    pdf_path = Path(args.pdf).expanduser().resolve()
    output_dir = Path(args.out).expanduser().resolve()

    if not pdf_path.exists():
        raise SystemExit(f"PDF not found: {pdf_path}")

    tesseract_cmd = str(Path(args.tesseract_cmd).expanduser().resolve()) if args.tesseract_cmd else "tesseract"
    tessdata_dir = (
        Path(args.tessdata_dir).expanduser().resolve() if args.tessdata_dir else None
    )

    keywords = [
        item.strip()
        for item in (args.keywords.split(",") if args.keywords else DEFAULT_KEYWORDS)
        if item.strip()
    ]
    matches: dict[str, list[int]] = {keyword: [] for keyword in keywords}
    output_dir.mkdir(parents=True, exist_ok=True)

    page_chunks: list[str] = []
    doc = fitz.open(pdf_path)
    try:
        start_page = max(args.start_page, 1)
        end_page = args.end_page or doc.page_count
        end_page = min(end_page, doc.page_count)
        zoom = args.dpi / 72.0
        matrix = fitz.Matrix(zoom, zoom)

        for page_number in range(start_page, end_page + 1):
            page = doc.load_page(page_number - 1)
            pixmap = page.get_pixmap(matrix=matrix, alpha=False)
            image = Image.open(BytesIO(pixmap.tobytes("png"))).convert("RGB")
            temp_fd, temp_path = mkstemp(suffix=".png")
            os.close(temp_fd)
            Path(temp_path).unlink(missing_ok=True)
            try:
                image.save(temp_path, format="PNG")
                command = [tesseract_cmd]
                if tessdata_dir:
                    command.extend(["--tessdata-dir", str(tessdata_dir)])
                command.extend([temp_path, "stdout", "-l", args.lang])
                result = subprocess.run(
                    command,
                    check=True,
                    capture_output=True,
                )
                text = normalize_text(result.stdout.decode("utf-8", errors="replace"))
            finally:
                Path(temp_path).unlink(missing_ok=True)
            page_chunks.append(f"## Page {page_number}\n\n{text or '[No OCR text extracted]'}")
            for keyword in keywords:
                if keyword in text:
                    matches[keyword].append(page_number)
    finally:
        doc.close()

    (output_dir / "ocr_text.md").write_text(
        "\n\n".join(page_chunks) + "\n", encoding="utf-8"
    )
    (output_dir / "matches.json").write_text(
        json.dumps(matches, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (output_dir / "summary.json").write_text(
        json.dumps(
            {
                "source_pdf": str(pdf_path),
                "dpi": args.dpi,
                "lang": args.lang,
                "keywords": keywords,
                "start_page": args.start_page,
                "end_page": args.end_page,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    print(f"OCR pages: {len(page_chunks)}")
    print(f"Output directory: {output_dir}")


if __name__ == "__main__":
    main()