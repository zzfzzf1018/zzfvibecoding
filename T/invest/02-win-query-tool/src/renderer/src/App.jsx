import React, { useState } from 'react';
import { Layout, Menu, Typography } from 'antd';
import StockSearch from './components/StockSearch';
import FinanceReport from './components/FinanceReport';
import CompanyAnalysis from './components/CompanyAnalysis';
import Prospectus from './components/Prospectus';
import { SearchOutlined, FileTextOutlined, BarChartOutlined, ReadOutlined } from '@ant-design/icons';

const { Header, Content, Sider } = Layout;
const { Title } = Typography;

function App() {
    const [selectedStock, setSelectedStock] = useState(null);
    const [activeTab, setActiveTab] = useState('search');

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

    return (
        <Layout style={{ height: '100vh' }}>
            <Header style={{ backgroundColor: '#1890ff', display: 'flex', alignItems: 'center', padding: '0 24px' }}>
                <Title level={3} style={{ color: '#fff', margin: 0 }}>
                    📊 股票财务查询系统
                </Title>
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
