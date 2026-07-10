"""接口契约测试（端口 + 具体实现一致性）。

验证 datasource 具体实现满足 interfaces 中定义的 Protocol（依赖倒置）。
"""
from __future__ import annotations

from app.datasource.interfaces import (
    ConstituentSource,
    EtfBasicSource,
    ValuationSource,
)
from app.datasource.akshare_src import (
    AkShareConstituentSource,
    AkShareEtfBasicSource,
    AkShareValuationSource,
)
from app.datasource.csindex_src import (
    CsIndexConstituentSource,
    CsIndexValuationSource,
)
from app.datasource.em_src import EmValuationSource


def test_akshare_implements_protocols():
    assert isinstance(AkShareEtfBasicSource(), EtfBasicSource)
    assert isinstance(AkShareValuationSource(), ValuationSource)
    assert isinstance(AkShareConstituentSource(), ConstituentSource)


def test_csindex_implements_protocols():
    assert isinstance(CsIndexValuationSource(), ValuationSource)
    assert isinstance(CsIndexConstituentSource(), ConstituentSource)


def test_em_implements_protocol():
    assert isinstance(EmValuationSource(), ValuationSource)
