"""东方财富适配器（兜底源）。

实现 ValuationSource 端口，行情/估值兜底。通过 push2.eastmoney.com 指数行情接口取 PE/PB/股息率。
解析失败返回 None，由估值服务的多源降级逻辑自动跳过，不阻断主流程。
外部请求统一经 core.clients.HttpClient，满足 docs/AI开发约束.md §5。
"""
from __future__ import annotations

from datetime import date

from app.core.clients import HttpClient
from app.core.logging import logger
from app.models.schemas import IndexValuation, IndexValuationPoint


_URL = "https://push2.eastmoney.com/api/qt/stock/get"


def _to_float(v):
    if v is None:
        return None
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def _secid(index_code: str) -> str:
    """东财 secid 规则：深交所指数以 39 开头（如 399001），其余按上交所处理。"""
    code = str(index_code)
    if code.startswith("39"):
        return f"0.{code}"
    return f"1.{code}"


class EmValuationSource:
    name = "eastmoney"

    def __init__(self, client: HttpClient | None = None) -> None:
        self._client = client or HttpClient()

    def get_latest(self, index_code: str) -> IndexValuation | None:
        """FR-03：指数 PE(TTM)/PB/股息率（东财 push2 字段 f116/f117/f162）。"""
        params = {"secid": _secid(index_code), "fields": "f116,f117,f162,f57,f58"}
        try:
            data = self._client.get_json(_URL, params=params)
        except Exception as exc:  # noqa: BLE001
            logger.warning("东财估值 %s 失败: %s", index_code, exc)
            return None
        d = (data or {}).get("data") or {}
        if not d:
            return None
        return IndexValuation(
            index_code=index_code,
            pe_ttm=_to_float(d.get("f116")),
            pb=_to_float(d.get("f117")),
            dividend_yield=_to_float(d.get("f162")),
            valuation_date=date.today(),
            source="eastmoney",
        )

    def get_history(
        self, index_code: str, start: date, end: date
    ) -> list[IndexValuationPoint]:
        # 东财公开接口无稳定的指数历史估值序列，历史由调度自采 + 中证兜底支撑。
        return []


__all__ = ["EmValuationSource"]
