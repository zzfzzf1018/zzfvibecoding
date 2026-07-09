import React, { useState } from 'react';
import * as XLSX from 'xlsx';
import jsPDF from 'jspdf';
import { FileSpreadsheet, FileText, Download, Check } from 'lucide-react';
import { FinancialData, Stock } from '../../types';
import { formatNumber } from '../../utils/ratios';

interface ExportPanelProps {
  financial: FinancialData;
  stock: Stock;
}

const availableFields = [
  { key: 'totalAssets', label: '总资产', section: 'balance' },
  { key: 'totalLiabilities', label: '总负债', section: 'balance' },
  { key: 'totalEquity', label: '净资产', section: 'balance' },
  { key: 'currentAssets', label: '流动资产', section: 'balance' },
  { key: 'currentLiabilities', label: '流动负债', section: 'balance' },
  { key: 'revenue', label: '营业收入', section: 'income' },
  { key: 'grossProfit', label: '毛利润', section: 'income' },
  { key: 'netProfit', label: '净利润', section: 'income' },
  { key: 'operatingProfit', label: '营业利润', section: 'income' },
  { key: 'eps', label: '每股收益', section: 'income' },
  { key: 'operatingCashFlow', label: '经营现金流', section: 'cash' },
  { key: 'investingCashFlow', label: '投资现金流', section: 'cash' },
  { key: 'financingCashFlow', label: '筹资现金流', section: 'cash' },
];

export const ExportPanel: React.FC<ExportPanelProps> = ({ financial, stock }) => {
  const [selectedFields, setSelectedFields] = useState<string[]>(availableFields.map(f => f.key));
  const [exportFormat, setExportFormat] = useState<'excel' | 'pdf'>('excel');
  const [showSuccess, setShowSuccess] = useState(false);

  const toggleField = (key: string) => {
    setSelectedFields(prev =>
      prev.includes(key) ? prev.filter(f => f !== key) : [...prev, key]
    );
  };

  const selectAll = () => {
    setSelectedFields(availableFields.map(f => f.key));
  };

  const clearAll = () => {
    setSelectedFields([]);
  };

  const exportToExcel = () => {
    const exportData: Record<string, unknown>[] = [];
    
    selectedFields.forEach(fieldKey => {
      const field = availableFields.find(f => f.key === fieldKey);
      if (!field) return;

      let value: unknown;
      if (field.section === 'balance') {
        value = financial.balanceSheet[field.key as keyof typeof financial.balanceSheet];
      } else if (field.section === 'income') {
        value = financial.incomeStatement[field.key as keyof typeof financial.incomeStatement];
      } else {
        value = financial.cashFlow[field.key as keyof typeof financial.cashFlow];
      }

      exportData.push({
        指标: field.label,
        数值: typeof value === 'number' ? formatNumber(value) : value,
      });
    });

    const worksheet = XLSX.utils.json_to_sheet(exportData);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, '财务数据');

    XLSX.writeFile(workbook, `${stock.name}_财务报表.xlsx`);
    showSuccessMessage();
  };

  const exportToPDF = () => {
    const doc = new jsPDF();
    doc.setFontSize(16);
    doc.text(`${stock.name} (${stock.code}) 财务报表`, 20, 20);
    
    doc.setFontSize(12);
    doc.text(`报告日期: ${financial.reportDate}`, 20, 35);
    
    let y = 50;
    selectedFields.forEach((fieldKey, index) => {
      const field = availableFields.find(f => f.key === fieldKey);
      if (!field) return;

      let value: unknown;
      if (field.section === 'balance') {
        value = financial.balanceSheet[field.key as keyof typeof financial.balanceSheet];
      } else if (field.section === 'income') {
        value = financial.incomeStatement[field.key as keyof typeof financial.incomeStatement];
      } else {
        value = financial.cashFlow[field.key as keyof typeof financial.cashFlow];
      }

      const label = field.label;
      const displayValue = typeof value === 'number' ? formatNumber(value) : String(value);

      doc.setFontSize(10);
      doc.text(`${index + 1}. ${label}:`, 20, y);
      doc.text(displayValue, 100, y);
      y += 15;
    });

    doc.save(`${stock.name}_财务报表.pdf`);
    showSuccessMessage();
  };

  const showSuccessMessage = () => {
    setShowSuccess(true);
    setTimeout(() => setShowSuccess(false), 2000);
  };

  const handleExport = () => {
    if (selectedFields.length === 0) return;
    if (exportFormat === 'excel') {
      exportToExcel();
    } else {
      exportToPDF();
    }
  };

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-6">数据导出</h3>
      
      <div className="mb-4">
        <label className="text-sm font-medium text-gray-600 mb-2 block">导出格式</label>
        <div className="flex gap-4">
          <button
            onClick={() => setExportFormat('excel')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg border transition-colors ${
              exportFormat === 'excel'
                ? 'border-blue-500 bg-blue-50 text-blue-600'
                : 'border-gray-200 hover:border-gray-300'
            }`}
          >
            <FileSpreadsheet size={18} />
            Excel
          </button>
          <button
            onClick={() => setExportFormat('pdf')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg border transition-colors ${
              exportFormat === 'pdf'
                ? 'border-blue-500 bg-blue-50 text-blue-600'
                : 'border-gray-200 hover:border-gray-300'
            }`}
          >
            <FileText size={18} />
            PDF
          </button>
        </div>
      </div>

      <div className="mb-6">
        <div className="flex items-center justify-between mb-2">
          <label className="text-sm font-medium text-gray-600">自定义导出字段</label>
          <div className="flex gap-2 text-xs">
            <button onClick={selectAll} className="text-blue-600 hover:text-blue-700">全选</button>
            <button onClick={clearAll} className="text-gray-500 hover:text-gray-600">清空</button>
          </div>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-2 max-h-60 overflow-y-auto">
          {availableFields.map(field => (
            <label
              key={field.key}
              className={`flex items-center gap-2 p-2 rounded-lg cursor-pointer transition-colors ${
                selectedFields.includes(field.key)
                  ? 'bg-blue-50 border border-blue-200'
                  : 'bg-gray-50 border border-gray-200 hover:bg-gray-100'
              }`}
            >
              <input
                type="checkbox"
                checked={selectedFields.includes(field.key)}
                onChange={() => toggleField(field.key)}
                className="w-4 h-4 text-blue-500 rounded"
              />
              <span className="text-sm text-gray-700">{field.label}</span>
            </label>
          ))}
        </div>
      </div>

      <button
        onClick={handleExport}
        disabled={selectedFields.length === 0}
        className={`w-full flex items-center justify-center gap-2 py-3 rounded-lg font-medium transition-colors ${
          selectedFields.length > 0
            ? 'bg-blue-500 text-white hover:bg-blue-600'
            : 'bg-gray-200 text-gray-400 cursor-not-allowed'
        }`}
      >
        <Download size={18} />
        导出报表
      </button>

      {showSuccess && (
        <div className="mt-4 flex items-center justify-center gap-2 text-green-600">
          <Check size={20} />
          <span className="text-sm">导出成功!</span>
        </div>
      )}
    </div>
  );
};

export default ExportPanel;
