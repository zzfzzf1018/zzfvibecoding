import { ArrowUpRight, BarChart3, DollarSign } from 'lucide-react';
import type { ETF } from '@/types';
import { Link } from 'react-router-dom';

interface ETFCardProps {
  etf: ETF;
}

export const ETFCard = ({ etf }: ETFCardProps) => {
  const isUp = etf.changePercent >= 0;

  return (
    <Link
      to={`/etf/${etf.code}`}
      className="bg-white rounded-xl shadow-sm hover:shadow-lg transition-all duration-300 border border-neutral-100 overflow-hidden group cursor-pointer"
    >
      <div className="p-5">
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center space-x-2">
              <span className="font-mono font-bold text-lg text-neutral-800">
                {etf.code}
              </span>
              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-primary-100 text-primary-700">
                {etf.categoryName}
              </span>
            </div>
            <h3 className="text-neutral-800 font-semibold mt-1 group-hover:text-primary-700 transition-colors">
              {etf.name}
            </h3>
            <p className="text-xs text-neutral-500 mt-0.5">{etf.fundCompany}</p>
          </div>
          <ArrowUpRight className="h-5 w-5 text-neutral-300 group-hover:text-primary-600 transition-colors" />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="bg-neutral-50 rounded-lg p-3">
            <div className="flex items-center text-xs text-neutral-500 mb-1">
              <DollarSign className="h-3 w-3 mr-1" />
              <span>最新净值</span>
            </div>
            <div className="font-mono text-lg font-semibold text-neutral-800">
              {etf.nav.toFixed(4)}
            </div>
            <div className={`text-xs font-medium mt-0.5 ${isUp ? 'text-up' : 'text-down'}`}>
              {isUp ? '+' : ''}{etf.changePercent.toFixed(2)}%
            </div>
          </div>

          <div className="bg-neutral-50 rounded-lg p-3">
            <div className="flex items-center text-xs text-neutral-500 mb-1">
              <BarChart3 className="h-3 w-3 mr-1" />
              <span>规模(亿)</span>
            </div>
            <div className="font-mono text-lg font-semibold text-neutral-800">
              {etf.scale.toFixed(2)}
            </div>
            <div className="text-xs text-neutral-500 mt-0.5">
              {etf.trackingIndex}
            </div>
          </div>
        </div>

        <div className="mt-4 pt-4 border-t border-neutral-100">
          <div className="flex items-center justify-between text-sm">
            <span className="text-neutral-500">近一年收益</span>
            <span className={`font-semibold ${etf.oneYearReturn >= 0 ? 'text-up' : 'text-down'}`}>
              {etf.oneYearReturn >= 0 ? '+' : ''}{etf.oneYearReturn.toFixed(2)}%
            </span>
          </div>
          {etf.pe > 0 && (
            <div className="flex items-center justify-between text-sm mt-2">
              <span className="text-neutral-500">PE/PB</span>
              <span className="font-mono text-neutral-700">
                {etf.pe.toFixed(2)}/{etf.pb.toFixed(2)}
              </span>
            </div>
          )}
        </div>
      </div>
    </Link>
  );
};