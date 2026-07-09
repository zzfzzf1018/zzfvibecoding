import { BarChart3, Percent, DollarSign } from 'lucide-react';
import type { ETFValuation } from '@/types';

interface ValuationProps {
  valuation: ETFValuation;
}

const getPercentileColor = (percentile: number) => {
  if (percentile < 30) return 'text-green-600 bg-green-100';
  if (percentile < 70) return 'text-yellow-600 bg-yellow-100';
  return 'text-red-600 bg-red-100';
};

const getPercentileLabel = (percentile: number) => {
  if (percentile < 30) return '偏低';
  if (percentile < 70) return '适中';
  return '偏高';
};

export const Valuation = ({ valuation }: ValuationProps) => {
  const valuationItems = [
    {
      icon: <BarChart3 className="h-5 w-5" />,
      label: '市盈率(PE)',
      value: valuation.pe,
      percentile: valuation.pePercentile,
      unit: '',
      description: '当前PE处于历史分位',
    },
    {
      icon: <BarChart3 className="h-5 w-5" />,
      label: '市净率(PB)',
      value: valuation.pb,
      percentile: valuation.pbPercentile,
      unit: '',
      description: '当前PB处于历史分位',
    },
    {
      icon: <BarChart3 className="h-5 w-5" />,
      label: '市销率(PS)',
      value: valuation.ps,
      percentile: valuation.psPercentile,
      unit: '',
      description: '当前PS处于历史分位',
    },
    {
      icon: <Percent className="h-5 w-5" />,
      label: '盈利收益率',
      value: valuation.earningsYield,
      percentile: null,
      unit: '%',
      description: '市盈率的倒数',
    },
    {
      icon: <DollarSign className="h-5 w-5" />,
      label: '股息率',
      value: valuation.dividendYield,
      percentile: null,
      unit: '%',
      description: '年度股息与价格比率',
    },
  ];

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {valuationItems.slice(0, 3).map((item) => (
          <div
            key={item.label}
            className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6"
          >
            <div className="flex items-center space-x-2 mb-4">
              <div className="p-2 bg-primary-100 rounded-lg">
                <span className="text-primary-700">{item.icon}</span>
              </div>
              <span className="font-medium text-neutral-700">{item.label}</span>
            </div>

            <div className="flex items-end justify-between mb-4">
              <div>
                <div className="text-3xl font-bold text-neutral-800">
                  {item.value.toFixed(2)}{item.unit}
                </div>
                <div className="text-sm text-neutral-500 mt-1">{item.description}</div>
              </div>
              {item.percentile !== null && (
                <span className={`px-3 py-1 rounded-full text-sm font-medium ${getPercentileColor(item.percentile)}`}>
                  {getPercentileLabel(item.percentile)}
                </span>
              )}
            </div>

            {item.percentile !== null && (
              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-neutral-500">历史分位</span>
                  <span className="font-semibold text-neutral-700">
                    {item.percentile.toFixed(1)}%
                  </span>
                </div>
                <div className="w-full bg-neutral-200 rounded-full h-2">
                  <div
                    className={`h-2 rounded-full transition-all duration-500 ${
                      item.percentile < 30
                        ? 'bg-green-500'
                        : item.percentile < 70
                        ? 'bg-yellow-500'
                        : 'bg-red-500'
                    }`}
                    style={{ width: `${item.percentile}%` }}
                  />
                </div>
                <div className="flex justify-between text-xs text-neutral-400">
                  <span>0%</span>
                  <span>50%</span>
                  <span>100%</span>
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
        {valuationItems.slice(3).map((item) => (
          <div
            key={item.label}
            className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6"
          >
            <div className="flex items-center space-x-2 mb-4">
              <div className="p-2 bg-primary-100 rounded-lg">
                <span className="text-primary-700">{item.icon}</span>
              </div>
              <span className="font-medium text-neutral-700">{item.label}</span>
            </div>
            <div className="text-3xl font-bold text-neutral-800">
              {item.value.toFixed(2)}{item.unit}
            </div>
            <div className="text-sm text-neutral-500 mt-2">{item.description}</div>
          </div>
        ))}
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <h3 className="font-semibold text-neutral-800 mb-4">估值指标说明</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div className="bg-neutral-50 rounded-lg p-4">
            <h4 className="font-medium text-neutral-700 mb-2">市盈率(PE)</h4>
            <p className="text-sm text-neutral-600">
              股价与每股收益的比率，反映投资者愿意为每单位盈利支付的价格。较低的PE通常表示估值较低。
            </p>
          </div>
          <div className="bg-neutral-50 rounded-lg p-4">
            <h4 className="font-medium text-neutral-700 mb-2">市净率(PB)</h4>
            <p className="text-sm text-neutral-600">
              股价与每股净资产的比率，反映投资者对公司资产价值的认可程度。适合金融、资源类股票。
            </p>
          </div>
          <div className="bg-neutral-50 rounded-lg p-4">
            <h4 className="font-medium text-neutral-700 mb-2">历史分位</h4>
            <p className="text-sm text-neutral-600">
              当前估值在过去一段时间内所处的位置，分位越低表示当前估值越便宜，分位越高表示越贵。
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};