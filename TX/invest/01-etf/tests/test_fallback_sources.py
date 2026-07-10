"""兜底数据源 + Web 基础测试。

验证：
- 中证/东财兜底源满足 Protocol（依赖倒置）且具 name 标识；
- SearchService.get_basic 经 basic_sources 补全；
- Web 模板存在且包含关键交互标记。
"""
from __future__ import annotations

from pathlib import Path

from app.datasource.csindex_src import CsIndexConstituentSource, CsIndexValuationSource
from app.datasource.em_src import EmValuationSource
from app.datasource.interfaces import ConstituentSource, EtfBasicSource, ValuationSource
from app.models.schemas import EtfBasic
from app.services.search_service import SearchService


def test_csindex_protocol_and_name():
    assert isinstance(CsIndexValuationSource(), ValuationSource)
    assert isinstance(CsIndexConstituentSource(), ConstituentSource)
    assert CsIndexValuationSource().name == "csindex"
    assert CsIndexConstituentSource().name == "csindex"


def test_em_protocol_and_name():
    assert isinstance(EmValuationSource(), ValuationSource)
    assert EmValuationSource().name == "eastmoney"


class _FakeRepo:
    def get_basic(self, code):
        return None

    def upsert_basic(self, e):
        pass

    def search_by_code(self, k, p, s):
        return []

    def search_by_name(self, k, p, s):
        return []


class _FakeBasic(EtfBasicSource):
    name = "fake"

    def list_etfs(self):
        return []

    def get_etf(self, code):
        return EtfBasic(code=code, name="测试ETF", type="宽基", track_index_code="000300")


def test_search_get_basic_via_fake_source():
    svc = SearchService(_FakeRepo(), [_FakeBasic()])
    etf = svc.get_basic("510300")
    assert etf.code == "510300"
    assert etf.track_index_code == "000300"


def test_web_template_present():
    tpl = Path(__file__).resolve().parent.parent / "src" / "app" / "templates" / "index.html"
    assert tpl.exists(), "Web 模板 index.html 缺失"
    html = tpl.read_text(encoding="utf-8")
    assert "doSearch" in html and "/api/etf/search" in html
