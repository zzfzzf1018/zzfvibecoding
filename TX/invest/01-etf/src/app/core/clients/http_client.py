"""受控 HTTP 客户端（core/clients）。

外部请求集中在此，提供超时、重试、限速，供 datasource/* 使用。
services/ 与 api/ 严禁直接使用 requests。详见 docs/AI开发约束.md §5。
"""
from __future__ import annotations

import time

import requests

from app.core.config import settings
from app.core.logging import logger


class HttpClient:
    def __init__(
        self,
        timeout: int | None = None,
        rate_limit_interval: float | None = None,
        max_retries: int = 3,
    ) -> None:
        self._timeout = timeout or settings.http_timeout
        self._interval = rate_limit_interval if rate_limit_interval is not None else settings.rate_limit_interval
        self._max_retries = max_retries
        self._last_call = 0.0

    def _throttle(self) -> None:
        elapsed = time.monotonic() - self._last_call
        wait = self._interval - elapsed
        if wait > 0:
            time.sleep(wait)
        self._last_call = time.monotonic()

    def get_json(self, url: str, params: dict | None = None) -> dict:
        """GET 并返回 JSON；失败按指数退避重试。未实现实际业务，仅提供骨架。"""
        last_exc: Exception | None = None
        for attempt in range(1, self._max_retries + 1):
            self._throttle()
            try:
                resp = requests.get(url, params=params, timeout=self._timeout)
                resp.raise_for_status()
                return resp.json()
            except requests.RequestException as exc:  # noqa: PERF203
                last_exc = exc
                logger.warning("HTTP 请求失败 %s 第%d次: %s", url, attempt, exc)
        raise last_exc if last_exc else RuntimeError("http_client failed")


__all__ = ["HttpClient"]
