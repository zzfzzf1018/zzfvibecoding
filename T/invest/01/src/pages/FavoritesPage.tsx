import React, { useState } from 'react';
import { useFavoritesStore } from '../store/useFavoritesStore';
import { getLatestFinancialData } from '../data/mockData';
import { Heart, Trash2, ArrowRight } from 'lucide-react';
import { formatNumber } from '../utils/ratios';
import { Stock } from '../types';

interface FavoritesPageProps {
  onSelectStock?: (stock: Stock) => void;
}

export const FavoritesPage: React.FC<FavoritesPageProps> = ({ onSelectStock }) => {
  const { favorites, removeFavorite } = useFavoritesStore();
  const [expandedStock, setExpandedStock] = useState<string | null>(null);

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-xl font-semibold text-gray-800">我的收藏</h2>
        {favorites.length > 0 && (
          <button
            onClick={() => window.location.href = '/'}
            className="text-sm text-blue-600 hover:text-blue-700 flex items-center gap-1"
          >
            返回首页 <ArrowRight size={16} />
          </button>
        )}
      </div>

      {favorites.length === 0 ? (
        <div className="text-center py-12">
          <Heart size={48} className="mx-auto text-gray-300 mb-4" />
          <p className="text-gray-500">暂无收藏的股票</p>
          <p className="text-gray-400 text-sm mt-2">点击股票卡片上的心形图标添加收藏</p>
        </div>
      ) : (
        <div className="space-y-4">
          {favorites.map((stock) => {
            const financial = getLatestFinancialData(stock.code, stock.market);
            const isExpanded = expandedStock === `${stock.market}_${stock.code}`;

            return (
              <div
                key={`${stock.market}_${stock.code}`}
                className="border border-gray-200 rounded-lg overflow-hidden"
              >
                <div
                  className="flex items-center justify-between p-4 cursor-pointer hover:bg-gray-50"
                  onClick={() => setExpandedStock(isExpanded ? null : `${stock.market}_${stock.code}`)}
                >
                  <div className="flex items-center gap-4">
                    <div
                      className={`w-10 h-10 rounded-full flex items-center justify-center text-white font-semibold ${
                        stock.market === 'a股' ? 'bg-red-500' : 'bg-green-500'
                      }`}
                    >
                      {stock.name.charAt(0)}
                    </div>
                    <div>
                      <div className="font-medium text-gray-800">{stock.name}</div>
                      <div className="text-sm text-gray-500">{stock.code} · {stock.market}</div>
                    </div>
                  </div>

                  <div className="flex items-center gap-4">
                    <div className="text-right">
                      <div className="font-medium text-gray-800">{stock.price.toFixed(2)}</div>
                      <div
                        className={`text-sm ${
                          stock.change >= 0 ? 'text-red-500' : 'text-green-500'
                        }`}
                      >
                        {stock.change >= 0 ? '+' : ''}{stock.changePercent.toFixed(2)}%
                      </div>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        removeFavorite(stock.code, stock.market);
                      }}
                      className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors"
                    >
                      <Trash2 size={18} />
                    </button>
                  </div>
                </div>

                {isExpanded && financial && (
                  <div className="border-t border-gray-100 p-4 bg-gray-50">
                    <div className="grid grid-cols-3 gap-4 text-sm">
                      <div>
                        <div className="text-gray-500">营业收入</div>
                        <div className="font-medium text-gray-800">{formatNumber(financial.incomeStatement.revenue)}</div>
                      </div>
                      <div>
                        <div className="text-gray-500">净利润</div>
                        <div className="font-medium text-gray-800">{formatNumber(financial.incomeStatement.netProfit)}</div>
                      </div>
                      <div>
                        <div className="text-gray-500">总资产</div>
                        <div className="font-medium text-gray-800">{formatNumber(financial.balanceSheet.totalAssets)}</div>
                      </div>
                    </div>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        onSelectStock(stock);
                      }}
                      className="mt-4 w-full py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors"
                    >
                      查看详情
                    </button>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default FavoritesPage;
