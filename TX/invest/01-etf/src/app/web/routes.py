"""Web UI 路由（最简）。

仅托管单页 index.html；页面内通过 fetch 调用 /api/etf/* 完成全部交互。
本层不依赖 services/datasource，保持 UI 与业务解耦（详见 docs/架构设计文档.md §3）。
"""
from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter
from fastapi.responses import FileResponse

_TPL = Path(__file__).resolve().parent.parent / "templates" / "index.html"
web_router = APIRouter(tags=["web"])


@web_router.get("/", include_in_schema=False)
def index() -> FileResponse:
    return FileResponse(_TPL)
