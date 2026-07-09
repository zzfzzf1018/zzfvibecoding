import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import 'antd/dist/reset.css';

class ErrorBoundary extends React.Component {
    constructor(props) {
        super(props);
        this.state = { hasError: false, error: null };
    }

    static getDerivedStateFromError(error) {
        return { hasError: true, error };
    }

    componentDidCatch(error, errorInfo) {
        console.error('ErrorBoundary caught:', error, errorInfo);
    }

    render() {
        if (this.state.hasError) {
            return <div style={{color: 'red', padding: '20px'}}>组件错误: {this.state.error?.message}</div>;
        }
        return this.props.children;
    }
}

console.log('React renderer starting...');
console.log('Root element:', document.getElementById('root'));

const rootElement = document.getElementById('root');

try {
    const root = ReactDOM.createRoot(rootElement);
    root.render(
        <React.StrictMode>
            <ErrorBoundary>
                <App />
            </ErrorBoundary>
        </React.StrictMode>
    );
    console.log('React render called');
    
    setTimeout(() => {
        console.log('After 1s - Root innerHTML length:', rootElement.innerHTML.length);
        console.log('First 500 chars:', rootElement.innerHTML.substring(0, 500));
    }, 1000);
} catch (error) {
    console.error('React render error:', error);
    rootElement.innerHTML = `<div style="color: red; padding: 20px;">React渲染错误: ${error.message}</div>`;
}
