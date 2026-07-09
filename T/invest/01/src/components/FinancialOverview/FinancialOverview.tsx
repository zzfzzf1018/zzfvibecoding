import { Wallet, TrendingUp, BarChart3, Banknote } from 'lucide-react';
import { FinancialData, Stock } from '../../types';
import { useCompareStore } from '../../store/useCompareStore';

interface FinancialOverviewProps {
  financial: FinancialData;
  stock?: Stock;
}

export default function FinancialOverview({ financial, stock }: FinancialOverviewProps) {
  const { addItem, isInCompare, removeItem } = useCompareStore();
  const isAdded = isInCompare(financial.stockCode);

  const handleToggleCompare = () => {
    if (isAdded) {
      removeItem(financial.stockCode);
    } else if (stock) {
      addItem(stock, financial);
    }
  };

  const formatNumber = (num: number): string => {
    if (num >= 10000) {
      return (num / 10000).toFixed(2) + ' 亿';
    }
    return num.toFixed(2);
  };

  const stats = [
    {
      label: '总资产',
      value: formatNumber(financial.balanceSheet.totalAssets),
      icon: Wallet,
      color: 'from-blue-500 to-blue-600',
      bgColor: 'bg-blue-50',
    },
    {
      label: '营业收入',
      value: formatNumber(financial.incomeStatement.revenue),
      icon: BarChart3,
      color: 'from-green-500 to-green-600',
      bgColor: 'bg-green-50',
    },
    {
      label: '净利润',
      value: formatNumber(financial.incomeStatement.netProfit),
      icon: TrendingUp,
      color: 'from-purple-500 to-purple-600',
      bgColor: 'bg-purple-50',
    },
    {
      label: '经营现金流',
      value: formatNumber(financial.cashFlow.operatingCashFlow),
      icon: Banknote,
      color: 'from-orange-500 to-orange-600',
      bgColor: 'bg-orange-50',
    },
    {
      label: '每股收益',
      value: financial.incomeStatement.eps.toFixed(2) + ' 元',
      icon: TrendingUp,
      color: 'from-cyan-500 to-cyan-600',
      bgColor: 'bg-cyan-50',
    },
    {
      label: '净资产',
      value: formatNumber(financial.balanceSheet.totalEquity),
      icon: Wallet,
      color: 'from-pink-500 to-pink-600',
      bgColor: 'bg-pink-50',
    },
  ];

  return (
    <div className="bg-white rounded-xl shadow-md border border-gray-100 overflow-hidden">
      <div className="p-4 border-b border-gray-100 flex items-center justify-between">
        <div>
          <h2 className="text-lg font-bold text-gray-800">
            {financial.stockName}
            <span className="text-sm font-normal text-gray-500 ml-2">
              ({financial.market} · {financial.stockCode})
            </span>
          </h2>
          <p className="text-sm text-gray-500">
            报告期: {financial.reportDate}
          </p>
        </div>
        <button
          onClick={handleToggleCompare}
          className={`px-4 py-2 rounded-lg font-medium transition-all ${
            isAdded
              ? 'bg-gray-100 text-gray-600 hover:bg-gray-200'
              : 'bg-blue-600 text-white hover:bg-blue-700'
          }`}
        >
          {isAdded ? '已添加对比' : '添加对比'}
        </button>
      </div>

      <div className="p-4 grid grid-cols-2 md:grid-cols-3 gap-4">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <div
              key={stat.label}
              className={`${stat.bgColor} rounded-lg p-4 transition-transform hover:scale-105`}
            >
              <div className="flex items-center mb-2">
                <div className={`w-8 h-8 rounded-lg bg-gradient-to-br ${stat.color} flex items-center justify-center`}>
                  <Icon className="w-4 h-4 text-white" />
                </div>
                <span className="text-sm text-gray-600 ml-2">{stat.label}</span>
              </div>
              <div className="text-xl font-bold text-gray-800">{stat.value}</div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
