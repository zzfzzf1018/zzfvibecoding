"""中证指数公司官网适配器（兜底源）。

实现 ValuationSource / ConstituentSource 端口，用于 AkShare 失效时兜底。
取数方式：中证官网 REST 接口（POST JSON）。详见 docs/数据源调研.md §4。
解析失败时返回 None，由估值服务的多源降级逻辑自动跳过，不阻断主流程。

外部请求统一经 core.clients.HttpClient，满足 docs/AI开发约束.md §5（外部请求仅 datasource 层）。
"""
from __future__ import annotations

from datetime import date, datetime

from app.core.clients import HttpClient
from app.core.logging import logger
from app.models.schemas import ConstituentStock, IndexValuation, IndexValuationPoint

_VAL_URL = "https://www.csindex.com.cn/csindex-home/valuation/indices"
_CONS_URL = "https://www.csindex.com.cn/csindex-home/component/detail"


def _to_float(v):
    if v is None:
        return None
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def _to_date(v):
    if not v:
        return None
    s = str(v)[:10]
    try:
        return datetime.strptime(s, "%Y-%m-%d").date()
    except ValueError:
        return None


def _pick(d, keys):
    """在 dict 中按候选键名取第一个非空值（兼容不同字段命名）。"""
    if not isinstance(d, dict):
        return None
    for k in keys:
        if k in d and d[k] is not None:
            return d[k]
    return None


def _list_of(data):
    """从接口返回的多种结构中抽取 list。"""
    if isinstance(data, dict):
        inner = data.get("data")
        if isinstance(inner, dict):
            return inner.get("list") or []
        if isinstance(inner, list):
            return inner
        # 部分接口直接把 list 放在 data 下的一级字段
        for v in data.values():
            if isinstance(v, list):
                return v
    if isinstance(data, list):
        return data
    return []


class CsIndexValuationSource:
    name = "csindex"

    def __init__(self, client: HttpClient | None = None) -> None:
        self._client = client or HttpClient()

    def get_latest(self, index_code: str) -> IndexValuation | None:
        """FR-03：取指数最新估值（PE/PB/股息率）。失败返回 None。"""
        try:
            data = self._client.post_json(
                _VAL_URL,
                json={"pageNum": 1, "pageSize": 30, "indexCode": str(index_code),
                      "startDate": "", "endDate": ""},
            )
        except Exception as exc:  # noqa: BLE001
            logger.warning("CsIndex 估值获取 %s 失败: %s", index_code, exc)
            return None
        items = _list_of(data)
        if not items:
            return None
        it = items[0]
        return IndexValuation(
            index_code=index_code,
            pe_ttm=_to_float(_pick(it, ["peTtm", "pe", "peTTM", "peLFY"])),
            pb=_to_float(_pick(it, ["pb", "pbMrq", "pbMRQ"])),
            dividend_yield=_to_float(_pick(it, ["dy", "dividendYield", "股息率"])),
            valuation_date=_to_date(_pick(it, ["tradeDate", "date"])),
            source="csindex",
        )

    def get_history(
        self, index_code: str, start: date, end: date
    ) -> list[IndexValuationPoint]:
        """FR-06：中证估值接口支持按日期区间返回历史序列（bonus：补充历史分位数据）。"""
        if not start or not end:
            return []
        try:
            data = self._client.post_json(
                _VAL_URL,
                json={"pageNum": 1, "pageSize": 2000, "indexCode": str(index_code),
                      "startDate": start.isoformat(), "endDate": end.isoformat()},
            )
        except Exception as exc:  # noqa: BLE001
            logger.warning("CsIndex 历史估值 %s 失败: %s", index_code, exc)
            return []
        out: list[IndexValuationPoint] = []
        for it in _list_of(data):
            d = _to_date(_pick(it, ["tradeDate", "date"]))
            if d is None:
                continue
            out.append(
                IndexValuationPoint(
                    date=d,
                    pe_ttm=_to_float(_pick(it, ["peTtm", "pe", "peTTM"])),
                    pb=_to_float(_pick(it, ["pb", "pbMrq"])),
                )
            )
        return out


class CsIndexConstituentSource:
    name = "csindex"

    def __init__(self, client: HttpClient | None = None) -> None:
        self._client = client or HttpClient()

    def get_constituents(self, index_code: str) -> list[ConstituentStock]:
        """FR-05：取指数成分股（含权重）。行业(sw_l1)需逐股补全，此处留空（best-effort）。"""
        try:
            data = self._client.post_json(
                _CONS_URL,
                json={"pageNum": 1, "pageSize": 2000, "indexCode": str(index_code)},
            )
        except Exception as exc:  # noqa: BLE001
            logger.warning("CsIndex 成分股 %s 失败: %s", index_code, exc)
            return []
        out: list[ConstituentStock] = []
        for it in _list_of(data):
            sc = _pick(it, ["stockCode", "stock_code", "code"])
            if not sc:
                continue
            out.append(
                ConstituentStock(
                    stock_code=str(sc).strip().zfill(6),
                    stock_name=_pick(it, ["stockName", "stock_name", "name"]),
                    sw_l1=None,
                    weight=_to_float(_pick(it, ["weight", "权重"])),
                    exchange=None,
                )
            )
        return out


__all__ = ["CsIndexValuationSource", "CsIndexConstituentSource"]
