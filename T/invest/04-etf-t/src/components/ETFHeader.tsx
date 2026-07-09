import { ArrowLeft, Home, Info, PieChart, CreditCard, Gift, TrendingUp, History } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import type { ETFBasicInfo } from '@/types';

interface ETFHeaderProps {
  basic: ETFBasicInfo;
  currentTab: string;
  onTabChange: (tab: string) => void;
}

const tabs = [
  { key: 'basic', label: '基本信息', icon: <Info className="h-4 w-4" /> },
  { key: 'holdings', label: '成分股', icon: <PieChart className="h-4 w-4" /> },
  { key: 'fees', label: '费率', icon: <CreditCard className="h-4 w-4" /> },
  { key: 'dividends', label: '分红', icon: <Gift className="h-4 w-4" /> },
  { key: 'valuation', label: '估值', icon: <TrendingUp className="h-4 w-4" /> },
  { key: 'quantiles', label: '历史分位', icon: <History className="h-4 w-4" /> },
];

export const ETFHeader = ({ basic, currentTab, onTabChange }: ETFHeaderProps) => {
  const navigate = useNavigate();

  return (
    <div className="bg-white shadow-sm sticky top-16 z-40">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between py-4">
          <div className="flex items-center space-x-4">
            <button
              onClick={() => navigate('/')}
              className="flex items-center space-x-1 text-neutral-600 hover:text-primary-700 transition-colors"
            >
              <ArrowLeft className="h-5 w-5" />
              <span className="hidden sm:inline">返回列表</span>
            </button>
            <div className="h-6 w-px bg-neutral-200" />
            <div>
              <div className="flex items-center space-x-2">
                <span className="font-mono text-2xl font-bold text-neutral-800">
                  {basic.code}
                </span>
                <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-primary-100 text-primary-700">
                  ETF
                </span>
              </div>
              <h1 className="text-lg font-semibold text-neutral-800">{basic.name}</h1>
              <p className="text-sm text-neutral-500">{basic.fullName}</p>
            </div>
          </div>

          <button
            onClick={() => navigate('/')}
            className="flex items-center space-x-1 px-3 py-1.5 rounded-lg bg-neutral-100 text-neutral-600 hover:bg-neutral-200 transition-colors"
          >
            <Home className="h-4 w-4" />
            <span className="text-sm">首页</span>
          </button>
        </div>

        <div className="border-t border-neutral-100">
          <div className="flex overflow-x-auto">
            {tabs.map((tab) => (
              <button
                key={tab.key}
                onClick={() => onTabChange(tab.key)}
                className={`flex items-center space-x-1.5 px-4 py-3 text-sm font-medium whitespace-nowrap transition-all duration-200 border-b-2 ${
                  currentTab === tab.key
                    ? 'border-primary-800 text-primary-800 bg-primary-50'
                    : 'border-transparent text-neutral-500 hover:text-neutral-700 hover:bg-neutral-50'
                }`}
              >
                {tab.icon}
                <span>{tab.label}</span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};