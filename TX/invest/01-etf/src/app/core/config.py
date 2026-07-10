"""全局配置（core/config）。

配置外置：通过环境变量注入，禁止在业务代码硬编码。
详见 docs/编码规范.md §8。
"""
from __future__ import annotations

import os
from dataclasses import dataclass


def _env_int(name: str, default: int) -> int:
    try:
        return int(os.getenv(name, str(default)))
    except ValueError:
        return default


def _env_float(name: str, default: float) -> float:
    try:
        return float(os.getenv(name, str(default)))
    except ValueError:
        return default


@dataclass(frozen=True)
class Settings:
    # 存储
    database_url: str = os.getenv("DB_URL", "sqlite:///./etf.db")
    # 受控 HTTP 客户端
    http_timeout: int = _env_int("HTTP_TIMEOUT", 10)
    rate_limit_interval: float = _env_float("RATE_LIMIT", 0.3)
    # 缓存 TTL
    cache_ttl_basic_hours: int = _env_int("CACHE_TTL_BASIC_HOURS", 24)
    cache_ttl_valuation_days: int = _env_int("CACHE_TTL_VALUATION_DAYS", 1)
    cache_ttl_constituent_days: int = _env_int("CACHE_TTL_CONSTITUENT_DAYS", 7)
    # 业务阈值
    min_samples: int = _env_int("MIN_SAMPLES", 250)


settings = Settings()
