import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ETFHeader } from '@/components/ETFHeader';
import { BasicInfo } from '@/components/BasicInfo';
import { Holdings } from '@/components/Holdings';
import { Fees } from '@/components/Fees';
import { Dividends } from '@/components/Dividends';
import { Valuation } from '@/components/Valuation';
import { Quantiles } from '@/components/Quantiles';
import { useETFDetail } from '@/hooks/useETF';
import { Loader2, AlertCircle } from 'lucide-react';

export const ETFDetailPage = () => {
  const { code } = useParams<{ code: string }>();
  const [currentTab, setCurrentTab] = useState('basic');
  const { detail, loading, error, fetchETFDetail } = useETFDetail();

  useEffect(() => {
    if (code) {
      fetchETFDetail(code);
    }
  }, [code, fetchETFDetail]);

  if (loading) {
    return (
      <div className="min-h-screen bg-neutral-50 flex items-center justify-center">
        <Loader2 className="h-12 w-12 text-primary-600 animate-spin" />
      </div>
    );
  }

  if (error || !detail) {
    return (
      <div className="min-h-screen bg-neutral-50 flex items-center justify-center">
        <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-8 text-center max-w-md">
          <AlertCircle className="h-16 w-16 text-red-400 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-neutral-800 mb-2">ETF详情获取失败</h3>
          <p className="text-neutral-500 mb-4">{error}</p>
          <button
            onClick={() => window.location.href = '/'}
            className="px-4 py-2 bg-primary-800 text-white rounded-lg hover:bg-primary-700 transition-colors"
          >
            返回ETF列表
          </button>
        </div>
      </div>
    );
  }

  const renderContent = () => {
    switch (currentTab) {
      case 'basic':
        return <BasicInfo basic={detail.basic} />;
      case 'holdings':
        return <Holdings holdings={detail.holdings} />;
      case 'fees':
        return <Fees fees={detail.fees} />;
      case 'dividends':
        return <Dividends dividends={detail.dividends} />;
      case 'valuation':
        return <Valuation valuation={detail.valuation} />;
      case 'quantiles':
        return <Quantiles quantiles={detail.quantiles} />;
      default:
        return <BasicInfo basic={detail.basic} />;
    }
  };

  return (
    <div className="min-h-screen bg-neutral-50">
      <ETFHeader
        basic={detail.basic}
        currentTab={currentTab}
        onTabChange={setCurrentTab}
      />

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {renderContent()}
      </main>
    </div>
  );
};