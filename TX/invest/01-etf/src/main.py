"""应用入口（FastAPI）。

装配路由、统一异常处理。运行：uvicorn src.main:app --reload
"""
from __future__ import annotations

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.api import etf_router, valuation_router
from app.core.errors import AppError
from app.web import web_router

app = FastAPI(title="中国股市 ETF 查询工具", version="1.0")

app.include_router(etf_router)
app.include_router(valuation_router)
app.include_router(web_router)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.exception_handler(AppError)
async def handle_app_error(request: Request, exc: AppError) -> JSONResponse:
    return JSONResponse(status_code=exc.code, content={"code": exc.code, "message": str(exc)})


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("src.main:app", host="0.0.0.0", port=8000, reload=True)
