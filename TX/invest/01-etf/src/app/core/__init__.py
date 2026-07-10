"""横切关注点：config / logging / errors / clients。

注意：不要在此导入 di（避免与 datasource -> core.logging 形成循环依赖）。
需 Container 请使用 `from app.core.di import Container`。
"""
from app.core.config import settings
from app.core.logging import logger
from app.core.errors import AppError, NotFound, SourceUnavailable, InvalidParam

__all__ = [
    "settings",
    "logger",
    "AppError",
    "NotFound",
    "SourceUnavailable",
    "InvalidParam",
]
