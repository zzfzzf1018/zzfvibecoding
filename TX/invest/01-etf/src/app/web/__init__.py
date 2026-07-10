"""Web UI 层（最简前端，仅负责托管静态页面；业务调用全部走 /api）。"""
from app.web.routes import web_router

__all__ = ["web_router"]
