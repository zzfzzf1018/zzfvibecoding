import { useState } from 'react';
import { FinancialData } from '../../types';

interface FinancialTableProps {
  financial: FinancialData;
}

type TabType = 'balance' | 'income' | 'cashflow';

const balanceSheetData = [
  { label: '流动资产', key: 'currentAssets' },
  { label: '非流动资产', key: 'nonCurrentAssets' },
  { label: '总资产', key: 'totalAssets' },
  { label: '流动负债', key: 'currentLiabilities' },
  { label: '非流动负债', key: 'nonCurrentLiabilities' },
  { label: '总负债', key: 'totalLiabilities' },
  { label: '净资产', key: 'totalEquity' },
];

const incomeStatementData = [
  { label: '营业收入', key: 'revenue' },
  { label: '营业利润', key: 'operatingProfit' },
  { label: '毛利润', key: 'grossProfit' },
  { label: '毛利率', key: 'grossMargin', isPercent: true },
  { label: '净利润', key: 'netProfit' },
  { label: '净利率', key: 'netMargin', isPercent: true },
  { label: '每股收益', key: 'eps', unit: '元' },
];

const cashFlowData = [
  { label: '经营活动现金流', key: 'operatingCashFlow' },
  { label: '投资活动现金流', key: 'investingCashFlow' },
  { label: '筹资活动现金流', key: 'financingCashFlow' },
  { label: '净现金流', key: 'netCashFlow' },
];

export default function FinancialTable({ financial }: FinancialTableProps) {
  const [activeTab, setActiveTab] = useState<TabType>('balance');

  const formatNumber = (num: number, isPercent = false, unit = ''): string => {
    if (isPercent) {
      return num.toFixed(2) + '%';
    }
    if (num >= 10000) {
      return (num / 10000).toFixed(2) + ' 亿' + unit;
    }
    return num.toFixed(2) + unit;
  };

  const renderTable = () => {
    switch (activeTab) {
      case 'balance':
        return balanceSheetData.map((item) => (
          <tr key={item.key} className="border-b border-gray-100 hover:bg-gray-50">
            <td className="py-3 px-4 text-gray-600">{item.label}</td>
            <td className="py-3 px-4 text-right font-medium text-gray-800">
              {formatNumber(financial.balanceSheet[item.key as keyof typeof financial.balanceSheet])}
            </td>
          </tr>
        ));
      case 'income':
        return incomeStatementData.map((item) => (
          <tr key={item.key} className="border-b border-gray-100 hover:bg-gray-50">
            <td className="py-3 px-4 text-gray-600">{item.label}</td>
            <td className="py-3 px-4 text-right font-medium text-gray-800">
              {formatNumber(
                financial.incomeStatement[item.key as keyof typeof financial.incomeStatement],
                item.isPercent,
                item.unit
              )}
            </td>
          </tr>
        ));
      case 'cashflow':
        return cashFlowData.map((item) => (
          <tr key={item.key} className="border-b border-gray-100 hover:bg-gray-50">
            <td className="py-3 px-4 text-gray-600">{item.label}</td>
            <td
              className={`py-3 px-4 text-right font-medium ${
                financial.cashFlow[item.key as keyof typeof financial.cashFlow] >= 0
                  ? 'text-green-600'
                  : 'text-red-600'
              }`}
            >
              {financial.cashFlow[item.key as keyof typeof financial.cashFlow] >= 0 ? '+' : ''}
              {formatNumber(financial.cashFlow[item.key as keyof typeof financial.cashFlow])}
            </td>
          </tr>
        ));
    }
  };

  return (
    <div className="bg-white rounded-xl shadow-md border border-gray-100 overflow-hidden">
      <div className="flex border-b border-gray-100">
        <button
          className={`flex-1 px-4 py-3 font-medium transition-colors ${
            activeTab === 'balance'
              ? 'text-blue-600 border-b-2 border-blue-600'
              : 'text-gray-600 hover:text-gray-800'
          }`}
          onClick={() => setActiveTab('balance')}
        >
          资产负债表
        </button>
        <button
          className={`flex-1 px-4 py-3 font-medium transition-colors ${
            activeTab === 'income'
              ? 'text-blue-600 border-b-2 border-blue-600'
              : 'text-gray-600 hover:text-gray-800'
          }`}
          onClick={() => setActiveTab('income')}
        >
          利润表
        </button>
        <button
          className={`flex-1 px-4 py-3 font-medium transition-colors ${
            activeTab === 'cashflow'
              ? 'text-blue-600 border-b-2 border-blue-600'
              : 'text-gray-600 hover:text-gray-800'
          }`}
          onClick={() => setActiveTab('cashflow')}
        >
          现金流量表
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full">
          <tbody>{renderTable()}</tbody>
        </table>
      </div>
    </div>
  );
}
