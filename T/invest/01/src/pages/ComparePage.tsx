import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Trash2, BarChart3, PieChart, LineChart } from 'lucide-react';
import { useCompareStore } from '../store/useCompareStore';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  PieChart as RePieChart,
  Pie,
  Cell,
  LineChart as ReLineChart,
  Line,
} from 'recharts';
import { useState } from 'react';

type ChartType = 'bar' | 'pie' | 'line';

const COLORS = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899'];

interface ComparePageProps {
  className?: string;
}

export default function ComparePage({ className }: ComparePageProps) {
  const navigate = useNavigate();
  const { items, removeItem, clearAll } = useCompareStore();
  const [chartType, setChartType] = useState<ChartType>('bar');

  const latestData = items.map((item) => {
    const financial = item.financial;
    return {
      name: item.stock.name,
      revenue: financial.incomeStatement.revenue / 10000,
      netProfit: financial.incomeStatement.netProfit / 10000,
      totalAssets: financial.balanceSheet.totalAssets / 10000,
      operatingCashFlow: financial.cashFlow.operatingCashFlow / 10000,
    };
  });

  const pieData = items.map((item, index) => ({
    name: item.stock.name,
    value: item.financial.incomeStatement.revenue / 10000,
    color: COLORS[index % COLORS.length],
  }));

  const lineData = items[0]?.financial.reportDate ? ['2022', '2023', '2024'] : [];
  
  const lineChartData = lineData.map((year) => {
    const result: Record<string, number | string> = { name: year };
    items.forEach((item) => {
      const financial = item.financial;
      const dataPoint = {
        revenue: financial.incomeStatement.revenue / 10000,
        netProfit: financial.incomeStatement.netProfit / 10000,
      };
      result[`${item.stock.name}_revenue`] = dataPoint.revenue;
      result[`${item.stock.name}_netProfit`] = dataPoint.netProfit;
    });
    return result;
  });

  if (items.length === 0) {
    return (
      <div className={`${className} min-h-screen bg-gradient-to-br from-slate-50 to-blue-50`}>
        <div className="container mx-auto px-4 py-8">
          <div className="bg-white rounded-xl shadow-md border border-gray-100 p-8 text-center">
            <div className="w-20 h-20 bg-gray-100 rounded-full flex items-center justify-center mx-auto mb-4">
              <BarChart3 className="w-10 h-10 text-gray-400" />
            </div>
            <h2 className="text-xl font-bold text-gray-800 mb-2">暂无对比数据</h2>
            <p className="text-gray-500 mb-6">请在首页添加股票到对比列表</p>
            <button
              onClick={() => navigate('/')}
              className="px-6 py-3 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors"
            >
              返回首页
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={`${className} min-h-screen bg-gradient-to-br from-slate-50 to-blue-50`}>
      <div className="container mx-auto px-4 py-8">
        <div className="flex items-center justify-between mb-6">
          <button
            onClick={() => navigate('/')}
            className="flex items-center text-gray-600 hover:text-gray-800 transition-colors"
          >
            <ArrowLeft className="w-5 h-5 mr-2" />
            返回首页
          </button>
          {items.length > 1 && (
            <button
              onClick={clearAll}
              className="flex items-center text-red-600 hover:text-red-700 transition-colors"
            >
              <Trash2 className="w-4 h-4 mr-1" />
              清空对比
            </button>
          )}
        </div>

        <div className="bg-white rounded-xl shadow-md border border-gray-100 p-6 mb-6">
          <h2 className="text-lg font-bold text-gray-800 mb-4">对比列表</h2>
          <div className="flex flex-wrap gap-3">
            {items.map((item, index) => (
              <div
                key={item.stock.code}
                className="flex items-center bg-gray-100 rounded-lg px-4 py-2"
              >
                <span
                  className="w-3 h-3 rounded-full mr-2"
                  style={{ backgroundColor: COLORS[index % COLORS.length] }}
                />
                <span className="font-medium text-gray-800">{item.stock.name}</span>
                <span className="text-gray-500 text-sm ml-1">({item.stock.market})</span>
                <button
                  onClick={() => removeItem(item.stock.code)}
                  className="ml-2 text-gray-400 hover:text-red-500 transition-colors"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 bg-white rounded-xl shadow-md border border-gray-100 p-6">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-bold text-gray-800">财务数据对比图表</h2>
              <div className="flex gap-2">
                <button
                  className={`flex items-center px-3 py-2 rounded-lg transition-colors ${
                    chartType === 'bar'
                      ? 'bg-blue-600 text-white'
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  }`}
                  onClick={() => setChartType('bar')}
                >
                  <BarChart3 className="w-4 h-4 mr-1" />
                  柱状图
                </button>
                <button
                  className={`flex items-center px-3 py-2 rounded-lg transition-colors ${
                    chartType === 'pie'
                      ? 'bg-blue-600 text-white'
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  }`}
                  onClick={() => setChartType('pie')}
                >
                  <PieChart className="w-4 h-4 mr-1" />
                  饼图
                </button>
                <button
                  className={`flex items-center px-3 py-2 rounded-lg transition-colors ${
                    chartType === 'line'
                      ? 'bg-blue-600 text-white'
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  }`}
                  onClick={() => setChartType('line')}
                >
                  <LineChart className="w-4 h-4 mr-1" />
                  趋势图
                </button>
              </div>
            </div>

            <div className="h-80">
              {chartType === 'bar' && (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={latestData} layout="vertical">
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis type="number" tickFormatter={(tick) => tick.toFixed(0) + '亿'} />
                    <YAxis type="category" dataKey="name" />
                    <Tooltip formatter={(value) => value.toFixed(2) + '亿'} />
                    <Legend />
                    <Bar dataKey="revenue" name="营业收入" fill="#3B82F6" radius={[0, 4, 4, 0]} />
                    <Bar dataKey="netProfit" name="净利润" fill="#10B981" radius={[0, 4, 4, 0]} />
                    <Bar dataKey="totalAssets" name="总资产" fill="#F59E0B" radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}

              {chartType === 'pie' && (
                <ResponsiveContainer width="100%" height="100%">
                  <RePieChart>
                    <Pie
                      data={pieData}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={({ name, percent }) => `${name}: ${(percent * 100).toFixed(0)}%`}
                      outerRadius={100}
                      dataKey="value"
                    >
                      {pieData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip formatter={(value) => value.toFixed(2) + '亿'} />
                    <Legend />
                  </RePieChart>
                </ResponsiveContainer>
              )}

              {chartType === 'line' && (
                <ResponsiveContainer width="100%" height="100%">
                  <ReLineChart data={lineChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis tickFormatter={(tick) => tick.toFixed(0) + '亿'} />
                    <Tooltip formatter={(value) => value.toFixed(2) + '亿'} />
                    <Legend />
                    {items.map((item, index) => (
                      <>
                        <Line
                          key={`${item.stock.code}-revenue`}
                          type="monotone"
                          dataKey={`${item.stock.name}_revenue`}
                          name={`${item.stock.name} - 营业收入`}
                          stroke={COLORS[index % COLORS.length]}
                          strokeWidth={2}
                          dot={{ r: 4 }}
                        />
                        <Line
                          key={`${item.stock.code}-netProfit`}
                          type="monotone"
                          dataKey={`${item.stock.name}_netProfit`}
                          name={`${item.stock.name} - 净利润`}
                          stroke={COLORS[index % COLORS.length]}
                          strokeWidth={2}
                          strokeDasharray="5 5"
                          dot={{ r: 4 }}
                        />
                      </>
                    ))}
                  </ReLineChart>
                </ResponsiveContainer>
              )}
            </div>
          </div>

          <div className="bg-white rounded-xl shadow-md border border-gray-100 p-6">
            <h2 className="text-lg font-bold text-gray-800 mb-4">关键指标对比</h2>
            <div className="space-y-4">
              <div className="border-b border-gray-100 pb-4">
                <h3 className="text-sm font-medium text-gray-500 mb-2">营业收入(亿)</h3>
                {items.map((item, index) => (
                  <div key={item.stock.code} className="flex items-center justify-between mb-2">
                    <div className="flex items-center">
                      <span
                        className="w-2 h-2 rounded-full mr-2"
                        style={{ backgroundColor: COLORS[index % COLORS.length] }}
                      />
                      <span className="text-sm text-gray-700">{item.stock.name}</span>
                    </div>
                    <span className="font-medium text-gray-800">
                      {(item.financial.incomeStatement.revenue / 10000).toFixed(2)}
                    </span>
                  </div>
                ))}
              </div>

              <div className="border-b border-gray-100 pb-4">
                <h3 className="text-sm font-medium text-gray-500 mb-2">净利润(亿)</h3>
                {items.map((item, index) => (
                  <div key={item.stock.code} className="flex items-center justify-between mb-2">
                    <div className="flex items-center">
                      <span
                        className="w-2 h-2 rounded-full mr-2"
                        style={{ backgroundColor: COLORS[index % COLORS.length] }}
                      />
                      <span className="text-sm text-gray-700">{item.stock.name}</span>
                    </div>
                    <span className="font-medium text-gray-800">
                      {(item.financial.incomeStatement.netProfit / 10000).toFixed(2)}
                    </span>
                  </div>
                ))}
              </div>

              <div className="border-b border-gray-100 pb-4">
                <h3 className="text-sm font-medium text-gray-500 mb-2">总资产(亿)</h3>
                {items.map((item, index) => (
                  <div key={item.stock.code} className="flex items-center justify-between mb-2">
                    <div className="flex items-center">
                      <span
                        className="w-2 h-2 rounded-full mr-2"
                        style={{ backgroundColor: COLORS[index % COLORS.length] }}
                      />
                      <span className="text-sm text-gray-700">{item.stock.name}</span>
                    </div>
                    <span className="font-medium text-gray-800">
                      {(item.financial.balanceSheet.totalAssets / 10000).toFixed(2)}
                    </span>
                  </div>
                ))}
              </div>

              <div>
                <h3 className="text-sm font-medium text-gray-500 mb-2">净利率(%)</h3>
                {items.map((item, index) => (
                  <div key={item.stock.code} className="flex items-center justify-between mb-2">
                    <div className="flex items-center">
                      <span
                        className="w-2 h-2 rounded-full mr-2"
                        style={{ backgroundColor: COLORS[index % COLORS.length] }}
                      />
                      <span className="text-sm text-gray-700">{item.stock.name}</span>
                    </div>
                    <span className="font-medium text-gray-800">
                      {item.financial.incomeStatement.netMargin.toFixed(2)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
