import { TrendingUp, TrendingDown } from 'lucide-react';
import type { ETFHolding } from '@/types';

interface HoldingsProps {
  holdings: ETFHolding[];
}

export const Holdings = ({ holdings }: HoldingsProps) => {
  const totalWeight = holdings.reduce((sum, h) => sum + h.weight, 0);

  return (
    <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-lg font-semibold text-neutral-800">前十大重仓股</h2>
        <span className="text-sm text-neutral-500">
          合计持仓: <span className="font-semibold text-neutral-700">{totalWeight.toFixed(2)}%</span>
        </span>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="border-b border-neutral-200">
              <th className="text-left py-3 px-4 text-sm font-semibold text-neutral-600">排名</th>
              <th className="text-left py-3 px-4 text-sm font-semibold text-neutral-600">股票代码</th>
              <th className="text-left py-3 px-4 text-sm font-semibold text-neutral-600">股票名称</th>
              <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">持仓比例</th>
              <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">持仓市值(亿)</th>
              <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">涨跌幅</th>
            </tr>
          </thead>
          <tbody>
            {holdings.map((holding) => (
              <tr key={holding.rank} className="border-b border-neutral-100 hover:bg-neutral-50">
                <td className="py-3 px-4 text-sm text-neutral-500">{holding.rank}</td>
                <td className="py-3 px-4 font-mono text-sm text-neutral-700">{holding.stockCode}</td>
                <td className="py-3 px-4 text-sm font-medium text-neutral-800">{holding.stockName}</td>
                <td className="py-3 px-4 text-right">
                  <div className="flex items-center justify-end space-x-2">
                    <span className="font-semibold text-neutral-700">{holding.weight.toFixed(2)}%</span>
                    <div className="w-20 bg-neutral-200 rounded-full h-1.5">
                      <div
                        className="bg-primary-600 h-1.5 rounded-full"
                        style={{ width: `${holding.weight}%` }}
                      />
                    </div>
                  </div>
                </td>
                <td className="py-3 px-4 text-right font-mono text-sm text-neutral-700">
                  {holding.marketValue.toFixed(2)}
                </td>
                <td className="py-3 px-4 text-right">
                  <div className={`flex items-center justify-end space-x-1 text-sm font-medium ${
                    holding.changePercent >= 0 ? 'text-up' : 'text-down'
                  }`}>
                    {holding.changePercent >= 0 ? (
                      <TrendingUp className="h-3 w-3" />
                    ) : (
                      <TrendingDown className="h-3 w-3" />
                    )}
                    <span>
                      {holding.changePercent >= 0 ? '+' : ''}{holding.changePercent.toFixed(2)}%
                    </span>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};