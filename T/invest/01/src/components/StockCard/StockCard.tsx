import { TrendingUp, TrendingDown, Building2, Heart } from 'lucide-react';
import { Stock } from '../../types';

interface StockCardProps {
  stock: Stock;
  onClick: () => void;
  onFavorite?: () => void;
  isFavorite?: boolean;
}

export default function StockCard({ stock, onClick, onFavorite, isFavorite }: StockCardProps) {
  return (
    <div
      onClick={onClick}
      className="bg-white rounded-xl shadow-md hover:shadow-lg transition-all duration-300 cursor-pointer overflow-hidden border border-gray-100"
    >
      <div className="p-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center">
            <div className={`w-10 h-10 rounded-lg flex items-center justify-center mr-3 ${
              stock.market === 'a股' ? 'bg-gradient-to-br from-red-500 to-red-600' : 'bg-gradient-to-br from-green-500 to-green-600'
            }`}>
              <Building2 className="w-5 h-5 text-white" />
            </div>
            <div>
              <h3 className="font-bold text-gray-800">{stock.name}</h3>
              <p className="text-sm text-gray-500">{stock.market} · {stock.code}</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {onFavorite && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onFavorite();
                }}
                className={`p-1.5 rounded-lg transition-colors ${
                  isFavorite
                    ? 'bg-red-50 text-red-500'
                    : 'text-gray-400 hover:text-red-400 hover:bg-red-50'
                }`}
              >
                <Heart size={16} fill={isFavorite ? 'currentColor' : 'none'} />
              </button>
            )}
            <div className={`flex items-center text-sm ${stock.change >= 0 ? 'text-red-600' : 'text-green-600'}`}>
              {stock.change >= 0 ? (
                <TrendingUp className="w-4 h-4 mr-1" />
              ) : (
                <TrendingDown className="w-4 h-4 mr-1" />
              )}
              {stock.change >= 0 ? '+' : ''}
              {stock.changePercent.toFixed(2)}%
            </div>
          </div>
        </div>
        <div className="flex items-baseline">
          <span className="text-2xl font-bold text-gray-800">
            {stock.price.toFixed(2)}
          </span>
          <span
            className={`ml-2 text-sm ${stock.change >= 0 ? 'text-red-600' : 'text-green-600'}`}
          >
            {stock.change >= 0 ? '+' : ''}
            {stock.change.toFixed(2)}
          </span>
        </div>
      </div>
      <div className={`h-1 ${stock.change >= 0 ? 'bg-red-500' : 'bg-green-500'}`} />
    </div>
  );
}
