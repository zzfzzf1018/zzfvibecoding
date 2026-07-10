"""AkShare 数据源适配器（主源）。

实现 datasource.interfaces 的端口。具体取数逻辑按 docs/数据源调研.md 调用 AkShare。
akshare 采用函数内懒加载，避免无网络环境下模块导入失败。
本文件除端口实现外，严禁被 services/api 直接 import（见 docs/AI开发约束.md §5）。
"""
from __future__ import annotations

import re
from datetime import date, datetime

from app.core.logging import logger
from app.models.schemas import ConstituentStock, EtfBasic, IndexValuation, IndexValuationPoint


# ---------- 解析辅助 ----------
def _pick(cols, candidates):
    """在列名中按候选（忽略大小写/子串）挑选匹配列。"""
    lowered = {str(c).lower(): c for c in cols}
    for cand in candidates:
        if cand.lower() in lowered:
            return lowered[cand.lower()]
    for cand in candidates:
        for low, orig in lowered.items():
            if cand.lower() in low:
                return orig
    return None


def _to_float(s):
    if s is None:
        return None
    s = str(s).replace("%", "").replace(",", "").strip()
    if s in ("", "-", "None", "nan"):
        return None
    try:
        return float(s)
    except ValueError:
        return None


def _scale(s):
    if s is None:
        return None
    s = str(s).strip()
    m = re.search(r"[\d.]+", s)
    if not m:
        return None
    num = float(m.group())
    if "亿" in s:
        return num * 1e8
    if "万" in s:
        return num * 1e4
    return num


def _to_date(s):
    if s is None:
        return None
    s = str(s).strip()
    for fmt in ("%Y-%m-%d", "%Y/%m/%d", "%Y-%m-%d %H:%M:%S"):
        try:
            return datetime.strptime(s, fmt).date()
        except ValueError:
            continue
    return None


def _map_type(t):
    if not t:
        return None
    t = str(t)
    if "债券" in t:
        return "债券"
    if "货币" in t:
        return "货币"
    if "商品" in t or "黄金" in t:
        return "商品"
    if "QDII" in t or "跨境" in t:
        return "跨境"
    if "策略" in t:
        return "策略"
    if "主题" in t:
        return "主题"
    if "行业" in t:
        return "行业"
    if "指数" in t:
        return "宽基"
    return None


def _map_exchange(code):
    if not code:
        return None
    if code.startswith(("51", "58")):
        return "SSE"
    if code.startswith(("15", "16")):
        return "SZSE"
    return None


# ---------- 端口实现 ----------
class AkShareEtfBasicSource:
    name = "akshare"

    def list_etfs(self) -> list[EtfBasic]:
        import akshare as ak

        df = ak.fund_etf_category_sina(symbol="ETF基金")
        code_col = _pick(df.columns, ["代码", "code"])
        name_col = _pick(df.columns, ["名称", "name"])
        out: list[EtfBasic] = []
        for _, r in df.iterrows():
            code = str(r[code_col]).strip().zfill(6) if code_col else None
            if not code or code == "000000":
                continue
            name = str(r[name_col]).strip() if name_col else code
            out.append(EtfBasic(code=code, name=name, type=None, exchange=_map_exchange(code)))
        return out

    def get_etf(self, code: str) -> EtfBasic | None:
        import akshare as ak

        df = ak.fund_etf_info_em(symbol=code)
        cols = list(df.columns)
        kcol, vcol = cols[0], cols[1]
        info = dict(zip(df[kcol], df[vcol]))
        track_name = next(
            (v for k, v in info.items() if "跟踪" in str(k) and "代码" not in str(k)), None
        )
        track_code = next(
            (
                v
                for k, v in info.items()
                if ("跟踪" in str(k) and "代码" in str(k)) or "指数代码" in str(k)
            ),
            None,
        )
        return EtfBasic(
            code=code,
            name=str(info.get("基金全称") or info.get("基金简称") or code),
            short_name=info.get("基金简称"),
            type=_map_type(info.get("基金类型")),
            track_index=str(track_name) if track_name else None,
            track_index_code=str(track_code).strip() if track_code else None,
            fund_manager=info.get("基金管理人"),
            custodian=info.get("基金托管人"),
            fund_scale=_scale(info.get("资产规模")),
            shares=_scale(info.get("份额规模")),
            establish_date=_to_date(info.get("成立日")),
            manager=info.get("基金经理"),
            management_fee_rate=_to_float(info.get("管理费率")),
            custody_fee_rate=_to_float(info.get("托管费率")),
            tracking_error=_to_float(info.get("跟踪误差")),
            exchange=_map_exchange(code),
            update_time=datetime.now(),
        )


class AkShareValuationSource:
    name = "akshare"

    def get_latest(self, index_code: str) -> IndexValuation | None:
        import akshare as ak

        df = ak.stock_index_value_name_em()
        code_col = _pick(df.columns, ["指数代码", "code"])
        pe_col = _pick(df.columns, ["市盈率", "PE"])
        pb_col = _pick(df.columns, ["市净率", "PB"])
        dy_col = _pick(df.columns, ["股息率", "股息"])
        date_col = _pick(df.columns, ["日期", "date"])
        if code_col is None:
            return None
        row = df[df[code_col].astype(str).str.strip() == str(index_code)]
        if row.empty:
            return None
        r = row.iloc[0]
        return IndexValuation(
            index_code=index_code,
            pe_ttm=_to_float(r[pe_col]) if pe_col else None,
            pb=_to_float(r[pb_col]) if pb_col else None,
            dividend_yield=_to_float(r[dy_col]) if dy_col else None,
            valuation_date=_to_date(r[date_col]) if date_col else date.today(),
            source="akshare",
        )

    def get_history(self, index_code: str, start: date, end: date) -> list[IndexValuationPoint]:
        # 历史估值由调度任务自采落库（见 docs/数据源调研.md §4）；
        # AkShare 无稳定全量历史估值接口，此处返回空，由 DB 历史序列支撑。
        logger.debug("AkShare 不提供指数历史估值全量接口，使用自采历史")
        return []


class AkShareConstituentSource:
    name = "akshare"

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        import akshare as ak

        df = ak.index_stock_cons_csindex(symbol=index_code)
        code_col = _pick(df.columns, ["股票代码", "代码"])
        name_col = _pick(df.columns, ["股票名称", "名称"])
        w_col = _pick(df.columns, ["占指数净值比例", "权重", "weight"])
        out: list[ConstituentStock] = []
        for _, r in df.iterrows():
            sc = str(r[code_col]).strip().zfill(6) if code_col else None
            if not sc:
                continue
            out.append(
                ConstituentStock(
                    stock_code=sc,
                    stock_name=str(r[name_col]).strip() if name_col else None,
                    sw_l1=self._get_industry(sc),
                    weight=_to_float(r[w_col]) if w_col else None,
                    exchange=_map_exchange(sc),
                )
            )
        return out

    @staticmethod
    def _get_industry(stock_code: str) -> str | None:
        try:
            import akshare as ak

            df = ak.stock_individual_info_em(symbol=stock_code)
            cols = list(df.columns)
            info = dict(zip(df[cols[0]], df[cols[1]]))
            return info.get("行业")
        except Exception as exc:  # noqa: BLE001
            logger.warning("获取 %s 行业失败: %s", stock_code, exc)
            return None


__all__ = [
    "AkShareEtfBasicSource",
    "AkShareValuationSource",
    "AkShareConstituentSource",
]
