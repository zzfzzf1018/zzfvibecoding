import React, { useState, useEffect } from 'react';
import { Button, Table, Spin, message, Tabs, Tag, DownloadOutlined } from 'antd';
import axios from 'axios';

const { TabPane } = Tabs;

function FinanceReport({ stock }) {
    const [loading, setLoading] = useState(false);
    const [reports, setReports] = useState({
        balance: { columns: [], data: [], cached: false },
        income: { columns: [], data: [], cached: false },
        cash: { columns: [], data: [], cached: false }
    });

    useEffect(() => {
        if (stock) {
            fetchAllReports();
        }
    }, [stock]);

    const fetchReport = async (reportType) => {
        try {
            const response = await axios.get('http://localhost:5000/api/stock/finance_report', {
                params: { symbol: stock.full_code, type: reportType }
            });
            
            if (response.data.success) {
                return {
                    columns: response.data.columns,
                    data: response.data.data,
                    cached: response.data.cached || false
                };
            } else {
                message.warning(`${getReportName(reportType)}获取失败: ${response.data.error}`);
                return { columns: [], data: [], cached: false };
            }
        } catch (error) {
            message.error(`${getReportName(reportType)}获取失败`);
            return { columns: [], data: [], cached: false };
        }
    };

    const fetchAllReports = async () => {
        if (!stock) return;
        
        setLoading(true);
        try {
            const [balance, income, cash] = await Promise.all([
                fetchReport('balance'),
                fetchReport('income'),
                fetchReport('cash')
            ]);
            
            setReports({ balance, income, cash });
        } catch (error) {
            message.error('获取财务报表失败');
        } finally {
            setLoading(false);
        }
    };

    const getReportName = (type) => {
        const names = { balance: '资产负债表', income: '利润表', cash: '现金流量表' };
        return names[type] || type;
    };

    const handleDownload = async (reportType) => {
        if (!stock) {
            message.warning('请先选择股票');
            return;
        }

        try {
            const response = await axios.get('http://localhost:5000/api/stock/download_report', {
                params: { symbol: stock.full_code, type: reportType }
            });
            
            if (response.data.success) {
                message.success(`${getReportName(reportType)}下载成功`);
                
                const { ipcRenderer } = window.require('electron');
                ipcRenderer.invoke('open-file-dialog', {
                    properties: ['openDirectory']
                }).then((result) => {
                    if (!result.canceled && result.filePaths.length > 0) {
                        const destPath = `${result.filePaths[0]}/${response.data.filename}`;
                        ipcRenderer.invoke('download-file', response.data.filepath, destPath);
                        message.success(`文件已保存到: ${destPath}`);
                    }
                });
            } else {
                message.error(response.data.error || '下载失败');
            }
        } catch (error) {
            message.error('下载失败');
        }
    };

    const renderTable = (reportType) => {
        const report = reports[reportType];
        
        if (report.columns.length === 0) {
            return (
                <div style={{ textAlign: 'center', padding: '40px' }}>
                    <p>暂无数据</p>
                </div>
            );
        }

        const columns = report.columns.map((col, index) => ({
            title: col,
            dataIndex: index,
            key: index,
            width: index === 0 ? 180 : 120,
            ellipsis: true,
            render: (value) => {
                if (typeof value === 'number') {
                    return value.toLocaleString();
                }
                return value;
            }
        }));

        const dataSource = report.data.map((row, rowIndex) => ({
            key: rowIndex,
            ...row
        }));

        return (
            <div>
                {report.cached && (
                    <div style={{ marginBottom: 12 }}>
                        <Tag color="orange">缓存数据</Tag>
                        <span style={{ marginLeft: 8, color: '#999', fontSize: 12 }}>当前为缓存数据，可能不是最新</span>
                    </div>
                )}
                <Table
                    columns={columns}
                    dataSource={dataSource}
                    pagination={{ pageSize: 20 }}
                    scroll={{ x: 'max-content' }}
                />
            </div>
        );
    };

    if (!stock) {
        return (
            <div style={{ textAlign: 'center', padding: '100px' }}>
                <p style={{ fontSize: 18, color: '#999' }}>请先在左侧菜单选择「股票搜索」并选择一只股票</p>
            </div>
        );
    }

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <div>
                    <h2>财务报表</h2>
                    <p>当前股票：<Tag color="blue">{stock.name}</Tag> ({stock.full_code})</p>
                </div>
            </div>

            <Spin spinning={loading}>
                <Tabs type="card">
                    <TabPane tab={`资产负债表 <Button icon={<DownloadOutlined />} size="small" onClick={() => handleDownload('balance')} />`} key="balance">
                        {renderTable('balance')}
                    </TabPane>
                    <TabPane tab={`利润表 <Button icon={<DownloadOutlined />} size="small" onClick={() => handleDownload('income')} />`} key="income">
                        {renderTable('income')}
                    </TabPane>
                    <TabPane tab={`现金流量表 <Button icon={<DownloadOutlined />} size="small" onClick={() => handleDownload('cash')} />`} key="cash">
                        {renderTable('cash')}
                    </TabPane>
                </Tabs>
            </Spin>
        </div>
    );
}

export default FinanceReport;
