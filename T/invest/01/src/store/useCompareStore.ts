import { create } from 'zustand';
import { CompareItem, Stock, FinancialData } from '../types';

interface CompareState {
  items: CompareItem[];
  addItem: (stock: Stock, financial: FinancialData) => void;
  removeItem: (stockCode: string) => void;
  clearAll: () => void;
  isInCompare: (stockCode: string) => boolean;
}

export const useCompareStore = create<CompareState>((set, get) => ({
  items: [],
  addItem: (stock, financial) => {
    const existing = get().items.find((item) => item.stock.code === stock.code);
    if (!existing) {
      set((state) => ({ items: [...state.items, { stock, financial }] }));
    }
  },
  removeItem: (stockCode) => {
    set((state) => ({ items: state.items.filter((item) => item.stock.code !== stockCode) }));
  },
  clearAll: () => set({ items: [] }),
  isInCompare: (stockCode) => {
    return get().items.some((item) => item.stock.code === stockCode);
  },
}));
