"""横切关注点：config / logging / errors / di / clients。"""
from app.core.config import settings
from app.core.logging import logger
from app.core.errors import AppError, NotFound, SourceUnavailable, InvalidParam
from app.core.di import Container

__all__ = [
    "settings",
    "logger",
    "AppError",
    "NotFound",
    "SourceUnavailable",
    "InvalidParam",
    "Container",
]
