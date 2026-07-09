import React, { useState } from 'react';
import { getIndustries, getIndustryData, getLatestFinancialData } from '../../data/mockData';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, LineChart, Line, Cell } from 'recharts';
import { formatNumber } from '../../utils/ratios';

interface IndustryCompareProps {
  onSelectStock: (stock: ReturnType<typeof getIndustryData>['stocks'][0]) => void;
}

export const IndustryCompare: React.FC<IndustryCompareProps> = ({ onSelectStock }) => {
  const industries = getIndustries();
  const [selectedIndustry, setSelectedIndustry] = useState(industries[0] || '');

  const industryData = selectedIndustry ? getIndustryData(selectedIndustry) : null;

  if (!industryData) {
    return (
      <div className="bg-white rounded-xl shadow-sm p-6">
        <h3 className="text-lg font-semibold text-gray-800 mb-6">行业对比分析</h3>
        <p className="text-gray-500">暂无行业数据</p>
      </div>
    );
  }

  const { stocks, averages } = industryData;

  const stockFinancials = stocks.map(stock => {
    const financial = getLatestFinancialData(stock.code, stock.market);
    return {
      name: stock.name,
      code: stock.code,
      market: stock.market,
      revenue: financial?.incomeStatement.revenue || 0,
      netProfit: financial?.incomeStatement.netProfit || 0,
      roe: financial && financial.balanceSheet.totalEquity > 0
        ? (financial.incomeStatement.netProfit / financial.balanceSheet.totalEquity) * 100
        : 0,
      debtRatio: financial && financial.balanceSheet.totalAssets > 0
        ? (financial.balanceSheet.totalLiabilities / financial.balanceSheet.totalAssets) * 100
        : 0,
    };
  });

  const chartData = [
    ...stockFinancials.map(s => ({ ...s, type: 'actual' })),
    {
      name: '行业平均',
      code: '-',
      market: '-',
      revenue: averages.revenue,
      netProfit: averages.netProfit,
      roe: averages.roe,
      debtRatio: averages.debtRatio,
      type: 'average',
    },
  ];

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-6">行业对比分析</h3>

      <div className="mb-6">
        <label className="text-sm font-medium text-gray-600 mb-2 block">选择行业</label>
        <select
          value={selectedIndustry}
          onChange={(e) => setSelectedIndustry(e.target.value)}
          className="w-full max-w-xs px-4 py-2 border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          {industries.map(industry => (
            <option key={industry} value={industry}>{industry}</option>
          ))}
        </select>
      </div>

      <div className="mb-6">
        <h4 className="text-sm font-medium text-gray-600 mb-3">行业平均指标</h4>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div className="bg-blue-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">平均PE</div>
            <div className="text-lg font-semibold text-gray-800">{averages.pe.toFixed(2)}</div>
          </div>
          <div className="bg-green-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">平均ROE</div>
            <div className="text-lg font-semibold text-gray-800">{averages.roe.toFixed(2)}%</div>
          </div>
          <div className="bg-purple-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">平均PB</div>
            <div className="text-lg font-semibold text-gray-800">{averages.pb.toFixed(2)}</div>
          </div>
          <div className="bg-orange-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">平均负债率</div>
            <div className="text-lg font-semibold text-gray-800">{averages.debtRatio.toFixed(2)}%</div>
          </div>
        </div>
      </div>

      <div className="mb-6">
        <h4 className="text-sm font-medium text-gray-600 mb-3">营业收入对比</h4>
        <div className="h-50">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="name" tick={{ fontSize: 10 }} />
              <YAxis tick={{ fontSize: 10 }} />
              <Tooltip formatter={(value: number) => [formatNumber(value), '']} />
              <Legend />
              <Bar dataKey="revenue" name="营业收入">
                {chartData.map((entry, index) => (
                  <Cell key={`revenue-${index}`} fill={entry.type === 'average' ? '#f59e0b' : '#3b82f6'} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="mb-6">
        <h4 className="text-sm font-medium text-gray-600 mb-3">ROE对比</h4>
        <div className="h-50">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="name" tick={{ fontSize: 10 }} />
              <YAxis tick={{ fontSize: 10 }} />
              <Tooltip formatter={(value: number) => [`${value.toFixed(2)}%`, '']} />
              <Legend />
              <Bar dataKey="roe" name="ROE">
                {chartData.map((entry, index) => (
                  <Cell key={`roe-${index}`} fill={entry.type === 'average' ? '#f59e0b' : '#22c55e'} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="overflow-x-auto">
        <h4 className="text-sm font-medium text-gray-600 mb-3">行业股票列表</h4>
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-2 text-left font-medium text-gray-600">股票</th>
              <th className="px-4 py-2 text-right font-medium text-gray-600">价格</th>
              <th className="px-4 py-2 text-right font-medium text-gray-600">营业收入</th>
              <th className="px-4 py-2 text-right font-medium text-gray-600">净利润</th>
              <th className="px-4 py-2 text-right font-medium text-gray-600">ROE</th>
              <th className="px-4 py-2 text-center font-medium text-gray-600">操作</th>
            </tr>
          </thead>
          <tbody>
            {stockFinancials.map((item, index) => (
              <tr key={index} className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="font-medium text-gray-800">{item.name}</div>
                  <div className="text-xs text-gray-500">{item.code} · {item.market}</div>
                </td>
                <td className="px-4 py-3 text-right font-medium text-gray-800">
                  {stocks[index]?.price.toFixed(2)}
                </td>
                <td className="px-4 py-3 text-right text-gray-600">{formatNumber(item.revenue)}</td>
                <td className="px-4 py-3 text-right text-gray-600">{formatNumber(item.netProfit)}</td>
                <td className="px-4 py-3 text-right text-gray-600">{item.roe.toFixed(2)}%</td>
                <td className="px-4 py-3 text-center">
                  <button
                    onClick={() => onSelectStock(stocks[index])}
                    className="px-3 py-1 text-sm bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors"
                  >
                    查看
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default IndustryCompare;
