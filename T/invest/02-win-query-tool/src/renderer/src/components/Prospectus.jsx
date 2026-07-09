import React, { useState, useEffect } from 'react';
import { Button, Table, Spin, message, Tag, Input } from 'antd';
import { SearchOutlined, DownloadOutlined, LinkOutlined } from '@ant-design/icons';
import axios from 'axios';

function Prospectus({ stock }) {
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState([]);
    const [keyword, setKeyword] = useState('');
    const [isCached, setIsCached] = useState(false);

    useEffect(() => {
        if (stock) {
            fetchProspectus();
        }
    }, [stock]);

    const fetchProspectus = async () => {
        if (!stock) return;
        
        setLoading(true);
        try {
            const response = await axios.get('http://localhost:5000/api/stock/prospectus', {
                params: { symbol: stock.full_code }
            });
            
            if (response.data.success) {
                setData(response.data.data);
                setIsCached(response.data.cached || false);
            } else {
                message.warning(response.data.error || '获取招股书信息失败');
                setIsCached(false);
            }
        } catch (error) {
            message.error('获取招股书信息失败');
            setIsCached(false);
        } finally {
            setLoading(false);
        }
    };

    const handleDownload = async (url, name) => {
        if (!url) {
            message.warning('没有可用的下载链接');
            return;
        }

        try {
            const response = await axios.get('http://localhost:5000/api/stock/download_prospectus', {
                params: { url, filename: `${name}_招股书.pdf` }
            });
            
            if (response.data.success) {
                const { ipcRenderer } = window.require('electron');
                ipcRenderer.invoke('open-file-dialog', {
                    properties: ['openDirectory']
                }).then((result) => {
                    if (!result.canceled && result.filePaths.length > 0) {
                        const destPath = `${result.filePaths[0]}/${response.data.filename}`;
                        ipcRenderer.invoke('download-file', response.data.filepath, destPath);
                        message.success(`招股书已保存到: ${destPath}`);
                    }
                });
            } else {
                message.error(response.data.error || '下载失败');
            }
        } catch (error) {
            message.error('下载失败');
        }
    };

    const handleOpenUrl = (url) => {
        if (!url) {
            message.warning('没有可用的链接');
            return;
        }
        
        const { ipcRenderer } = window.require('electron');
        ipcRenderer.invoke('open-external', url);
    };

    const handleSearch = async () => {
        if (!keyword.trim()) {
            message.warning('请输入股票代码或名称');
            return;
        }

        try {
            setLoading(true);
            const response = await axios.get('http://localhost:5000/api/stock/prospectus', {
                params: { symbol: keyword }
            });
            
            if (response.data.success) {
                setData(response.data.data);
                setIsCached(response.data.cached || false);
            } else {
                message.error(response.data.error || '搜索失败');
                setIsCached(false);
            }
        } catch (error) {
            message.error('网络请求失败');
            setIsCached(false);
        } finally {
            setLoading(false);
        }
    };

    const columns = [
        {
            title: '股票代码',
            dataIndex: 'code',
            key: 'code',
            width: 100,
        },
        {
            title: '股票名称',
            dataIndex: 'name',
            key: 'name',
            width: 120,
        },
        {
            title: '招股日期',
            dataIndex: 'ipo_date',
            key: 'ipo_date',
            width: 120,
        },
        {
            title: '操作',
            key: 'action',
            render: (_, record) => (
                <div style={{ display: 'flex', gap: 8 }}>
                    {record.prospectus_url && (
                        <>
                            <Button size="small" onClick={() => handleOpenUrl(record.prospectus_url)}>
                                <LinkOutlined />
                            </Button>
                            <Button size="small" icon={<DownloadOutlined />} onClick={() => handleDownload(record.prospectus_url, record.name)}>
                                下载
                            </Button>
                        </>
                    )}
                    {!record.prospectus_url && (
                        <span style={{ color: '#999', fontSize: 12 }}>暂无链接</span>
                    )}
                </div>
            ),
        },
    ];

    return (
        <div>
            <div style={{ marginBottom: 24 }}>
                <h2>招股书下载</h2>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    {stock && (
                        <p>当前股票：<Tag color="blue">{stock.name}</Tag> ({stock.full_code})</p>
                    )}
                    {isCached && (
                        <Tag color="orange">缓存数据</Tag>
                    )}
                </div>
                <div style={{ display: 'flex', gap: 12, marginTop: 16 }}>
                    <Input.Search
                        placeholder="搜索股票招股书"
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
                    支持搜索A股和港股的招股书信息
                </p>
            </div>

            <Spin spinning={loading}>
                <Table
                    columns={columns}
                    dataSource={data}
                    rowKey="code"
                    pagination={{ pageSize: 10 }}
                    title={() => '招股书列表'}
                />
            </Spin>
        </div>
    );
}

export default Prospectus;
