import { CreditCard, Percent, DollarSign, FileText } from 'lucide-react';
import type { ETFFees } from '@/types';

interface FeesProps {
  fees: ETFFees;
}

export const Fees = ({ fees }: FeesProps) => {
  const feeItems = [
    {
      icon: <Percent className="h-5 w-5" />,
      label: '管理费',
      value: fees.managementFeeRate,
      description: '按年计提',
      highlight: true,
    },
    {
      icon: <Percent className="h-5 w-5" />,
      label: '托管费',
      value: fees.custodianFeeRate,
      description: '按年计提',
      highlight: true,
    },
    {
      icon: <Percent className="h-5 w-5" />,
      label: '销售服务费',
      value: fees.salesServiceFeeRate,
      description: '按年计提',
    },
    {
      icon: <DollarSign className="h-5 w-5" />,
      label: '申购费',
      value: fees.subscriptionFee === 0 ? '0%' : `${fees.subscriptionFee}%`,
      description: '买入时收取',
    },
    {
      icon: <DollarSign className="h-5 w-5" />,
      label: '赎回费',
      value: fees.redemptionFee === 0 ? '0%' : `${fees.redemptionFee}%`,
      description: '卖出时收取',
    },
  ];

  const totalAnnualFee = fees.managementFee + fees.custodianFee + fees.salesServiceFee;

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-lg font-semibold text-neutral-800">费率信息</h2>
          <div className="flex items-center space-x-2">
            <CreditCard className="h-5 w-5 text-primary-600" />
            <span className="text-sm text-neutral-500">费用明细</span>
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {feeItems.map((item) => (
            <div
              key={item.label}
              className={`rounded-lg p-4 transition-all duration-200 ${
                item.highlight
                  ? 'bg-primary-50 border border-primary-200'
                  : 'bg-neutral-50 border border-neutral-100'
              }`}
            >
              <div className="flex items-center space-x-2 mb-2">
                <div className={`p-1.5 rounded-lg ${
                  item.highlight ? 'bg-primary-100' : 'bg-neutral-200'
                }`}>
                  <span className={item.highlight ? 'text-primary-700' : 'text-neutral-600'}>
                    {item.icon}
                  </span>
                </div>
                <span className="text-sm font-medium text-neutral-700">{item.label}</span>
              </div>
              <div className={`text-2xl font-bold ${item.highlight ? 'text-primary-800' : 'text-neutral-800'}`}>
                {item.value}
              </div>
              <div className="text-xs text-neutral-500 mt-1">{item.description}</div>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-gradient-to-r from-primary-800 to-primary-700 rounded-xl shadow-lg p-6 text-white">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold">年度总费用率</h3>
            <p className="text-primary-200 text-sm">管理费 + 托管费 + 销售服务费</p>
          </div>
          <div className="text-right">
            <div className="text-3xl font-bold">{totalAnnualFee.toFixed(2)}%</div>
            <p className="text-primary-200 text-xs mt-1">每年每万元</p>
          </div>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
        <div className="flex items-center space-x-2 mb-4">
          <FileText className="h-5 w-5 text-neutral-500" />
          <h3 className="font-semibold text-neutral-800">费用说明</h3>
        </div>
        <ul className="space-y-2 text-sm text-neutral-600">
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-primary-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>管理费用于支付基金管理公司的管理服务费用</span>
          </li>
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-primary-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>托管费用于支付托管银行的资产保管服务费用</span>
          </li>
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-primary-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>销售服务费用于支付销售渠道的服务费用</span>
          </li>
          <li className="flex items-start space-x-2">
            <span className="w-1.5 h-1.5 bg-primary-500 rounded-full mt-1.5 flex-shrink-0" />
            <span>ETF申赎通常不收取申购赎回费，但可能涉及佣金</span>
          </li>
        </ul>
      </div>
    </div>
  );
};