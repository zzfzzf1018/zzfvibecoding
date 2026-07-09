import React from 'react';
import { Radar, RadarChart, PolarGrid, PolarAngleAxis, PolarRadiusAxis, ResponsiveContainer, Tooltip, Legend } from 'recharts';
import { FinancialData, Stock, FinancialRatios } from '../../types';
import { calculateRatios } from '../../utils/ratios';

interface RadarChartProps {
  data: Array<{ financial: FinancialData; stock: Stock }>;
}

const displayedKeys: (keyof FinancialRatios)[] = ['roe', 'roa', 'debtRatio', 'currentRatio', 'pe', 'pb'];
const ratioNames: Record<keyof FinancialRatios, string> = {
  roe: 'ROE',
  roa: 'ROA',
  debtRatio: '资产负债率',
  currentRatio: '流动比率',
  pe: 'PE',
  pb: 'PB',
  ps: 'PS',
  quickRatio: '速动比率',
  arTurnover: '应收账款周转率',
  inventoryTurnover: '存货周转率',
};

const colors = ['#ef4444', '#3b82f6', '#22c55e', '#f59e0b', '#8b5cf6', '#ec4899'];

export const StockRadarChart: React.FC<RadarChartProps> = ({ data }) => {
  const chartData = data.map((item, index) => {
    const ratios = calculateRatios(item.financial, item.stock);
    const result: Record<string, number> = {};
    
    displayedKeys.forEach(key => {
      result[key] = ratios[key] || 0;
    });

    return {
      name: item.stock.name,
      ...result,
    };
  });

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">多维度财务指标对比</h3>
      <div className="h-80">
        <ResponsiveContainer width="100%" height="100%">
          <RadarChart data={chartData}>
            <PolarGrid stroke="#e5e7eb" />
            <PolarAngleAxis 
              dataKey="name" 
              tick={{ fill: '#6b7280', fontSize: 12 }}
            />
            <PolarRadiusAxis 
              angle={30} 
              domain={[0, 50]} 
              tick={{ fill: '#9ca3af', fontSize: 10 }}
            />
            <Tooltip
              formatter={(value: number, name: string) => {
                return [`${value.toFixed(2)}`, ratioNames[name as keyof FinancialRatios] || name];
              }}
            />
            <Legend />
            {displayedKeys.map((key, idx) => (
              <Radar
                key={idx}
                name={ratioNames[key]}
                dataKey={key}
                stroke={colors[idx % colors.length]}
                fill={colors[idx % colors.length]}
                fillOpacity={0.2}
              />
            ))}
          </RadarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
};

export default StockRadarChart;
