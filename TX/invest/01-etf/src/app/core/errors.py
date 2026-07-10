"""统一异常（core/errors）。

所有业务/接口异常继承 AppError；API 层统一捕获并封装为 JSON。
错误码与软件详细设计说明书 §5 对齐。
"""
from __future__ import annotations


class AppError(Exception):
    code: int = 500
    message: str = "internal error"

    def __init__(self, message: str | None = None) -> None:
        super().__init__(message or self.message)


class NotFound(AppError):
    code = 404
    message = "resource not found"


class SourceUnavailable(AppError):
    code = 503
    message = "all data sources unavailable"


class InvalidParam(AppError):
    code = 400
    message = "invalid parameter"


__all__ = ["AppError", "NotFound", "SourceUnavailable", "InvalidParam"]
