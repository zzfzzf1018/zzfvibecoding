import React, { useState, useEffect } from 'react';
import { Input, Button, Table, Tag, Spin, message, Select } from 'antd';
import axios from 'axios';

const { Search } = Input;
const { Option } = Select;

function StockSearch({ onStockSelect }) {
    const [keyword, setKeyword] = useState('');
    const [market, setMarket] = useState('all');
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState([]);

    useEffect(() => {
        fetchPopularStocks();
    }, []);

    const fetchPopularStocks = async () => {
        try {
            setLoading(true);
            const response = await axios.get('http://localhost:5000/api/stock/search', {
                params: { keyword: '', market: 'all' }
            });
            if (response.data.success) {
                setData(response.data.data.slice(0, 15));
            }
        } catch (error) {
            message.error('获取股票列表失败');
        } finally {
            setLoading(false);
        }
    };

    const handleSearch = async () => {
        if (!keyword.trim()) {
            message.warning('请输入股票代码或名称');
            return;
        }

        try {
            setLoading(true);
            const response = await axios.get('http://localhost:5000/api/stock/search', {
                params: { keyword, market }
            });
            
            if (response.data.success) {
                setData(response.data.data);
                if (response.data.warning) {
                    message.warning(response.data.warning);
                }
            } else {
                message.error(response.data.error || '搜索失败');
            }
        } catch (error) {
            message.error('网络请求失败，请检查Python服务是否启动');
        } finally {
            setLoading(false);
        }
    };

    const handleSelectStock = (record) => {
        onStockSelect(record);
        message.success(`已选择 ${record.name} (${record.full_code})`);
    };

    const columns = [
        {
            title: '股票代码',
            dataIndex: 'full_code',
            key: 'full_code',
            width: 120,
        },
        {
            title: '股票名称',
            dataIndex: 'name',
            key: 'name',
            width: 120,
        },
        {
            title: '市场',
            dataIndex: 'market',
            key: 'market',
            width: 80,
            render: (market) => (
                <Tag color={market === 'cn' ? 'green' : 'blue'}>
                    {market === 'cn' ? 'A股' : '港股'}
                </Tag>
            ),
        },
        {
            title: '操作',
            key: 'action',
            render: (_, record) => (
                <Button type="primary" size="small" onClick={() => handleSelectStock(record)}>
                    选择
                </Button>
            ),
        },
    ];

    return (
        <div>
            <div style={{ marginBottom: 24 }}>
                <h2 style={{ marginBottom: 16 }}>股票搜索</h2>
                <div style={{ display: 'flex', gap: 12 }}>
                    <Select
                        value={market}
                        onChange={setMarket}
                        style={{ width: 120 }}
                        placeholder="选择市场"
                    >
                        <Option value="all">全部</Option>
                        <Option value="cn">A股</Option>
                        <Option value="hk">港股</Option>
                    </Select>
                    <Search
                        placeholder="输入股票代码或名称"
                        allowClear
                        enterButton="搜索"
                        size="large"
                        value={keyword}
                        onChange={(e) => setKeyword(e.target.value)}
                        onSearch={handleSearch}
                        style={{ width: 400 }}
                    />
                </div>
                <p style={{ marginTop: 8, color: '#999', fontSize: 12 }}>
                    示例：贵州茅台、600519、腾讯控股、00700
                </p>
            </div>

            <Spin spinning={loading}>
                <Table
                    columns={columns}
                    dataSource={data}
                    rowKey="full_code"
                    pagination={{ pageSize: 10 }}
                    title={() => '股票列表'}
                />
            </Spin>
        </div>
    );
}

export default StockSearch;
