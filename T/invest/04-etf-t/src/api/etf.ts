import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import etfsData from '@/data/etfs.json';
import detail510050 from '@/data/etf-details/510050.json';
import detail510300 from '@/data/etf-details/510300.json';
import detail510500 from '@/data/etf-details/510500.json';
import detail159919 from '@/data/etf-details/159919.json';
import detail512880 from '@/data/etf-details/512880.json';
import detail513100 from '@/data/etf-details/513100.json';

const detailMap: Record<string, ETFDetailResponse> = {
  '510050': detail510050,
  '510300': detail510300,
  '510500': detail510500,
  '159919': detail159919,
  '512880': detail512880,
  '513100': detail513100,
};

export const getETFList = async (
  type?: ETFCategory,
  keyword?: string
): Promise<{ etfs: ETF[]; total: number }> => {
  await new Promise((resolve) => setTimeout(resolve, 300));

  let filtered = etfsData.etfs as ETF[];

  if (type) {
    filtered = filtered.filter((etf) => etf.category === type);
  }

  if (keyword) {
    const lowerKeyword = keyword.toLowerCase();
    filtered = filtered.filter(
      (etf) =>
        etf.code.toLowerCase().includes(lowerKeyword) ||
        etf.name.toLowerCase().includes(lowerKeyword) ||
        etf.fullName.toLowerCase().includes(lowerKeyword)
    );
  }

  return {
    etfs: filtered,
    total: filtered.length,
  };
};

export const getETFDetail = async (code: string): Promise<ETFDetailResponse> => {
  await new Promise((resolve) => setTimeout(resolve, 300));

  const detail = detailMap[code];
  if (detail) {
    return detail;
  }

  throw new Error(`ETF ${code} not found`);
};