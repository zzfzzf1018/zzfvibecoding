import { useState, useCallback } from 'react';
import type { ETF, ETFDetailResponse, ETFCategory } from '@/types';
import { getETFList, getETFDetail } from '@/api/etf';

export const useETFList = () => {
  const [etfs, setEtfs] = useState<ETF[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [total, setTotal] = useState(0);

  const fetchETFList = useCallback(
    async (type?: ETFCategory, keyword?: string) => {
      setLoading(true);
      setError(null);
      try {
        const result = await getETFList(type, keyword);
        setEtfs(result.etfs);
        setTotal(result.total);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to fetch ETF list');
      } finally {
        setLoading(false);
      }
    },
    []
  );

  return { etfs, loading, error, total, fetchETFList };
};

export const useETFDetail = () => {
  const [detail, setDetail] = useState<ETFDetailResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchETFDetail = useCallback(async (code: string) => {
    setLoading(true);
    setError(null);
    try {
      const result = await getETFDetail(code);
      setDetail(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch ETF detail');
    } finally {
      setLoading(false);
    }
  }, []);

  return { detail, loading, error, fetchETFDetail };
};