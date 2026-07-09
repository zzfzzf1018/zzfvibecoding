import React from 'react';
import { FinancialData } from '../../types';

interface DuPontChartProps {
  financial: FinancialData;
}

export const DuPontChart: React.FC<DuPontChartProps> = ({ financial }) => {
  const { balanceSheet, incomeStatement } = financial;

  const roe = balanceSheet.totalEquity > 0 
    ? (incomeStatement.netProfit / balanceSheet.totalEquity) * 100 
    : 0;

  const roa = balanceSheet.totalAssets > 0 
    ? (incomeStatement.netProfit / balanceSheet.totalAssets) * 100 
    : 0;

  const leverage = balanceSheet.totalEquity > 0 
    ? balanceSheet.totalAssets / balanceSheet.totalEquity 
    : 0;

  const netMargin = incomeStatement.revenue > 0 
    ? (incomeStatement.netProfit / incomeStatement.revenue) * 100 
    : 0;

  const assetTurnover = balanceSheet.totalAssets > 0 
    ? incomeStatement.revenue / balanceSheet.totalAssets 
    : 0;

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-6">杜邦分析</h3>
      
      <div className="flex flex-col items-center">
        <div className="bg-gradient-to-br from-blue-500 to-blue-600 text-white rounded-xl p-6 text-center mb-4">
          <div className="text-sm opacity-80 mb-1">ROE 净资产收益率</div>
          <div className="text-4xl font-bold">{roe.toFixed(2)}%</div>
        </div>

        <div className="flex items-center gap-2 mb-4">
          <div className="w-16 h-1 bg-gray-300" />
          <span className="text-gray-400">×</span>
          <div className="w-16 h-1 bg-gray-300" />
        </div>

        <div className="flex gap-8">
          <div className="bg-gradient-to-br from-green-500 to-green-600 text-white rounded-xl p-5 text-center">
            <div className="text-xs opacity-80 mb-1">ROA 总资产收益率</div>
            <div className="text-2xl font-bold">{roa.toFixed(2)}%</div>
          </div>
          <div className="bg-gradient-to-br from-purple-500 to-purple-600 text-white rounded-xl p-5 text-center">
            <div className="text-xs opacity-80 mb-1">杠杆比率</div>
            <div className="text-2xl font-bold">{leverage.toFixed(2)}x</div>
          </div>
        </div>

        <div className="flex items-center gap-2 mt-4 mb-4">
          <div className="w-24 h-1 bg-gray-300" />
          <span className="text-gray-400">×</span>
          <div className="w-24 h-1 bg-gray-300" />
        </div>

        <div className="flex gap-8">
          <div className="bg-gradient-to-br from-orange-500 to-orange-600 text-white rounded-xl p-5 text-center">
            <div className="text-xs opacity-80 mb-1">净利率</div>
            <div className="text-2xl font-bold">{netMargin.toFixed(2)}%</div>
          </div>
          <div className="bg-gradient-to-br from-cyan-500 to-cyan-600 text-white rounded-xl p-5 text-center">
            <div className="text-xs opacity-80 mb-1">总资产周转率</div>
            <div className="text-2xl font-bold">{assetTurnover.toFixed(2)}x</div>
          </div>
        </div>

        <div className="mt-6 p-4 bg-gray-50 rounded-lg text-sm text-gray-600">
          <p><strong>杜邦公式：</strong></p>
          <p className="mt-1">ROE = ROA × 杠杆比率</p>
          <p className="mt-1">ROE = 净利率 × 总资产周转率 × 杠杆比率</p>
        </div>
      </div>
    </div>
  );
};

export default DuPontChart;
