import React, { useState, useEffect } from 'react';
import { Card, Spin, message, Tag, Row, Col, Statistic } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined, MinusOutlined } from '@ant-design/icons';
import axios from 'axios';

function CompanyAnalysis({ stock }) {
    const [loading, setLoading] = useState(false);
    const [analysis, setAnalysis] = useState(null);
    const [isCached, setIsCached] = useState(false);

    useEffect(() => {
        if (stock) {
            fetchAnalysis();
        }
    }, [stock]);

    const fetchAnalysis = async () => {
        if (!stock) return;
        
        setLoading(true);
        try {
            const response = await axios.get('http://localhost:5000/api/stock/analysis', {
                params: { symbol: stock.full_code }
            });
            
            if (response.data.success) {
                setAnalysis(response.data.data);
                setIsCached(response.data.cached || false);
            } else {
                message.error(response.data.error || '获取分析数据失败');
                setIsCached(false);
            }
        } catch (error) {
            message.error('获取分析数据失败');
            setIsCached(false);
        } finally {
            setLoading(false);
        }
    };

    const renderValuation = () => {
        if (!analysis || !analysis.valuation) return null;
        
        const { pe, pb, ps, dividend_yield } = analysis.valuation;
        
        return (
            <Card title="估值指标" style={{ marginBottom: 16 }}>
                <Row gutter={16}>
                    <Col span={6}>
                        <Statistic
                            title="市盈率 (PE)"
                            value={pe}
                            suffix="倍"
                            valueStyle={{ color: pe && pe > 30 ? '#cf1322' : '#3f8600' }}
                        />
                    </Col>
                    <Col span={6}>
                        <Statistic
                            title="市净率 (PB)"
                            value={pb}
                            suffix="倍"
                            valueStyle={{ color: pb && pb > 5 ? '#cf1322' : '#3f8600' }}
                        />
                    </Col>
                    <Col span={6}>
                        <Statistic
                            title="市销率 (PS)"
                            value={ps}
                            suffix="倍"
                            valueStyle={{ color: ps && ps > 3 ? '#cf1322' : '#3f8600' }}
                        />
                    </Col>
                    <Col span={6}>
                        <Statistic
                            title="股息率"
                            value={dividend_yield}
                            suffix="%"
                            valueStyle={{ color: dividend_yield && dividend_yield > 2 ? '#3f8600' : '#1890ff' }}
                        />
                    </Col>
                </Row>
            </Card>
        );
    };

    const renderFinancialRatios = () => {
        if (!analysis || !analysis.financial_ratios) return null;
        
        const ratios = analysis.financial_ratios;
        const ratioItems = Object.entries(ratios).map(([key, value], index) => (
            <div key={index} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                <span>{key}</span>
                <span style={{ fontWeight: 'bold' }}>{value}</span>
            </div>
        ));

        return (
            <Card title="财务比率分析" style={{ marginBottom: 16 }}>
                <div style={{ maxHeight: '400px', overflow: 'auto' }}>
                    {ratioItems}
                </div>
            </Card>
        );
    };

    const renderAnalysisSummary = () => {
        if (!analysis) return null;
        
        const { pe, pb } = analysis.valuation || {};
        let summary = [];
        
        if (pe) {
            if (pe < 10) summary.push('估值较低，具备投资价值');
            else if (pe > 30) summary.push('估值较高，需谨慎');
            else summary.push('估值处于合理区间');
        }
        
        if (pb) {
            if (pb < 1) summary.push('股价低于净资产');
            else if (pb > 5) summary.push('市净率较高');
        }

        return (
            <Card title="分析总结" bordered={false}>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                    {summary.map((item, index) => (
                        <Tag key={index} color="blue">{item}</Tag>
                    ))}
                    {summary.length === 0 && <span>暂无分析数据</span>}
                </div>
            </Card>
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
            <div style={{ marginBottom: 16 }}>
                <h2>公司分析</h2>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <p>当前股票：<Tag color="blue">{stock.name}</Tag> ({stock.full_code})</p>
                    {isCached && (
                        <Tag color="orange">缓存数据</Tag>
                    )}
                </div>
            </div>

            <Spin spinning={loading}>
                {renderValuation()}
                {renderFinancialRatios()}
                {renderAnalysisSummary()}
            </Spin>
        </div>
    );
}

export default CompanyAnalysis;
