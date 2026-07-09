import React from 'react';
import { FinancialData, FinancialHealthScore } from '../../types';
import { AlertTriangle, CheckCircle, XCircle, MinusCircle } from 'lucide-react';

interface FinancialWarningProps {
  financial: FinancialData;
  previousFinancial?: FinancialData;
}

const calculateHealthScore = (financial: FinancialData, previousFinancial?: FinancialData): FinancialHealthScore => {
  const { balanceSheet, incomeStatement, cashFlow } = financial;
  const details: FinancialHealthScore['details'] = [];
  const warnings: string[] = [];
  let totalScore = 0;

  const checkIndicator = (indicator: string, score: number, status: 'pass' | 'warning' | 'danger', warningText?: string) => {
    details.push({ indicator, score, status });
    totalScore += score;
    if (status !== 'pass' && warningText) {
      warnings.push(warningText);
    }
  };

  if (incomeStatement.netProfit >= 0) {
    checkIndicator('净利润', 20, 'pass');
  } else {
    checkIndicator('净利润', 5, 'danger', '净利润为负，企业可能面临亏损');
  }

  if (previousFinancial) {
    const profitGrowth = ((incomeStatement.netProfit - previousFinancial.incomeStatement.netProfit) / Math.abs(previousFinancial.incomeStatement.netProfit)) * 100;
    if (profitGrowth >= -10) {
      checkIndicator('净利润增长率', 15, 'pass');
    } else if (profitGrowth >= -30) {
      checkIndicator('净利润增长率', 8, 'warning', '净利润同比下降超过10%');
    } else {
      checkIndicator('净利润增长率', 3, 'danger', '净利润同比下降超过30%');
    }
  } else {
    checkIndicator('净利润增长率', 10, 'pass');
  }

  const roe = balanceSheet.totalEquity > 0 ? (incomeStatement.netProfit / balanceSheet.totalEquity) * 100 : 0;
  if (roe >= 15) {
    checkIndicator('ROE', 15, 'pass');
  } else if (roe >= 5) {
    checkIndicator('ROE', 10, 'warning', 'ROE低于15%，盈利能力一般');
  } else {
    checkIndicator('ROE', 3, 'danger', 'ROE低于5%，盈利能力较差');
  }

  const debtRatio = balanceSheet.totalAssets > 0 ? (balanceSheet.totalLiabilities / balanceSheet.totalAssets) * 100 : 0;
  if (debtRatio <= 60) {
    checkIndicator('资产负债率', 15, 'pass');
  } else if (debtRatio <= 80) {
    checkIndicator('资产负债率', 10, 'warning', '资产负债率超过60%');
  } else {
    checkIndicator('资产负债率', 3, 'danger', '资产负债率超过80%，偿债压力大');
  }

  const currentRatio = balanceSheet.currentLiabilities > 0 ? balanceSheet.currentAssets / balanceSheet.currentLiabilities : 0;
  if (currentRatio >= 1.5) {
    checkIndicator('流动比率', 15, 'pass');
  } else if (currentRatio >= 1) {
    checkIndicator('流动比率', 10, 'warning', '流动比率低于1.5');
  } else {
    checkIndicator('流动比率', 3, 'danger', '流动比率低于1，短期偿债能力不足');
  }

  if (cashFlow.operatingCashFlow >= 0) {
    checkIndicator('经营现金流', 15, 'pass');
  } else {
    checkIndicator('经营现金流', 5, 'danger', '经营现金流为负，需关注资金状况');
  }

  let level: FinancialHealthScore['level'] = 'excellent';
  if (totalScore >= 80) level = 'excellent';
  else if (totalScore >= 60) level = 'good';
  else if (totalScore >= 40) level = 'fair';
  else level = 'poor';

  return { score: totalScore, level, warnings, details };
};

const levelConfig = {
  excellent: { label: '优秀', color: 'text-green-600', bg: 'bg-green-50', border: 'border-green-200' },
  good: { label: '良好', color: 'text-blue-600', bg: 'bg-blue-50', border: 'border-blue-200' },
  fair: { label: '一般', color: 'text-yellow-600', bg: 'bg-yellow-50', border: 'border-yellow-200' },
  poor: { label: '较差', color: 'text-red-600', bg: 'bg-red-50', border: 'border-red-200' },
};

export const FinancialWarning: React.FC<FinancialWarningProps> = ({ financial, previousFinancial }) => {
  const health = calculateHealthScore(financial, previousFinancial);
  const config = levelConfig[health.level];

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-gray-800">财务健康评估</h3>
        <div className={`px-4 py-2 rounded-full ${config.bg} ${config.border} border`}>
          <span className={`font-semibold ${config.color}`}>{config.label}</span>
        </div>
      </div>

      <div className="flex items-center gap-4 mb-6">
        <div className="text-5xl font-bold text-gray-800">{health.score}</div>
        <div className="flex-1">
          <div className="h-3 bg-gray-200 rounded-full overflow-hidden">
            <div
              className={`h-full ${
                health.level === 'excellent' ? 'bg-green-500' :
                health.level === 'good' ? 'bg-blue-500' :
                health.level === 'fair' ? 'bg-yellow-500' : 'bg-red-500'
              }`}
              style={{ width: `${health.score}%` }}
            />
          </div>
        </div>
      </div>

      {health.warnings.length > 0 && (
        <div className="mb-6">
          <h4 className="text-sm font-medium text-gray-600 mb-3 flex items-center gap-2">
            <AlertTriangle size={16} className="text-yellow-500" />
            风险提示
          </h4>
          <ul className="space-y-2">
            {health.warnings.map((warning, index) => (
              <li key={index} className="flex items-start gap-2 text-sm text-red-600">
                <XCircle size={14} className="mt-0.5 flex-shrink-0" />
                {warning}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div>
        <h4 className="text-sm font-medium text-gray-600 mb-3">评估详情</h4>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
          {health.details.map((item) => (
            <div
              key={item.indicator}
              className={`p-3 rounded-lg ${
                item.status === 'pass' ? 'bg-green-50' :
                item.status === 'warning' ? 'bg-yellow-50' : 'bg-red-50'
              }`}
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm text-gray-600">{item.indicator}</span>
                {item.status === 'pass' && <CheckCircle size={14} className="text-green-500" />}
                {item.status === 'warning' && <MinusCircle size={14} className="text-yellow-500" />}
                {item.status === 'danger' && <XCircle size={14} className="text-red-500" />}
              </div>
              <div className="text-lg font-semibold text-gray-800">{item.score}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default FinancialWarning;
