"""AkShare 数据源适配器（主源）。

实现 datasource.interfaces 的端口。具体取数逻辑按 docs/数据源调研.md 调用 AkShare。
akshare 采用函数内懒加载，避免无网络环境下模块导入失败。
本文件除端口实现外，严禁被 services/api 直接 import（见 docs/AI开发约束.md §5）。

> 注：akshare 1.x 多次移除/重命名接口，本文件所用接口均经实际联网探测验证
> （见 docs/tasks/task-02.md 附录）。如某接口再次失效，应定位根因更换接口，
> 而非用 try/except 静默吞掉（见 docs/AI开发约束.md §9）。
"""
from __future__ import annotations

import re
import unicodedata
from datetime import date, datetime

from app.core.logging import logger
from app.datasource._runner import run_df
from app.models.schemas import ConstituentStock, EtfBasic, IndexValuation, IndexValuationPoint

# ---------------------------------------------------------------------------
# 指数「名 -> 代码」常用映射（兜底）。运行时若 `index_code_id_map_em` 可用会进一步增强。
# 取值为交易所指数代码（上交所 000/000 开头、深交所 399 开头等）。
# ---------------------------------------------------------------------------
_COMMON_INDEX_MAP = {
    "沪深300": "000300", "中证500": "000905", "中证1000": "000852",
    "创业板指": "399006", "创业板": "399006", "科创50": "000688",
    "上证50": "000016", "上证180": "000010", "深证成指": "399001",
    "深证100": "399330", "沪深300医药": "000913", "中证白酒": "399997",
    "中证消费": "000932", "中证银行": "399986", "证券公司": "399975",
    "中证军工": "399967", "中证传媒": "399971", "中证煤炭": "399998",
    "中证环保": "000827", "中证红利": "000922", "中证电子": "399811",
    "中证医疗": "399989", "新能源车": "399976", "国证芯片": "980017",
    "5G通信": "931079", "中证基建": "399995", "中证证保": "399986",
    "中证传媒": "399971", "央视50": "399550", "基本面50": "000925",
    "中证100": "000903", "沪深300价值": "000919", "中证红利低波": "930955",
}

# 运行时从 eastmoney 拉取的全量 名->码 映射（进程内缓存，拉取失败则为空）。
_INDEX_CODE_MAP_CACHE: dict[str, str] | None = None


# ---------- 解析辅助 ----------
def _norm_key(s: str) -> str:
    """归一化：NFKC 把全角数字/字母转半角，并小写，便于列名稳定匹配。"""
    return unicodedata.normalize("NFKC", str(s)).lower()


def _pick(cols, candidates):
    """在列名中按候选（忽略大小写/全半角/子串）挑选匹配列。"""
    lowered = {_norm_key(c): c for c in cols}
    for cand in candidates:
        k = _norm_key(cand)
        if k in lowered:
            return lowered[k]
    for cand in candidates:
        k = _norm_key(cand)
        for low, orig in lowered.items():
            if k in low:
                return orig
    return None


def _to_float(s):
    """提取首个浮点数；兼容 '0.15%/年'、'14.79'、'—'、空等。"""
    if s is None:
        return None
    s = str(s).strip()
    if s in ("", "-", "None", "nan", "---", "—"):
        return None
    m = re.search(r"-?\d+(?:\.\d+)?", s)
    if not m:
        return None
    try:
        return float(m.group())
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
    if not s or s in ("None", "nan", "NaT", "NaN"):
        return None
    # 子进程桥接以 ISO 8601 序列化日期（如 '2005-04-08T00:00:00.000'），
    # 优先用 fromisoformat 解析，兼容带 'T'/毫秒/时区的形式。
    try:
        return datetime.fromisoformat(s.replace("Z", "+00:00")).date()
    except ValueError:
        pass
    for fmt in ("%Y-%m-%d", "%Y/%m/%d", "%Y-%m-%d %H:%M:%S", "%Y%m%d"):
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


def _norm_index_name(name: str | None) -> str | None:
    """把 '沪深300指数' -> '沪深300'，便于喂给 stock_index_pe_lg/pb_lg。"""
    if not name:
        return None
    n = str(name).strip()
    for suffix in ("指数", "ETF", "etf"):
        if n.endswith(suffix):
            n = n[: -len(suffix)]
    return n.strip() or None


def _runtime_index_code_map() -> dict[str, str]:
    """尽力从 eastmoney 拉全量 名->码 映射（进程内缓存；失败返回空）。"""
    global _INDEX_CODE_MAP_CACHE
    if _INDEX_CODE_MAP_CACHE is not None:
        return _INDEX_CODE_MAP_CACHE
    out: dict[str, str] = {}
    try:
        df = run_df("index_code_id_map_em")
        name_col = _pick(df.columns, ["名称", "name", "指数名称"])
        code_col = _pick(df.columns, ["代码", "code", "指数代码"])
        if name_col and code_col:
            for _, r in df.iterrows():
                nm = str(r[name_col]).strip()
                cd = str(r[code_col]).strip()
                if nm and cd:
                    out[nm] = cd
                    out[_norm_index_name(nm) or nm] = cd
    except Exception as exc:  # noqa: BLE001 - 该映射为增强项，失败不影响主路径
        logger.debug("index_code_id_map_em 拉取失败（将仅用内置映射）: %s", exc)
    _INDEX_CODE_MAP_CACHE = out
    return out


def _resolve_index_code(name: str | None) -> str | None:
    """指数名 -> 代码。先查内置常用映射，再查运行时全量映射。"""
    if not name:
        return None
    norm = _norm_index_name(name)
    for key in (norm, name):
        if key and key in _COMMON_INDEX_MAP:
            return _COMMON_INDEX_MAP[key]
    rt = _runtime_index_code_map()
    for key in (norm, name):
        if key and key in rt:
            return rt[key]
    return None


# ---------- 端口实现 ----------
class AkShareEtfBasicSource:
    name = "akshare"

    def list_etfs(self) -> list[EtfBasic]:
        df = run_df("fund_etf_category_sina", [], {"symbol": "ETF基金"})
        code_col = _pick(df.columns, ["代码", "code"])
        name_col = _pick(df.columns, ["名称", "name"])
        out: list[EtfBasic] = []
        for _, r in df.iterrows():
            code = str(r[code_col]).strip().zfill(6) if code_col else None
            if not code or code == "000000":
                continue
            raw = str(r[name_col]).strip() if name_col else code
            # sina 代码形如 'sh510300' / 'sz159915'
            code = re.sub(r"^[a-zA-Z]+", "", code)
            out.append(EtfBasic(code=code, name=raw, type=None, exchange=_map_exchange(code)))
        return out

    def get_etf(self, code: str) -> EtfBasic | None:
        try:
            df = run_df("fund_overview_em", [code])
        except Exception as exc:  # noqa: BLE001
            logger.warning("akshare fund_overview_em(%s) 失败: %s", code, exc)
            return None
        if df is None or len(df) == 0:
            return None

        info = df.iloc[0].to_dict()
        # 注意：akshare `fund_overview_em` 中跟踪指数实际位于「业绩比较基准」列
        # （值如 "沪深300指数"），「投资标的」列常为空/截断，故以前者为首选。
        track_name = _pick_val(
            info, ["业绩比较基准", "投资标的", "跟踪指数", "标的指数", "跟踪标的"]
        )
        if not track_name:  # 兜底：扫描任意含「指数」的值
            for v in info.values():
                if isinstance(v, str) and "指数" in v:
                    track_name = v
                    break
        track_code = _resolve_index_code(track_name) if track_name else None
        raw_type = _pick_val(info, ["基金类型", "类型"])
        return EtfBasic(
            code=code,
            name=str(_pick_val(info, ["基金简称", "基金名称", "名称"]) or code),
            short_name=_pick_val(info, ["基金简称"]),
            type=raw_type,  # 原始类型串（如 "指数型-股票"），applicability 仅排除 债券/货币/商品
            track_index=str(track_name) if track_name else None,
            track_index_code=track_code,
            fund_manager=_pick_val(info, ["基金管理人"]),
            custodian=_pick_val(info, ["基金托管人"]),
            fund_scale=_scale(_pick_val(info, ["最新规模", "规模"])),
            shares=_scale(_pick_val(info, ["份额规模"])),
            establish_date=_to_date(_pick_val(info, ["成立日", "成立日期"])),
            manager=_pick_val(info, ["基金经理", "现任基金经理"]),
            management_fee_rate=_to_float(_pick_val(info, ["管理费"])),
            custody_fee_rate=_to_float(_pick_val(info, ["托管费"])),
            tracking_error=_to_float(_pick_val(info, ["跟踪误差"])),
            exchange=_map_exchange(code),
            update_time=datetime.now(),
        )


def _pick_val(info: dict, candidates) -> str | None:
    for c in candidates:
        if c in info and info[c] not in (None, "", "None", "nan"):
            return str(info[c])
    # 子串兜底
    for c in candidates:
        for k, v in info.items():
            if c in str(k) and v not in (None, "", "None", "nan"):
                return str(v)
    return None


class AkShareValuationSource:
    name = "akshare"

    def get_latest(self, index_code: str, index_name: str | None = None) -> IndexValuation | None:
        """FR-03：指数最新 PE/PB/股息率。

        口径一致性优先：历史分位用乐咕乐股（`stock_index_pe_lg`/`stock_index_pb_lg`，
        静态口径）按指数名计算，故「最新值」同样优先取乐咕乐股，保证同一口径，
        分位数才有意义。
        中证 `stock_zh_index_value_csindex`（按代码）无市净率列，仅作 PE + 股息率兜底，
        并补充乐咕乐股缺失的股息率。
        """
        name = _norm_index_name(index_name)

        # 1) 主源：乐咕乐股（静态口径，与历史分位同源）
        # 每次调用经 _runner 在独立子进程执行，规避同进程多次调用相互污染。
        lg_pe = lg_pb = lg_date = None
        if name:
            try:
                df = run_df("stock_index_pe_lg", [name])
                if df is not None and len(df):
                    r = df.iloc[-1]
                    lg_pe = _to_float(r[_pick(list(r.index), ["静态市盈率", "市盈率"])])
                    lg_date = _to_date(r[_pick(list(r.index), ["日期", "date"])])
                dfp = run_df("stock_index_pb_lg", [name])
                if dfp is not None and len(dfp):
                    r = dfp.iloc[-1]
                    lg_pb = _to_float(r[_pick(list(r.index), ["市净率"])])
                    if lg_date is None:
                        lg_date = _to_date(r[_pick(list(r.index), ["日期", "date"])])
            except Exception as exc:  # noqa: BLE001
                logger.warning("akshare lg 估值 %s 失败: %s", name, exc)

        # 2) 兜底：仅当乐咕乐股不可用（无指数名 / 非主流宽基）时，才用中证按代码取
        #    PE + 股息率（中证无市净率列）。lg 成功时不再调用中证，避免额外限流。
        cs_pe = cs_dy = cs_date = None
        if (lg_pe is None and lg_pb is None) and index_code:
            try:
                df = run_df("stock_zh_index_value_csindex", [index_code])
                if df is not None and len(df):
                    r = df.iloc[-1]
                    cs_pe = _to_float(r[_pick(list(r.index), ["市盈率1", "市盈率", "pe"])])
                    cs_dy = _to_float(r[_pick(list(r.index), ["股息率", "股息"])])
                    cs_date = _to_date(r[_pick(list(r.index), ["日期", "date"])])
            except Exception as exc:  # noqa: BLE001
                logger.warning("akshare csindex 估值 %s 失败: %s", index_code, exc)

        # 合并：PE/PB 优先乐咕乐股（保证与历史分位口径一致），股息率取中证
        pe = lg_pe if lg_pe is not None else cs_pe
        pb = lg_pb if lg_pb is not None else None
        dy = cs_dy
        if pe is not None or pb is not None:
            source = "akshare-lg" if (lg_pe is not None or lg_pb is not None) else "akshare-csindex"
            return IndexValuation(
                index_code=index_code,
                pe_ttm=pe,
                pb=pb,
                dividend_yield=dy,
                valuation_date=(lg_date or cs_date) or date.today(),
                source=source,
            )
        return None

    def get_history(
        self,
        index_code: str,
        start: date,
        end: date,
        index_name: str | None = None,
    ) -> list[IndexValuationPoint]:
        """FR-06：指数 PE/PB 历史序列（按指数名，乐咕乐股全量历史）。

        `stock_index_pe_lg` / `stock_index_pb_lg` 仅主流宽基指数可用；
        其余指数返回空（由上层优雅降级）。
        """
        name = _norm_index_name(index_name)
        if not name:
            return []
        try:
            df_pe = run_df("stock_index_pe_lg", [name])
            df_pb = run_df("stock_index_pb_lg", [name])
        except Exception as exc:  # noqa: BLE001
            logger.warning("akshare 历史估值 %s 失败: %s", name, exc)
            return []

        pe_map: dict[date, float] = {}
        if df_pe is not None and len(df_pe):
            dcol = _pick(list(df_pe.columns), ["日期", "date"])
            pcol = _pick(list(df_pe.columns), ["静态市盈率", "市盈率"])
            if dcol and pcol:
                for _, r in df_pe.iterrows():
                    d = _to_date(r[dcol])
                    if d:
                        pe_map[d] = _to_float(r[pcol])
        pb_map: dict[date, float] = {}
        if df_pb is not None and len(df_pb):
            dcol = _pick(list(df_pb.columns), ["日期", "date"])
            pcol = _pick(list(df_pb.columns), ["市净率"])
            if dcol and pcol:
                for _, r in df_pb.iterrows():
                    d = _to_date(r[dcol])
                    if d:
                        pb_map[d] = _to_float(r[pcol])

        out: list[IndexValuationPoint] = []
        for d in set(pe_map) | set(pb_map):
            if start and d < start:
                continue
            if end and d > end:
                continue
            out.append(
                IndexValuationPoint(
                    date=d,
                    pe_ttm=pe_map.get(d),
                    pb=pb_map.get(d),
                )
            )
        out.sort(key=lambda p: p.date)
        return out


class AkShareConstituentSource:
    name = "akshare"

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        try:
            df = run_df("index_stock_cons_csindex", [index_code])
        except Exception as exc:  # noqa: BLE001
            logger.warning("akshare 成分股 %s 失败: %s", index_code, exc)
            return []
        if df is None or len(df) == 0:
            return []

        code_col = _pick(df.columns, ["成分券代码", "股票代码", "代码"])
        name_col = _pick(df.columns, ["成分券名称", "股票名称", "名称"])
        ex_col = _pick(df.columns, ["交易所", "exchange"])

        # 权重需单独接口
        weight_map: dict[str, float] = {}
        try:
            dfw = run_df("index_stock_cons_weight_csindex", [index_code])
            wcode = _pick(dfw.columns, ["股票代码", "代码", "成分券代码"])
            wcol = _pick(dfw.columns, ["权重", "weight", "占指数净值比例"])
            if wcode and wcol:
                for _, r in dfw.iterrows():
                    sc = str(r[wcode]).strip().zfill(6)
                    if sc:
                        weight_map[sc] = _to_float(r[wcol])
        except Exception as exc:  # noqa: BLE001
            logger.warning("akshare 成分股权重 %s 失败: %s", index_code, exc)

        out: list[ConstituentStock] = []
        for _, r in df.iterrows():
            sc = str(r[code_col]).strip().zfill(6) if code_col else None
            if not sc:
                continue
            out.append(
                ConstituentStock(
                    stock_code=sc,
                    stock_name=str(r[name_col]).strip() if name_col else None,
                    # 行业(sw_l1)逐股补全需对每只成分股发起网络请求（沪深300 约 300 次），
                    # 代价过高且易触发限流，故暂不补全；成分股列表（代码/名称/权重）已满足需求。
                    sw_l1=None,
                    weight=weight_map.get(sc),
                    exchange=str(r[ex_col]).strip() if ex_col else None,
                )
            )
        return out

    @staticmethod
    def _get_industry(stock_code: str) -> str | None:
        """逐股行业补全（开销大，当前未启用，保留以便后续按需调用）。"""
        try:
            df = run_df("stock_individual_info_em", [stock_code])
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
