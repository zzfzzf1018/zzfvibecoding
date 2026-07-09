import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { BarChart3, TrendingUp, Building2, Heart, Newspaper, Calendar, PieChart } from 'lucide-react';
import SearchBar from '../components/Search/SearchBar';
import StockCard from '../components/StockCard/StockCard';
import FinancialOverview from '../components/FinancialOverview/FinancialOverview';
import FinancialTable from '../components/FinancialTable/FinancialTable';
import FinancialRatiosComponent from '../components/FinancialRatios/FinancialRatios';
import FinancialWarning from '../components/FinancialWarning/FinancialWarning';
import IndustryCompare from '../components/Industry/IndustryCompare';
import StockRadarChart from '../components/Charts/RadarChart';
import KLineChart from '../components/Charts/KLineChart';
import DuPontChart from '../components/Charts/DuPontChart';
import FunnelChart from '../components/Charts/FunnelChart';
import ExportPanel from '../components/Export/ExportPanel';
import { Stock, FinancialData, MarketType } from '../types';
import { getHotStocks, getLatestFinancialData, getKLineData, getFinancialData } from '../data/mockData';
import { useCompareStore } from '../store/useCompareStore';
import { useFavoritesStore } from '../store/useFavoritesStore';
import { formatNumber } from '../utils/ratios';

interface HomePageProps {
  className?: string;
}

export default function HomePage({ className }: HomePageProps) {
  const navigate = useNavigate();
  const [selectedStock, setSelectedStock] = useState<Stock | null>(null);
  const [financialData, setFinancialData] = useState<FinancialData | null>(null);
  const [previousFinancial, setPreviousFinancial] = useState<FinancialData | null>(null);
  const [hotStocks, setHotStocks] = useState<Stock[]>([]);
  const [market, setMarket] = useState<MarketType>('a股');
  const [activeTab, setActiveTab] = useState<'overview' | 'ratios' | 'charts' | 'industry' | 'export'>('overview');
  const { items } = useCompareStore();
  const { favorites, addFavorite, removeFavorite, isFavorite } = useFavoritesStore();

  useEffect(() => {
    setHotStocks(getHotStocks());
  }, [market]);

  const handleSelectStock = (stock: Stock) => {
    setSelectedStock(stock);
    const financials = getFinancialData(stock.code, stock.market, 'annual');
    setFinancialData(financials[0] || null);
    setPreviousFinancial(financials[1] || null);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const toggleFavorite = (stock: Stock) => {
    if (isFavorite(stock.code, stock.market)) {
      removeFavorite(stock.code, stock.market);
    } else {
      addFavorite(stock);
    }
  };

  const klineData = selectedStock ? getKLineData(selectedStock.code, selectedStock.market) : [];

  const chartData = selectedStock && financialData 
    ? [{ financial: financialData, stock: selectedStock }] 
    : [];

  return (
    <div className={`${className} min-h-screen bg-gradient-to-br from-slate-50 to-blue-50`}>
      <header className="bg-white shadow-sm sticky top-0 z-40">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center">
              <div className="w-10 h-10 bg-gradient-to-br from-blue-600 to-blue-700 rounded-xl flex items-center justify-center mr-3">
                <BarChart3 className="w-6 h-6 text-white" />
              </div>
              <div>
                <h1 className="text-xl font-bold text-gray-800">财务数据对比分析</h1>
                <p className="text-sm text-gray-500">A股 & 港股上市公司财务数据</p>
              </div>
            </div>
            
            <div className="flex items-center gap-3">
              <button
                onClick={() => navigate('/favorites')}
                className="p-2 text-gray-600 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors relative"
                title="我的收藏"
              >
                <Heart size={20} />
                {favorites.length > 0 && (
                  <span className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">
                    {favorites.length}
                  </span>
                )}
              </button>
              <button
                onClick={() => navigate('/news')}
                className="p-2 text-gray-600 hover:text-blue-500 hover:bg-blue-50 rounded-lg transition-colors"
                title="新闻资讯"
              >
                <Newspaper size={20} />
              </button>
              <button
                onClick={() => navigate('/calendar')}
                className="p-2 text-gray-600 hover:text-green-500 hover:bg-green-50 rounded-lg transition-colors"
                title="财务日历"
              >
                <Calendar size={20} />
              </button>
              {items.length > 0 && (
                <button
                  onClick={() => navigate('/compare')}
                  className="px-4 py-2 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors flex items-center"
                >
                  <TrendingUp className="w-4 h-4 mr-2" />
                  对比 ({items.length})
                </button>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="container mx-auto px-4 py-8">
        <section className="mb-12">
          <div className="text-center mb-6">
            <h2 className="text-2xl font-bold text-gray-800 mb-2">
              获取上市公司财务数据
            </h2>
            <p className="text-gray-500">搜索A股或港股股票，查看详细财务报表</p>
          </div>
          <SearchBar onSelectStock={handleSelectStock} selectedStock={selectedStock} />
        </section>

        {selectedStock && financialData && (
          <section className="mb-12 space-y-6">
            <div className="bg-white rounded-xl shadow-sm p-4 flex items-center justify-between">
              <div className="flex items-center gap-4">
                <div className={`w-12 h-12 rounded-full flex items-center justify-center text-white font-semibold ${
                  selectedStock.market === 'a股' ? 'bg-red-500' : 'bg-green-500'
                }`}>
                  {selectedStock.name.charAt(0)}
                </div>
                <div>
                  <h2 className="text-xl font-bold text-gray-800">{selectedStock.name}</h2>
                  <p className="text-sm text-gray-500">{selectedStock.code} · {selectedStock.market} · {selectedStock.industry}</p>
                </div>
              </div>
              <div className="flex items-center gap-4">
                <div className="text-right">
                  <div className="text-xl font-bold text-gray-800">{selectedStock.price.toFixed(2)}</div>
                  <div className={`text-sm ${selectedStock.change >= 0 ? 'text-red-500' : 'text-green-500'}`}>
                    {selectedStock.change >= 0 ? '+' : ''}{selectedStock.changePercent.toFixed(2)}%
                  </div>
                </div>
                <button
                  onClick={() => toggleFavorite(selectedStock)}
                  className={`p-3 rounded-lg transition-colors ${
                    isFavorite(selectedStock.code, selectedStock.market)
                      ? 'bg-red-50 text-red-500'
                      : 'bg-gray-50 text-gray-400 hover:text-red-400'
                  }`}
                >
                  <Heart size={24} fill={isFavorite(selectedStock.code, selectedStock.market) ? 'currentColor' : 'none'} />
                </button>
              </div>
            </div>

            <div className="flex gap-2 overflow-x-auto pb-2">
              {[
                { key: 'overview', label: '财务概览', icon: Building2 },
                { key: 'ratios', label: '财务比率', icon: PieChart },
                { key: 'charts', label: '图表分析', icon: BarChart3 },
                { key: 'industry', label: '行业对比', icon: TrendingUp },
                { key: 'export', label: '数据导出', icon: BarChart3 },
              ].map(tab => (
                <button
                  key={tab.key}
                  onClick={() => setActiveTab(tab.key as typeof activeTab)}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg font-medium whitespace-nowrap transition-colors ${
                    activeTab === tab.key
                      ? 'bg-blue-500 text-white'
                      : 'bg-white text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  <tab.icon size={18} />
                  {tab.label}
                </button>
              ))}
            </div>

            {activeTab === 'overview' && (
              <div className="space-y-6">
                <FinancialOverview financial={financialData} />
                <FinancialTable financial={financialData} />
                <FinancialWarning financial={financialData} previousFinancial={previousFinancial} />
              </div>
            )}

            {activeTab === 'ratios' && (
              <div className="space-y-6">
                <FinancialRatiosComponent financial={financialData} stock={selectedStock} />
                <DuPontChart financial={financialData} />
              </div>
            )}

            {activeTab === 'charts' && (
              <div className="space-y-6">
                {klineData.length > 0 && (
                  <KLineChart data={klineData} stockName={selectedStock.name} />
                )}
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                  <StockRadarChart data={chartData} />
                  <FunnelChart financial={financialData} />
                </div>
              </div>
            )}

            {activeTab === 'industry' && (
              <IndustryCompare onSelectStock={handleSelectStock} />
            )}

            {activeTab === 'export' && (
              <ExportPanel financial={financialData} stock={selectedStock} />
            )}
          </section>
        )}

        {!selectedStock && (
          <section className="mb-12">
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center">
                <Building2 className="w-5 h-5 text-blue-600 mr-2" />
                <h2 className="text-xl font-bold text-gray-800">热门股票</h2>
              </div>
              <div className="flex bg-gray-100 rounded-lg p-1">
                <button
                  className={`px-4 py-2 rounded-md font-medium transition-colors ${
                    market === 'a股'
                      ? 'bg-white shadow-sm text-blue-600'
                      : 'text-gray-600 hover:text-gray-800'
                  }`}
                  onClick={() => setMarket('a股')}
                >
                  A股
                </button>
                <button
                  className={`px-4 py-2 rounded-md font-medium transition-colors ${
                    market === '港股'
                      ? 'bg-white shadow-sm text-blue-600'
                      : 'text-gray-600 hover:text-gray-800'
                  }`}
                  onClick={() => setMarket('港股')}
                >
                  港股
                </button>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {hotStocks.map((stock) => (
                <StockCard
                  key={`${stock.market}_${stock.code}`}
                  stock={stock}
                  onClick={() => handleSelectStock(stock)}
                  onFavorite={() => toggleFavorite(stock)}
                  isFavorite={isFavorite(stock.code, stock.market)}
                />
              ))}
            </div>
          </section>
        )}

        {!selectedStock && (
          <section className="bg-white rounded-xl shadow-md border border-gray-100 p-8">
            <div className="text-center">
              <div className="w-24 h-24 bg-gradient-to-br from-blue-100 to-blue-200 rounded-full flex items-center justify-center mx-auto mb-6">
                <BarChart3 className="w-12 h-12 text-blue-600" />
              </div>
              <h3 className="text-xl font-bold text-gray-800 mb-4">
                开始分析您关注的股票
              </h3>
              <p className="text-gray-500 mb-6 max-w-md mx-auto">
                通过搜索股票代码或名称，快速获取上市公司的财务数据，包括资产负债表、利润表和现金流量表，支持多股票对比分析。
              </p>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 max-w-lg mx-auto">
                <div className="p-4 bg-green-50 rounded-lg">
                  <div className="text-2xl font-bold text-green-600">1000+</div>
                  <div className="text-sm text-gray-600">上市公司</div>
                </div>
                <div className="p-4 bg-blue-50 rounded-lg">
                  <div className="text-2xl font-bold text-blue-600">10+</div>
                  <div className="text-sm text-gray-600">财务指标</div>
                </div>
                <div className="p-4 bg-purple-50 rounded-lg">
                  <div className="text-2xl font-bold text-purple-600">10年</div>
                  <div className="text-sm text-gray-600">历史数据</div>
                </div>
                <div className="p-4 bg-orange-50 rounded-lg">
                  <div className="text-2xl font-bold text-orange-600">5种</div>
                  <div className="text-sm text-gray-600">图表类型</div>
                </div>
              </div>
            </div>
          </section>
        )}
      </main>

      <footer className="bg-white border-t border-gray-100 mt-12">
        <div className="container mx-auto px-4 py-6">
          <div className="text-center text-gray-500 text-sm">
            <p>财务数据对比分析工具</p>
            <p className="mt-1">数据仅供参考，不构成投资建议</p>
          </div>
        </div>
      </footer>
    </div>
  );
}
