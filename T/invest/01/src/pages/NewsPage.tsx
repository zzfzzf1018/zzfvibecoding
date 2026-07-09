import React, { useState } from 'react';
import { getNews, searchStocks } from '../data/mockData';
import { Newspaper, FileText, Calendar, ArrowRight } from 'lucide-react';

interface NewsPageProps {
  onSelectStock: (stock: ReturnType<typeof searchStocks>[0]) => void;
}

export const NewsPage: React.FC<NewsPageProps> = ({ onSelectStock }) => {
  const [selectedStock, setSelectedStock] = useState<string | null>(null);
  const [newsType, setNewsType] = useState<'all' | 'news' | 'announcement'>('all');

  const allStocks = searchStocks();
  const allNews = getNews();

  const filteredNews = selectedStock
    ? allNews.filter(n => n.stockCode === selectedStock)
    : allNews.filter(n => newsType === 'all' || n.type === newsType);

  const getStockName = (code: string) => {
    const stock = allStocks.find(s => s.code === code);
    return stock?.name || code;
  };

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-xl font-semibold text-gray-800">新闻资讯</h2>
        <button
          onClick={() => window.location.href = '/'}
          className="text-sm text-blue-600 hover:text-blue-700 flex items-center gap-1"
        >
          返回首页 <ArrowRight size={16} />
        </button>
      </div>

      <div className="flex flex-wrap gap-4 mb-6">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium text-gray-600">股票筛选:</label>
          <select
            value={selectedStock || ''}
            onChange={(e) => setSelectedStock(e.target.value || null)}
            className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">全部股票</option>
            {allStocks.map(stock => (
              <option key={`${stock.market}_${stock.code}`} value={stock.code}>
                {stock.name} ({stock.code})
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-2">
          <label className="text-sm font-medium text-gray-600">类型:</label>
          <div className="flex gap-2">
            {[
              { key: 'all', label: '全部' },
              { key: 'news', label: '新闻' },
              { key: 'announcement', label: '公告' },
            ].map(item => (
              <button
                key={item.key}
                onClick={() => setNewsType(item.key as typeof newsType)}
                className={`px-3 py-1.5 text-sm rounded-lg transition-colors ${
                  newsType === item.key
                    ? 'bg-blue-500 text-white'
                    : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                }`}
              >
                {item.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="space-y-4">
        {filteredNews.length === 0 ? (
          <div className="text-center py-12">
            <Newspaper size={48} className="mx-auto text-gray-300 mb-4" />
            <p className="text-gray-500">暂无相关新闻</p>
          </div>
        ) : (
          filteredNews.map(news => (
            <div
              key={news.id}
              className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow cursor-pointer"
              onClick={() => {
                const stock = allStocks.find(s => s.code === news.stockCode);
                if (stock) onSelectStock(stock);
              }}
            >
              <div className="flex items-start gap-4">
                <div className={`p-3 rounded-lg ${
                  news.type === 'announcement' ? 'bg-blue-50' : 'bg-green-50'
                }`}>
                  {news.type === 'announcement' ? (
                    <FileText size={24} className="text-blue-500" />
                  ) : (
                    <Newspaper size={24} className="text-green-500" />
                  )}
                </div>

                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-2">
                    <span className={`px-2 py-0.5 text-xs rounded-full ${
                      news.type === 'announcement'
                        ? 'bg-blue-100 text-blue-600'
                        : 'bg-green-100 text-green-600'
                    }`}>
                      {news.type === 'announcement' ? '公告' : '新闻'}
                    </span>
                    <span className="text-sm text-gray-500">{getStockName(news.stockCode)}</span>
                  </div>

                  <h3 className="font-medium text-gray-800 mb-2">{news.title}</h3>
                  <p className="text-sm text-gray-600 line-clamp-2">{news.content}</p>

                  <div className="flex items-center gap-2 mt-3 text-xs text-gray-400">
                    <Calendar size={14} />
                    {news.date}
                  </div>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};

export default NewsPage;
