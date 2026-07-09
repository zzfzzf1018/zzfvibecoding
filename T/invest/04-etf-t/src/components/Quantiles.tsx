import { History, TrendingUp, TrendingDown } from 'lucide-react';
import type { ETFQuantile } from '@/types';
import { QuantileChart } from './QuantileChart';

interface QuantilesProps {
  quantiles: ETFQuantile[];
}

const getPercentileColor = (percentile: number) => {
  if (percentile < 30) return 'bg-green-500';
  if (percentile < 70) return 'bg-yellow-500';
  return 'bg-red-500';
};

export const Quantiles = ({ quantiles }: QuantilesProps) => {
  const latestData = quantiles[quantiles.length - 1];

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
        <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
          <div className="flex items-center space-x-2 mb-4">
            <History className="h-5 w-5 text-primary-600" />
            <h3 className="font-semibold text-neutral-800">最新PE分位</h3>
          </div>
          <div className="flex items-end justify-between">
            <div>
              <div className="text-4xl font-bold text-neutral-800">
                {latestData.pePercentile.toFixed(1)}%
              </div>
              <div className="text-sm text-neutral-500 mt-1">
                当前PE: {latestData.pe.toFixed(2)}
              </div>
            </div>
            <div className={`p-3 rounded-full ${getPercentileColor(latestData.pePercentile)}`}>
              {latestData.pePercentile < 30 ? (
                <TrendingDown className="h-6 w-6 text-white" />
              ) : (
                <TrendingUp className="h-6 w-6 text-white" />
              )}
            </div>
          </div>
          <div className="mt-4">
            <div className="w-full bg-neutral-200 rounded-full h-3">
              <div
                className={`h-3 rounded-full transition-all duration-500 ${getPercentileColor(latestData.pePercentile)}`}
                style={{ width: `${latestData.pePercentile}%` }}
              />
            </div>
            <div className="flex justify-between text-xs text-neutral-400 mt-2">
              <span>低估</span>
              <span>适中</span>
              <span>高估</span>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
          <div className="flex items-center space-x-2 mb-4">
            <History className="h-5 w-5 text-orange-600" />
            <h3 className="font-semibold text-neutral-800">最新PB分位</h3>
          </div>
          <div className="flex items-end justify-between">
            <div>
              <div className="text-4xl font-bold text-neutral-800">
                {latestData.pbPercentile.toFixed(1)}%
              </div>
              <div className="text-sm text-neutral-500 mt-1">
                当前PB: {latestData.pb.toFixed(2)}
              </div>
            </div>
            <div className={`p-3 rounded-full ${getPercentileColor(latestData.pbPercentile)}`}>
              {latestData.pbPercentile < 30 ? (
                <TrendingDown className="h-6 w-6 text-white" />
              ) : (
                <TrendingUp className="h-6 w-6 text-white" />
              )}
            </div>
          </div>
          <div className="mt-4">
            <div className="w-full bg-neutral-200 rounded-full h-3">
              <div
                className={`h-3 rounded-full transition-all duration-500 ${getPercentileColor(latestData.pbPercentile)}`}
                style={{ width: `${latestData.pbPercentile}%` }}
              />
            </div>
            <div className="flex justify-between text-xs text-neutral-400 mt-2">
              <span>低估</span>
              <span>适中</span>
              <span>高估</span>
            </div>
          </div>
        </div>
      </div>

      <QuantileChart
        quantiles={quantiles}
        title="PE/PB历史走势（近7个月）"
      />

      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <h3 className="font-semibold text-neutral-800 mb-4">历史分位数数据</h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-neutral-200">
                <th className="text-left py-3 px-4 text-sm font-semibold text-neutral-600">日期</th>
                <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">PE</th>
                <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">PE分位</th>
                <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">PB</th>
                <th className="text-right py-3 px-4 text-sm font-semibold text-neutral-600">PB分位</th>
              </tr>
            </thead>
            <tbody>
              {quantiles.map((q) => (
                <tr key={q.date} className="border-b border-neutral-100 hover:bg-neutral-50">
                  <td className="py-3 px-4 text-sm text-neutral-700">{q.date}</td>
                  <td className="py-3 px-4 text-right font-mono text-sm text-neutral-700">
                    {q.pe.toFixed(2)}
                  </td>
                  <td className="py-3 px-4 text-right">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                      q.pePercentile < 30
                        ? 'bg-green-100 text-green-700'
                        : q.pePercentile < 70
                        ? 'bg-yellow-100 text-yellow-700'
                        : 'bg-red-100 text-red-700'
                    }`}>
                      {q.pePercentile.toFixed(1)}%
                    </span>
                  </td>
                  <td className="py-3 px-4 text-right font-mono text-sm text-neutral-700">
                    {q.pb.toFixed(2)}
                  </td>
                  <td className="py-3 px-4 text-right">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                      q.pbPercentile < 30
                        ? 'bg-green-100 text-green-700'
                        : q.pbPercentile < 70
                        ? 'bg-yellow-100 text-yellow-700'
                        : 'bg-red-100 text-red-700'
                    }`}>
                      {q.pbPercentile.toFixed(1)}%
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <h3 className="font-semibold text-neutral-800 mb-4">历史分位数说明</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-neutral-50 rounded-lg p-4">
            <h4 className="font-medium text-neutral-700 mb-2">分位数含义</h4>
            <p className="text-sm text-neutral-600">
              历史分位数表示当前估值在所选时间段内的相对位置。例如，PE分位为30%表示当前PE处于过去一段时间内30%的位置，即比70%的时间都低。
            </p>
          </div>
          <div className="bg-neutral-50 rounded-lg p-4">
            <h4 className="font-medium text-neutral-700 mb-2">分位区间解读</h4>
            <ul className="text-sm text-neutral-600 space-y-1">
              <li className="flex items-center">
                <span className="w-2 h-2 bg-green-500 rounded-full mr-2" />
                0-30%: 低估区间，适合关注
              </li>
              <li className="flex items-center">
                <span className="w-2 h-2 bg-yellow-500 rounded-full mr-2" />
                30-70%: 适中区间，正常持有
              </li>
              <li className="flex items-center">
                <span className="w-2 h-2 bg-red-500 rounded-full mr-2" />
                70-100%: 高估区间，注意风险
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
};