import { Database, RefreshCw } from 'lucide-react';
import { useState } from 'react';
import { getDataSource, setDataSource, getAvailableDataSources, clearCache } from '@/api/etf';
import type { DataSourceType } from '@/api/datasources';

interface DataSourceSelectorProps {
  onDataSourceChange: () => void;
}

export const DataSourceSelector = ({ onDataSourceChange }: DataSourceSelectorProps) => {
  const [currentSource, setCurrentSource] = useState<DataSourceType>(getDataSource());
  const [showDropdown, setShowDropdown] = useState(false);
  const sources = getAvailableDataSources();

  const handleSelect = (type: DataSourceType) => {
    setCurrentSource(type);
    setDataSource(type);
    clearCache();
    onDataSourceChange();
    setShowDropdown(false);
  };

  const handleRefresh = () => {
    clearCache();
    onDataSourceChange();
  };

  const currentSourceName = sources.find((s) => s.type === currentSource)?.name || '未知';

  return (
    <div className="relative">
      <button
        onClick={() => setShowDropdown(!showDropdown)}
        className="flex items-center space-x-2 px-3 py-1.5 rounded-lg bg-white/10 border border-white/20 hover:bg-white/20 transition-colors"
      >
        <Database className="h-4 w-4" />
        <span className="text-sm font-medium">{currentSourceName}</span>
        <svg
          className={`h-4 w-4 transition-transform ${showDropdown ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {showDropdown && (
        <div className="absolute right-0 mt-2 w-48 bg-white rounded-lg shadow-xl border border-neutral-100 py-2 z-50">
          <div className="px-4 py-2 border-b border-neutral-100">
            <span className="text-xs font-medium text-neutral-500">选择数据源</span>
          </div>
          {sources.map((source) => (
            <button
              key={source.type}
              onClick={() => handleSelect(source.type)}
              className={`w-full px-4 py-2 text-left text-sm hover:bg-neutral-50 transition-colors ${
                currentSource === source.type ? 'bg-primary-50 text-primary-700 font-medium' : 'text-neutral-700'
              }`}
            >
              {source.name}
            </button>
          ))}
          <div className="border-t border-neutral-100 mt-2">
            <button
              onClick={handleRefresh}
              className="w-full px-4 py-2 text-left text-sm text-neutral-600 hover:bg-neutral-50 transition-colors flex items-center space-x-2"
            >
              <RefreshCw className="h-4 w-4" />
              <span>刷新缓存</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
};