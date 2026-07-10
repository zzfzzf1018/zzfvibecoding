"""数据源适配层入口。

仅导出端口与具体实现类；具体装配在 app.core.di。
"""
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

__all__ = [
    "EtfBasicSource",
    "ValuationSource",
    "ConstituentSource",
    "AkShareEtfBasicSource",
    "AkShareValuationSource",
    "AkShareConstituentSource",
    "CsIndexValuationSource",
    "CsIndexConstituentSource",
    "EmValuationSource",
]
