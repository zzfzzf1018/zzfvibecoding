import { useState, useRef, useEffect } from 'react';
import { Search, X } from 'lucide-react';
import { Stock, MarketType } from '../../types';
import { searchStocks } from '../../data/mockData';

interface SearchBarProps {
  onSelectStock: (stock: Stock) => void;
  selectedStock: Stock | null;
}

export default function SearchBar({ onSelectStock, selectedStock }: SearchBarProps) {
  const [market, setMarket] = useState<MarketType>('a股');
  const [keyword, setKeyword] = useState('');
  const [results, setResults] = useState<Stock[]>([]);
  const [showResults, setShowResults] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (keyword.trim()) {
      setResults(searchStocks(keyword, market));
      setShowResults(true);
    } else {
      setResults([]);
      setShowResults(false);
    }
  }, [keyword, market]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setShowResults(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelectStock = (stock: Stock) => {
    onSelectStock(stock);
    setKeyword('');
    setShowResults(false);
  };

  const handleClear = () => {
    setKeyword('');
    setResults([]);
    setShowResults(false);
    inputRef.current?.focus();
  };

  return (
    <div ref={containerRef} className="relative w-full max-w-2xl mx-auto">
      <div className="flex items-center bg-white rounded-xl shadow-lg border border-gray-200 overflow-hidden">
        <div className="flex border-r border-gray-200">
          <button
            className={`px-4 py-3 font-medium transition-colors ${
              market === 'a股'
                ? 'bg-blue-600 text-white'
                : 'bg-gray-50 text-gray-600 hover:bg-gray-100'
            }`}
            onClick={() => setMarket('a股')}
          >
            A股
          </button>
          <button
            className={`px-4 py-3 font-medium transition-colors ${
              market === '港股'
                ? 'bg-blue-600 text-white'
                : 'bg-gray-50 text-gray-600 hover:bg-gray-100'
            }`}
            onClick={() => setMarket('港股')}
          >
            港股
          </button>
        </div>
        <div className="flex-1 flex items-center px-4">
          <Search className="w-5 h-5 text-gray-400 mr-3" />
          <input
            ref={inputRef}
            type="text"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onFocus={() => keyword.trim() && setShowResults(true)}
            placeholder="输入股票代码或名称..."
            className="flex-1 py-3 text-gray-800 placeholder-gray-400 outline-none"
          />
          {keyword && (
            <button onClick={handleClear} className="ml-2 p-1 hover:bg-gray-100 rounded-full">
              <X className="w-5 h-5 text-gray-400" />
            </button>
          )}
        </div>
      </div>

      {showResults && results.length > 0 && (
        <div className="absolute top-full left-0 right-0 mt-2 bg-white rounded-xl shadow-xl border border-gray-200 z-50 overflow-hidden">
          {results.map((stock) => (
            <button
              key={`${stock.market}_${stock.code}`}
              onClick={() => handleSelectStock(stock)}
              className={`w-full px-4 py-3 flex items-center justify-between hover:bg-blue-50 transition-colors ${
                selectedStock?.code === stock.code && selectedStock?.market === stock.market
                  ? 'bg-blue-50'
                  : ''
              }`}
            >
              <div className="flex items-center">
                <span className="text-gray-500 text-sm mr-3">
                  {stock.market === 'a股' ? 'SH/SZ' : 'HK'}
                </span>
                <span className="font-medium text-gray-800">{stock.name}</span>
                <span className="text-gray-400 text-sm ml-2">{stock.code}</span>
              </div>
              <div className="flex items-center">
                <span className="font-medium text-gray-800 mr-2">
                  {stock.price.toFixed(2)}
                </span>
                <span
                  className={`text-sm ${
                    stock.change >= 0 ? 'text-green-600' : 'text-red-600'
                  }`}
                >
                  {stock.change >= 0 ? '+' : ''}
                  {stock.changePercent.toFixed(2)}%
                </span>
              </div>
            </button>
          ))}
        </div>
      )}

      {showResults && results.length === 0 && keyword.trim() && (
        <div className="absolute top-full left-0 right-0 mt-2 bg-white rounded-xl shadow-xl border border-gray-200 z-50 overflow-hidden">
          <div className="px-4 py-8 text-center text-gray-500">
            未找到匹配的股票
          </div>
        </div>
      )}
    </div>
  );
}
