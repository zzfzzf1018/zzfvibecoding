import React from 'react';
import { FinancialData, Stock, FinancialRatios as FinancialRatiosType } from '../../types';
import { calculateRatios } from '../../utils/ratios';

interface FinancialRatiosProps {
  financial: FinancialData;
  stock?: Stock;
}

const ratioConfig = [
  { key: 'pe' as const, label: 'PE(市盈率)', unit: '倍', category: 'valuation' as const },
  { key: 'pb' as const, label: 'PB(市净率)', unit: '倍', category: 'valuation' as const },
  { key: 'ps' as const, label: 'PS(市销率)', unit: '倍', category: 'valuation' as const },
  { key: 'roe' as const, label: 'ROE(净资产收益率)', unit: '%', category: 'profitability' as const },
  { key: 'roa' as const, label: 'ROA(总资产收益率)', unit: '%', category: 'profitability' as const },
  { key: 'debtRatio' as const, label: '资产负债率', unit: '%', category: 'solvency' as const },
  { key: 'currentRatio' as const, label: '流动比率', unit: '', category: 'solvency' as const },
  { key: 'quickRatio' as const, label: '速动比率', unit: '', category: 'solvency' as const },
  { key: 'arTurnover' as const, label: '应收账款周转率', unit: '次', category: 'operation' as const },
  { key: 'inventoryTurnover' as const, label: '存货周转率', unit: '次', category: 'operation' as const },
];

const categoryLabels = {
  valuation: '估值指标',
  profitability: '盈利能力',
  solvency: '偿债能力',
  operation: '运营能力',
};

export const FinancialRatiosComponent: React.FC<FinancialRatiosProps> = ({ financial, stock }) => {
  const ratios = calculateRatios(financial, stock);

  const categories = ['valuation', 'profitability', 'solvency', 'operation'] as const;

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-6">财务比率分析</h3>
      
      {categories.map((category) => (
        <div key={category} className="mb-6">
          <h4 className="text-sm font-medium text-gray-600 mb-3">{categoryLabels[category]}</h4>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
            {ratioConfig
              .filter((r) => r.category === category)
              .map(({ key, label, unit }) => (
                <div
                  key={key}
                  className="bg-gray-50 rounded-lg p-3 text-center"
                >
                  <div className="text-xs text-gray-500 mb-1">{label}</div>
                  <div className="text-lg font-semibold text-gray-800">
                    {ratios[key]}
                    <span className="text-sm font-normal text-gray-500 ml-0.5">{unit}</span>
                  </div>
                </div>
              ))}
          </div>
        </div>
      ))}
    </div>
  );
};

export default FinancialRatiosComponent;
