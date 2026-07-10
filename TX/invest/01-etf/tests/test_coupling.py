"""松耦合护栏测试（强制）。

静态检查：services/ 与 api/ 不得直接依赖具体数据源实现（akshare 等），
只能依赖 datasource.interfaces 抽象端口。违反即失败，确保后续改动不破坏分层。
见 docs/AI开发约束.md §5、docs/REVIEW规范.md §3.B。
"""
from __future__ import annotations

import ast
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVICES = ROOT / "src" / "app" / "services"
API = ROOT / "src" / "app" / "api"

# 禁止直接出现的模块（具体数据源实现 / 第三方数据源库）
FORBIDDEN_MODULES = {
    "akshare",
    "app.datasource.akshare_src",
    "app.datasource.csindex_src",
    "app.datasource.em_src",
}
# services 允许引用的 datasource 子模块（仅端口）
ALLOWED_DATASOURCE_IN_SERVICES = {"app.datasource.interfaces"}


def _iter_py(root: pathlib.Path):
    yield from root.rglob("*.py")


def _imported_modules(path: pathlib.Path) -> list[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"))
    mods: list[str] = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            mods.extend(n.name for n in node.names)
        elif isinstance(node, ast.ImportFrom):
            if node.module:
                mods.append(node.module)
    return mods


def test_services_no_concrete_source_import():
    for f in _iter_py(SERVICES):
        for m in _imported_modules(f):
            assert m.split(".")[0] != "akshare", f"{f}: 禁止直接 import akshare"
            assert m not in FORBIDDEN_MODULES, f"{f}: 禁止 import 具体数据源 {m}"
            if m.startswith("app.datasource"):
                assert m in ALLOWED_DATASOURCE_IN_SERVICES, (
                    f"{f}: services 仅可依赖 {ALLOWED_DATASOURCE_IN_SERVICES}，实际 {m}"
                )


def test_api_no_concrete_source_import():
    for f in _iter_py(API):
        for m in _imported_modules(f):
            assert m.split(".")[0] != "akshare", f"{f}: 禁止直接 import akshare"
            assert m not in FORBIDDEN_MODULES, f"{f}: 禁止 import 具体数据源 {m}"


def test_services_no_sqlalchemy_orm_direct_use():
    # services 不应直接引用 ORM（应通过 repositories）
    for f in _iter_py(SERVICES):
        for m in _imported_modules(f):
            assert m != "app.models.orm", f"{f}: services 禁止直接依赖 ORM，应走 repositories"
            assert m.split(".")[0] != "sqlalchemy", f"{f}: services 禁止直接使用 sqlalchemy"
