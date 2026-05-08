from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

try:
    import requests
except ImportError as exc:  # pragma: no cover - runtime dependency check
    raise SystemExit(
        "Missing dependency 'requests'. Install it with: "
        "pip install -r .github/skills/a-share-annual-report-analysis/scripts/requirements.txt"
    ) from exc


QUERY_URL = "https://www.cninfo.com.cn/new/hisAnnouncement/query"
DOWNLOAD_PREFIX = "https://static.cninfo.com.cn/"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Download annual report PDFs from CNINFO by stock code and year."
    )
    parser.add_argument("--code", required=True, help="A-share stock code, e.g. 300750.")
    parser.add_argument("--year", required=True, type=int, help="Report year, e.g. 2025.")
    parser.add_argument("--out", required=True, help="Directory to save downloaded PDFs.")
    parser.add_argument(
        "--save-metadata",
        action="store_true",
        help="Save raw query metadata JSON next to the PDF.",
    )
    return parser.parse_args()


def infer_market_fields(code: str) -> tuple[str, str]:
    if code.startswith(("600", "601", "603", "605", "688", "689")):
        return "sse", "sh"
    return "szse", "sz"


def build_session() -> requests.Session:
    session = requests.Session()
    session.headers.update(
        {
            "Accept": "application/json, text/plain, */*",
            "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
            "Origin": "https://www.cninfo.com.cn",
            "Referer": "https://www.cninfo.com.cn/new/commonUrl/pageOfSearch?url=disclosure/list/search",
            "User-Agent": (
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            ),
            "X-Requested-With": "XMLHttpRequest",
        }
    )
    return session


def choose_best_announcement(items: list[dict], code: str, year: int) -> dict:
    include_keywords = [
        f"{year}年年度报告",
        f"{year}年年度报告全文",
        f"{year}年报",
    ]
    exclude_keywords = ["摘要", "英文", "取消", "更正", "修订", "补充"]

    def score(item: dict) -> tuple[int, int]:
        title = item.get("announcementTitle", "")
        include_score = sum(keyword in title for keyword in include_keywords)
        exclude_score = sum(keyword in title for keyword in exclude_keywords)
        return (include_score, -exclude_score)

    filtered = [
        item
        for item in items
        if item.get("secCode") == code and score(item)[0] > 0
    ]
    if not filtered:
        available_titles = sorted(
            {
                item.get("announcementTitle", "")
                for item in items
                if item.get("secCode") == code and "年度报告" in item.get("announcementTitle", "")
            }
        )
        details = f" Available titles: {available_titles}" if available_titles else ""
        raise SystemExit(
            f"No likely annual report announcement found for code {code} and year {year}.{details}"
        )
    filtered.sort(key=score, reverse=True)
    return filtered[0]


def sanitize_filename(value: str) -> str:
    value = re.sub(r"[\\/:*?\"<>|]+", "_", value)
    value = re.sub(r"\s+", " ", value).strip()
    return value[:180]


def main() -> None:
    args = parse_args()
    code = args.code.strip()
    year = args.year
    out_dir = Path(args.out).expanduser().resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    column, plate = infer_market_fields(code)
    session = build_session()
    payload = {
        "pageNum": 1,
        "pageSize": 100,
        "column": column,
        "tabName": "fulltext",
        "plate": plate,
        "stock": "",
        "searchkey": code,
        "secid": "",
        "category": "category_ndbg_szsh",
        "trade": "",
        "seDate": f"{year}-01-01~{year + 1}-12-31",
        "sortName": "",
        "sortType": "",
        "isHLtitle": "true",
    }

    response = session.post(QUERY_URL, data=payload, timeout=30)
    response.raise_for_status()
    data = response.json()
    announcements = data.get("announcements") or []
    if not announcements:
        raise SystemExit("No announcements returned from CNINFO for the given code/year.")

    chosen = choose_best_announcement(announcements, code, year)
    adjunct_url = chosen.get("adjunctUrl")
    if not adjunct_url:
        raise SystemExit("Matched announcement has no downloadable PDF URL.")

    title = chosen.get("announcementTitle", f"{code}-{year}-annual-report")
    file_name = sanitize_filename(title) + ".pdf"
    pdf_path = out_dir / file_name

    download_url = adjunct_url if adjunct_url.startswith("http") else DOWNLOAD_PREFIX + adjunct_url.lstrip("/")
    pdf_response = session.get(download_url, timeout=60)
    pdf_response.raise_for_status()
    pdf_path.write_bytes(pdf_response.content)

    if args.save_metadata:
        metadata_path = out_dir / (sanitize_filename(title) + ".json")
        metadata_path.write_text(
            json.dumps(chosen, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )

    print(f"Downloaded: {pdf_path}")
    print(f"Title: {title}")


if __name__ == "__main__":
    main()