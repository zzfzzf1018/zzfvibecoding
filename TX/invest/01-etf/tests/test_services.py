"""服务层单元测试（使用 Mock 仓储/数据源，不依赖网络）。

覆盖：检索（编号/名称/空）、分位算法（含样本不足降级）、估值源降级。
松耦合护栏见 tests/test_coupling.py。
"""
from __future__ import annotations

from datetime import date, timedelta

from app.models.schemas import (
    EtfBasic,
    IndexValuation,
    IndexValuationPoint,
    ETFSearchResult,
)
from app.services.constituent_service import ConstituentService
from app.services.percentile_service import PercentileService
from app.services.search_service import SearchService
from app.services.valuation_service import ValuationService


class _FakeEtfRepo:
    def __init__(self, basics):
        self._basics = {b.code: b for b in basics}
        self._list = basics

    def get_basic(self, code):
        return self._basics.get(code)

    def search_by_code(self, kw, page, size):
        return [ETFSearchResult(code=b.code, name=b.name) for b in self._list if b.code.startswith(kw)][:size]

    def search_by_name(self, kw, page, size):
        return [ETFSearchResult(code=b.code, name=b.name) for b in self._list if kw in (b.name or "")][:size]


class _FakeValRepo:
    def __init__(self, hist=None, latest=None):
        self._hist = hist or []
        self._latest = latest

    def get_history(self, idx, start=None, end=None):
        return self._hist

    def get_latest(self, idx):
        return self._latest

    def upsert_latest(self, v):
        self._latest = v

    def append_history(self, idx, p):
        self._hist.append(p)


def test_search_by_code():
    repo = _FakeEtfRepo([EtfBasic(code="510300", name="沪深300ETF"), EtfBasic(code="510500", name="中证500ETF")])
    svc = SearchService(repo)
    res = svc.search("510300")
    assert len(res) == 1 and res[0].code == "510300"


def test_search_by_name():
    repo = _FakeEtfRepo([EtfBasic(code="510300", name="沪深300ETF"), EtfBasic(code="510500", name="中证500ETF")])
    svc = SearchService(repo)
    res = svc.search("ETF")  # 名称子串模糊匹配，两个都含 "ETF"
    assert {r.code for r in res} == {"510300", "510500"}


def test_search_empty():
    svc = SearchService(_FakeEtfRepo([]))
    assert svc.search("") == []


def test_percentile_calculation():
    hist = [
        IndexValuationPoint(date=date(2000, 1, 1) + timedelta(days=i), pe_ttm=float(i), pb=float(i) * 0.1)
        for i in range(300)
    ]
    latest = IndexValuation(index_code="000300", pe_ttm=150.0, pb=15.0, source="x")
    repo = _FakeValRepo(hist=hist, latest=latest)
    etf_repo = _FakeEtfRepo([EtfBasic(code="510300", name="x", track_index_code="000300")])
    svc = PercentileService(etf_repo, repo, basic_sources=[])
    view = svc.get_percentile("510300", "all")
    # pe 0..299，当前 150 -> count<=150 = 151 -> 151/300 = 50.3%
    assert view.pe_percentile == 50.3
    assert view.pb_percentile is not None
    assert view.sample_count == 300
    assert view.degraded is False


def test_percentile_degrade_on_small_sample():
    hist = [IndexValuationPoint(date=date(2024, 1, 1), pe_ttm=12.0, pb=1.2)]
    latest = IndexValuation(index_code="000300", pe_ttm=12.0, pb=1.2, source="x")
    repo = _FakeValRepo(hist=hist, latest=latest)
    etf_repo = _FakeEtfRepo([EtfBasic(code="510300", name="x", track_index_code="000300")])
    svc = PercentileService(etf_repo, repo, basic_sources=[])
    view = svc.get_percentile("510300", "5y")  # 样本 < 250 -> 降级
    assert view.degraded is True
    assert view.sample_count == 1


class _FailingSource:
    name = "fail"

    def get_latest(self, idx):
        raise RuntimeError("boom")


class _GoodSource:
    name = "good"

    def get_latest(self, idx):
        return IndexValuation(index_code=idx, pe_ttm=12.0, pb=1.2, source="good")


def test_valuation_source_fallback():
    repo = _FakeValRepo()
    etf_repo = _FakeEtfRepo([EtfBasic(code="510300", name="x", track_index_code="000300")])
    svc = ValuationService(etf_repo, repo, [_FailingSource(), _GoodSource()], basic_sources=[])
    view = svc.get_valuation("510300")
    assert view.applicable and view.source == "good" and view.pe_ttm == 12.0


def test_valuation_not_applicable_for_bond():
    repo = _FakeValRepo()
    etf_repo = _FakeEtfRepo([EtfBasic(code="511260", name="国债ETF", type="债券", track_index_code="000012")])
    svc = ValuationService(etf_repo, repo, [_GoodSource()], basic_sources=[])
    view = svc.get_valuation("511260")
    assert view.applicable is False
