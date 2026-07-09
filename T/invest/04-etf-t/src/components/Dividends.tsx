import { Gift, Calendar, DollarSign, FileText } from 'lucide-react';
import type { ETFDividend } from '@/types';

interface DividendsProps {
  dividends: ETFDividend[];
}

export const Dividends = ({ dividends }: DividendsProps) => {
  const totalDividend = dividends.reduce((sum, d) => sum + d.dividendAmount, 0);

  return (
    <div className="space-y-6">
      <div className="bg-gradient-to-r from-green-500 to-green-600 rounded-xl shadow-lg p-6 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="p-2 bg-white/20 rounded-lg">
              <Gift className="h-6 w-6" />
            </div>
            <div>
              <h3 className="text-lg font-semibold">累计分红金额</h3>
              <p className="text-green-100 text-sm">近5年累计</p>
            </div>
          </div>
          <div className="text-right">
            <div className="text-3xl font-bold">¥{totalDividend.toFixed(3)}</div>
            <p className="text-green-100 text-xs mt-1">每份累计分红</p>
          </div>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-lg font-semibold text-neutral-800">分红记录</h2>
          <span className="text-sm text-neutral-500">共 {dividends.length} 次分红</span>
        </div>

        <div className="space-y-4">
          {dividends.map((dividend, index) => (
            <div
              key={index}
              className="bg-neutral-50 rounded-lg p-4 hover:bg-neutral-100 transition-colors"
            >
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center space-x-2">
                  <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-700">
                    {dividend.dividendType}
                  </span>
                  <span className="text-sm text-neutral-500">{dividend.dividendDate}</span>
                </div>
                <div className="flex items-center space-x-1">
                  <DollarSign className="h-4 w-4 text-green-600" />
                  <span className="text-xl font-bold text-green-600">¥{dividend.dividendAmount.toFixed(3)}</span>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div className="flex items-center space-x-2">
                  <Calendar className="h-4 w-4 text-neutral-400" />
                  <span className="text-neutral-500">除息日:</span>
                  <span className="font-medium text-neutral-700">{dividend.exDividendDate}</span>
                </div>
                <div className="flex items-center space-x-2">
                  <FileText className="h-4 w-4 text-neutral-400" />
                  <span className="text-neutral-500">登记日:</span>
                  <span className="font-medium text-neutral-700">{dividend.recordDate}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <div className="flex items-center space-x-2 mb-4">
          <Gift className="h-5 w-5 text-neutral-500" />
          <h3 className="font-semibold text-neutral-800">分红说明</h3>
        </div>
        <ul className="space-y-2 text-sm text-neutral-600">
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-green-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>分红金额为每基金份额分红金额</span>
          </li>
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-green-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>除息日是指基金份额净值开始扣除分红金额的日期</span>
          </li>
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-green-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>登记日是指确认分红资格的日期</span>
          </li>
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-green-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>ETF分红通常为现金分红方式</span>
          </li>
        </ul>
      </div>
    </div>
  );
};