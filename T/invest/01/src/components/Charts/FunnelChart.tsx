import React from 'react';
import { FinancialData } from '../../types';

interface FunnelChartProps {
  financial: FinancialData;
}

export const FunnelChart: React.FC<FunnelChartProps> = ({ financial }) => {
  const { incomeStatement } = financial;
  const { revenue, grossProfit, operatingProfit, netProfit } = incomeStatement;

  const segments = [
    { label: '营业收入', value: revenue, color: '#ef4444', percentage: 100 },
    { label: '毛利润', value: grossProfit, color: '#f97316', percentage: revenue > 0 ? (grossProfit / revenue) * 100 : 0 },
    { label: '营业利润', value: operatingProfit, color: '#eab308', percentage: revenue > 0 ? (operatingProfit / revenue) * 100 : 0 },
    { label: '净利润', value: netProfit, color: '#22c55e', percentage: revenue > 0 ? (netProfit / revenue) * 100 : 0 },
  ];

  const formatValue = (value: number): string => {
    if (Math.abs(value) >= 100000000) {
      return (value / 100000000).toFixed(2) + '亿';
    }
    if (Math.abs(value) >= 10000) {
      return (value / 10000).toFixed(2) + '万';
    }
    return value.toFixed(2);
  };

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-6">利润构成分析</h3>
      
      <div className="flex flex-col items-center">
        {segments.map((segment, index) => {
          const width = `${80 - index * 15}%`;
          const height = '60px';
          
          return (
            <div key={index} className="w-full flex justify-center mb-2">
              <div
                className="rounded-lg flex items-center justify-between px-4 transition-all duration-300 hover:opacity-90"
                style={{
                  width,
                  height,
                  backgroundColor: segment.color,
                  boxShadow: `0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)`,
                }}
              >
                <span className="text-white font-medium">{segment.label}</span>
                <div className="text-right">
                  <div className="text-white font-bold">{formatValue(segment.value)}</div>
                  <div className="text-white text-xs opacity-80">{segment.percentage.toFixed(1)}%</div>
                </div>
              </div>
            </div>
          );
        })}

        <div className="mt-6 grid grid-cols-2 gap-4 text-sm">
          <div className="p-3 bg-gray-50 rounded-lg">
            <div className="text-gray-500">毛利率</div>
            <div className="font-semibold text-gray-800">{incomeStatement.grossMargin.toFixed(1)}%</div>
          </div>
          <div className="p-3 bg-gray-50 rounded-lg">
            <div className="text-gray-500">净利率</div>
            <div className="font-semibold text-gray-800">{incomeStatement.netMargin.toFixed(1)}%</div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FunnelChart;
