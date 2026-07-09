import { LayoutGrid, Building2, Lightbulb, Landmark, Globe2 } from 'lucide-react';
import type { ETFCategory } from '@/types';
import { categoryMap } from '@/types';

interface CategoryFilterProps {
  selectedCategory: ETFCategory | undefined;
  onSelect: (category: ETFCategory | undefined) => void;
}

const categories: { key: ETFCategory | undefined; label: string; icon: React.ReactNode }[] = [
  { key: undefined, label: '全部', icon: <LayoutGrid className="h-4 w-4" /> },
  { key: 'broad', label: categoryMap['broad'], icon: <LayoutGrid className="h-4 w-4" /> },
  { key: 'industry', label: categoryMap['industry'], icon: <Building2 className="h-4 w-4" /> },
  { key: 'theme', label: categoryMap['theme'], icon: <Lightbulb className="h-4 w-4" /> },
  { key: 'bond', label: categoryMap['bond'], icon: <Landmark className="h-4 w-4" /> },
  { key: 'cross-border', label: categoryMap['cross-border'], icon: <Globe2 className="h-4 w-4" /> },
];

export const CategoryFilter = ({ selectedCategory, onSelect }: CategoryFilterProps) => {
  return (
    <div className="flex flex-wrap gap-2">
      {categories.map((category) => (
        <button
          key={category.key ?? 'all'}
          onClick={() => onSelect(category.key)}
          className={`flex items-center space-x-1.5 px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200 ${
            selectedCategory === category.key
              ? 'bg-primary-800 text-white shadow-md'
              : 'bg-white text-neutral-700 border border-neutral-200 hover:bg-neutral-50 hover:border-primary-300'
          }`}
        >
          {category.icon}
          <span>{category.label}</span>
        </button>
      ))}
    </div>
  );
};