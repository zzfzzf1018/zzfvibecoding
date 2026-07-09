import React, { useState, useEffect } from 'react';
import { Layout, Menu, Typography, Select, Tag, message } from 'antd';
import StockSearch from './components/StockSearch';
import FinanceReport from './components/FinanceReport';
import CompanyAnalysis from './components/CompanyAnalysis';
import Prospectus from './components/Prospectus';
import { SearchOutlined, FileTextOutlined, BarChartOutlined, ReadOutlined, DatabaseOutlined } from '@ant-design/icons';
import axios from 'axios';

const { Header, Content, Sider } = Layout;
const { Title } = Typography;
const { Option } = Select;

function App() {
    const [selectedStock, setSelectedStock] = useState(null);
    const [activeTab, setActiveTab] = useState('search');
    const [dataSources, setDataSources] = useState([]);
    const [currentSource, setCurrentSource] = useState('eastmoney');

    useEffect(() => {
        fetchDataSources();
    }, []);

    const fetchDataSources = async () => {
        try {
            const response = await axios.get('http://localhost:5000/api/data_source/list');
            if (response.data.success) {
                setDataSources(response.data.data);
                setCurrentSource(response.data.current);
            }
        } catch (error) {
            console.error('获取数据源列表失败:', error);
        }
    };

    const handleDataSourceChange = async (value) => {
        try {
            const response = await axios.post('http://localhost:5000/api/data_source/switch', {
                source_id: value
            });
            if (response.data.success) {
                setCurrentSource(value);
                message.success(response.data.message);
            } else {
                message.error(response.data.error);
            }
        } catch (error) {
            message.error('切换数据源失败');
        }
    };

    const handleStockSelect = (stock) => {
        setSelectedStock(stock);
    };

    const menuItems = [
        { key: 'search', icon: <SearchOutlined />, label: '股票搜索' },
        { key: 'finance', icon: <FileTextOutlined />, label: '财务报表' },
        { key: 'analysis', icon: <BarChartOutlined />, label: '公司分析' },
        { key: 'prospectus', icon: <ReadOutlined />, label: '招股书' },
    ];

    const renderContent = () => {
        switch (activeTab) {
            case 'search':
                return <StockSearch onStockSelect={handleStockSelect} />;
            case 'finance':
                return <FinanceReport stock={selectedStock} />;
            case 'analysis':
                return <CompanyAnalysis stock={selectedStock} />;
            case 'prospectus':
                return <Prospectus stock={selectedStock} />;
            default:
                return <StockSearch onStockSelect={handleStockSelect} />;
        }
    };

    const getSourceName = (sourceId) => {
        const source = dataSources.find(s => s.id === sourceId);
        return source ? source.name : sourceId;
    };

    return (
        <Layout style={{ height: '100vh' }}>
            <Header style={{ backgroundColor: '#1890ff', display: 'flex', alignItems: 'center', padding: '0 24px', justifyContent: 'space-between' }}>
                <Title level={3} style={{ color: '#fff', margin: 0 }}>
                    📊 股票财务查询系统
                </Title>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <span style={{ color: '#fff', fontSize: 14 }}>数据源:</span>
                    <Select
                        value={currentSource}
                        onChange={handleDataSourceChange}
                        style={{ width: 160 }}
                        suffixIcon={<DatabaseOutlined />}
                    >
                        {dataSources.map(source => (
                            <Option key={source.id} value={source.id}>
                                {source.name}
                            </Option>
                        ))}
                    </Select>
                    <Tag color="blue">
                        当前: {getSourceName(currentSource)}
                    </Tag>
                </div>
            </Header>
            <Layout>
                <Sider width={200} theme="light">
                    <Menu
                        mode="inline"
                        selectedKeys={[activeTab]}
                        items={menuItems}
                        onClick={({ key }) => setActiveTab(key)}
                        style={{ height: '100%', borderRight: 0 }}
                    />
                </Sider>
                <Content style={{ padding: '24px', backgroundColor: '#f5f5f5', overflow: 'auto' }}>
                    {renderContent()}
                </Content>
            </Layout>
        </Layout>
    );
}

export default App;
