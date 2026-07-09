import { Search, TrendingUp } from 'lucide-react';
import { useState } from 'react';
import { DataSourceSelector } from './DataSourceSelector';

interface HeaderProps {
  onSearch: (keyword: string) => void;
  keyword: string;
  onDataSourceChange: () => void;
}

export const Header = ({ onSearch, keyword, onDataSourceChange }: HeaderProps) => {
  const [inputValue, setInputValue] = useState(keyword);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSearch(inputValue);
  };

  return (
    <header className="bg-primary-800 text-white sticky top-0 z-50 shadow-lg">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          <div className="flex items-center space-x-3">
            <div className="bg-white/10 p-2 rounded-lg">
              <TrendingUp className="h-6 w-6 text-up" />
            </div>
            <div>
              <h1 className="text-xl font-bold">ETF查询工具</h1>
              <p className="text-xs text-primary-200">中国股市ETF数据查询</p>
            </div>
          </div>

          <form onSubmit={handleSubmit} className="flex-1 max-w-md mx-8">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-primary-300 h-5 w-5" />
              <input
                type="text"
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                placeholder="搜索ETF代码或名称..."
                className="w-full pl-10 pr-4 py-2 bg-white/10 border border-white/20 rounded-lg focus:outline-none focus:ring-2 focus:ring-up/50 focus:border-transparent text-sm"
              />
            </div>
          </form>

          <div className="flex items-center space-x-4">
            <DataSourceSelector onDataSourceChange={onDataSourceChange} />
            <div className="hidden sm:block text-sm text-primary-200">
              <span className="inline-flex items-center px-2 py-1 rounded-full bg-up/10 text-up text-xs font-medium">
                实时数据
              </span>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};