import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import { Stock } from '../types';

interface FavoritesState {
  favorites: Stock[];
  addFavorite: (stock: Stock) => void;
  removeFavorite: (stockCode: string, market: string) => void;
  isFavorite: (stockCode: string, market: string) => boolean;
  clearFavorites: () => void;
}

export const useFavoritesStore = create<FavoritesState>()(
  persist(
    (set, get) => ({
      favorites: [],
      addFavorite: (stock) => {
        const { favorites } = get();
        if (!favorites.some(f => f.code === stock.code && f.market === stock.market)) {
          set({ favorites: [...favorites, stock] });
        }
      },
      removeFavorite: (stockCode, market) => {
        const { favorites } = get();
        set({ favorites: favorites.filter(f => !(f.code === stockCode && f.market === market)) });
      },
      isFavorite: (stockCode, market) => {
        const { favorites } = get();
        return favorites.some(f => f.code === stockCode && f.market === market);
      },
      clearFavorites: () => set({ favorites: [] }),
    }),
    {
      name: 'favorites-storage',
      storage: createJSONStorage(() => localStorage),
    }
  )
);
