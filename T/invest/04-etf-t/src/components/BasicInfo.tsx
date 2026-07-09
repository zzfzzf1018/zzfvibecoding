import { Building2, Calendar, Scale, BarChart3, Percent, Clock } from 'lucide-react';
import type { ETFBasicInfo } from '@/types';

interface BasicInfoProps {
  basic: ETFBasicInfo;
}

export const BasicInfo = ({ basic }: BasicInfoProps) => {
  const infoItems = [
    {
      icon: <Building2 className="h-5 w-5" />,
      label: '基金公司',
      value: basic.fundCompany,
    },
    {
      icon: <Calendar className="h-5 w-5" />,
      label: '成立日期',
      value: basic.establishDate,
    },
    {
      icon: <Scale className="h-5 w-5" />,
      label: '基金规模',
      value: `${basic.scale.toFixed(2)} 亿元`,
      subValue: `(${basic.scaleDate})`,
    },
    {
      icon: <BarChart3 className="h-5 w-5" />,
      label: '跟踪指数',
      value: basic.trackingIndex,
      subValue: basic.indexCode,
    },
    {
      icon: <Calendar className="h-5 w-5" />,
      label: '最新报告',
      value: basic.latestReportDate,
    },
    {
      icon: <Percent className="h-5 w-5" />,
      label: '管理费率',
      value: `${basic.managementFeeRate}%`,
    },
    {
      icon: <Percent className="h-5 w-5" />,
      label: '托管费率',
      value: `${basic.custodianFeeRate}%`,
    },
    {
      icon: <Clock className="h-5 w-5" />,
      label: '销售服务费',
      value: `${basic.salesServiceFeeRate}%`,
    },
  ];

  return (
    <div className="bg-white rounded-xl shadow-sm border border-neutral-100 p-6">
      <h2 className="text-lg font-semibold text-neutral-800 mb-6">基本信息</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {infoItems.map((item) => (
          <div
            key={item.label}
            className="bg-neutral-50 rounded-lg p-4 hover:bg-neutral-100 transition-colors"
          >
            <div className="flex items-center space-x-2 mb-2">
              <div className="p-1.5 bg-primary-100 rounded-lg">
                <span className="text-primary-700">{item.icon}</span>
              </div>
              <span className="text-sm text-neutral-500">{item.label}</span>
            </div>
            <div className="font-semibold text-neutral-800">{item.value}</div>
            {item.subValue && (
              <div className="text-xs text-neutral-400 mt-1">{item.subValue}</div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};