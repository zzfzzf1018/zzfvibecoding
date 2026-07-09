import { useEffect, useState } from 'react';
import { Header } from '@/components/Header';
import { CategoryFilter } from '@/components/CategoryFilter';
import { ETFCard } from '@/components/ETFCard';
import { useETFList } from '@/hooks/useETF';
import type { ETFCategory } from '@/types';
import { Loader2, Inbox } from 'lucide-react';

export const ETFListPage = () => {
  const [selectedCategory, setSelectedCategory] = useState<ETFCategory | undefined>(undefined);
  const [searchKeyword, setSearchKeyword] = useState('');
  const { etfs, loading, error, total, fetchETFList } = useETFList();

  useEffect(() => {
    fetchETFList(selectedCategory, searchKeyword);
  }, [selectedCategory, searchKeyword, fetchETFList]);

  const handleSearch = (keyword: string) => {
    setSearchKeyword(keyword);
  };

  const handleCategoryChange = (category: ETFCategory | undefined) => {
    setSelectedCategory(category);
  };

  return (
    <div className="min-h-screen bg-neutral-50">
      <Header onSearch={handleSearch} keyword={searchKeyword} />

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-8">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-2xl font-bold text-neutral-800">ETF列表</h2>
              <p className="text-sm text-neutral-500 mt-1">
                共 {total} 只ETF可供选择
              </p>
            </div>
            <div className="hidden sm:flex items-center space-x-2">
              <span className="inline-flex items-center px-2 py-1 rounded-full bg-primary-100 text-primary-700 text-xs font-medium">
                宽基 {etfs.filter((e) => e.category === 'broad').length}
              </span>
              <span className="inline-flex items-center px-2 py-1 rounded-full bg-blue-100 text-blue-700 text-xs font-medium">
                行业 {etfs.filter((e) => e.category === 'industry').length}
              </span>
              <span className="inline-flex items-center px-2 py-1 rounded-full bg-purple-100 text-purple-700 text-xs font-medium">
                主题 {etfs.filter((e) => e.category === 'theme').length}
              </span>
              <span className="inline-flex items-center px-2 py-1 rounded-full bg-green-100 text-green-700 text-xs font-medium">
                债券 {etfs.filter((e) => e.category === 'bond').length}
              </span>
              <span className="inline-flex items-center px-2 py-1 rounded-full bg-orange-100 text-orange-700 text-xs font-medium">
                跨境 {etfs.filter((e) => e.category === 'cross-border').length}
              </span>
            </div>
          </div>
          <CategoryFilter
            selectedCategory={selectedCategory}
            onSelect={handleCategoryChange}
          />
        </div>

        {loading && (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-8 w-8 text-primary-600 animate-spin" />
          </div>
        )}

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
            <p className="text-red-700">{error}</p>
          </div>
        )}

        {!loading && !error && etfs.length === 0 && (
          <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-12 text-center">
            <Inbox className="h-16 w-16 text-neutral-300 mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-neutral-800 mb-2">未找到匹配的ETF</h3>
            <p className="text-neutral-500">
              请尝试调整搜索关键词或筛选条件
            </p>
          </div>
        )}

        {!loading && !error && etfs.length > 0 && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
            {etfs.map((etf) => (
              <ETFCard key={etf.code} etf={etf} />
            ))}
          </div>
        )}
      </main>
    </div>
  );
};