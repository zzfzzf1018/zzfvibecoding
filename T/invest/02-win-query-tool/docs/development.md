# 开发指南

## 开发环境配置

### 1. 安装Node.js

- **版本要求**: >= 18.x
- **下载地址**: https://nodejs.org/
- **验证**: 
  ```bash
  node --version
  npm --version
  ```

### 2. 安装Python

- **版本要求**: >= 3.10
- **下载地址**: https://www.python.org/
- **验证**: 
  ```bash
  python --version
  ```

### 3. 安装依赖

#### Python依赖

```bash
pip install -r src/python/requirements.txt
```

#### 前端依赖

```bash
cd src/renderer
npm install
```

### 4. 开发工具

- **IDE**: VS Code（推荐）
- **Python扩展**: Python, Pylance
- **前端扩展**: ESLint, Prettier, React Developer Tools

## 开发流程

### 1. 启动开发服务器

#### Python数据服务

```bash
python src/python/app.py
```

服务启动后访问: http://localhost:5000

#### 前端开发模式

```bash
cd src/renderer
npm start
```

#### Electron应用

```bash
npm start
```

### 2. 代码规范

#### JavaScript/React

- 使用ES6+语法
- 使用函数组件和Hooks
- 代码格式化使用Prettier
- 代码检查使用ESLint

#### Python

- 使用PEP 8规范
- 使用4空格缩进
- 文件名使用小写和下划线
- 函数和变量使用小写和下划线

#### 通用规范

- 代码注释使用中文
- 变量命名清晰易懂
- 避免魔法数字
- 错误处理完善

### 3. 调试技巧

#### Python服务调试

- 使用print语句或logging模块
- 在VS Code中设置断点
- 访问http://localhost:5000/api/health检查服务状态

#### 前端调试

- 使用浏览器开发者工具（Ctrl+Shift+I）
- 使用React Developer Tools扩展
- 使用console.log输出调试信息

#### Electron调试

- 打开开发者工具: Ctrl+Shift+I
- 主进程日志: 终端输出
- 使用electron-log记录日志

### 4. 构建和测试

#### 前端构建

```bash
cd src/renderer
npm run build
```

#### 运行测试

```bash
cd src/renderer
npm test
```

#### 打包应用

```bash
npm run build
```

## 代码结构说明

### 目录结构

```
src/
├── main/
│   └── index.js          # Electron主进程
├── python/
│   ├── app.py            # Flask API服务
│   ├── cache.py          # 缓存模块
│   └── requirements.txt  # Python依赖
└── renderer/
    ├── src/
    │   ├── App.jsx       # 主应用组件
    │   ├── index.jsx     # React入口
    │   └── components/   # 组件目录
    ├── package.json      # 前端依赖
    ├── webpack.config.js # Webpack配置
    └── .babelrc          # Babel配置
```

### 文件职责

| 文件 | 职责 | 说明 |
|------|------|------|
| main/index.js | Electron主进程 | 窗口管理、Python进程管理、IPC通信 |
| python/app.py | Flask服务 | API路由、数据获取、业务逻辑 |
| python/cache.py | 缓存模块 | SQLite操作、缓存管理 |
| renderer/src/App.jsx | 主应用 | 布局、路由、状态管理 |
| renderer/src/index.jsx | React入口 | 渲染应用到DOM |

## 添加新功能

### 1. 添加新API接口

在`python/app.py`中添加新路由：

```python
@app.route('/api/new_feature', methods=['GET'])
def new_feature():
    # 参数获取
    param = request.args.get('param', '')
    
    # 缓存逻辑
    cache_params = {'param': param}
    cached_data = get_cache('new_feature', cache_params)
    
    try:
        # 调用akshare获取数据
        data = akshare.some_function(param)
        
        # 数据处理
        result = process_data(data)
        
        # 更新缓存
        set_cache('new_feature', cache_params, result)
        
        return jsonify({'success': True, 'data': result, 'cached': False})
    except Exception as e:
        if cached_data:
            return jsonify({'success': True, 'data': cached_data, 'cached': True, 'warning': '网络异常，显示缓存数据'})
        return jsonify({'success': False, 'error': f'网络请求失败: {str(e)}'})
```

### 2. 添加新前端组件

在`renderer/src/components/`中创建新组件：

```jsx
import { useState, useEffect } from 'react';
import { Card, Spin, message } from 'antd';
import axios from 'axios';

function NewFeature({ stock }) {
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState(null);
    const [isCached, setIsCached] = useState(false);

    useEffect(() => {
        if (stock) {
            fetchData();
        }
    }, [stock]);

    const fetchData = async () => {
        setLoading(true);
        try {
            const response = await axios.get('http://localhost:5000/api/new_feature', {
                params: { symbol: stock.full_code }
            });
            
            if (response.data.success) {
                setData(response.data.data);
                setIsCached(response.data.cached || false);
            } else {
                message.error(response.data.error || '获取数据失败');
            }
        } catch (error) {
            message.error('获取数据失败');
        } finally {
            setLoading(false);
        }
    };

    return (
        <Card title="新功能">
            <Spin spinning={loading}>
                {/* 渲染数据 */}
            </Spin>
        </Card>
    );
}

export default NewFeature;
```

### 3. 在主应用中注册组件

在`renderer/src/App.jsx`中注册：

```jsx
import NewFeature from './components/NewFeature';

// 在menuItems中添加菜单项
{ key: 'new_feature', icon: <NewFeatureOutlined />, label: '新功能' },

// 在content渲染中添加
case 'new_feature':
    return <NewFeature stock={selectedStock} />;
```

## 常见问题

### 1. Python服务启动失败

**原因**: Python环境缺少依赖或路径配置错误

**解决**:
```bash
# 检查Python版本
python --version

# 重新安装依赖
pip install -r src/python/requirements.txt

# 检查akshare版本
python -c "import akshare; print(akshare.__version__)"
```

### 2. 前端构建失败

**原因**: 依赖缺失或Webpack配置错误

**解决**:
```bash
cd src/renderer
npm install
npm run build
```

### 3. 网络请求失败

**原因**: 网络连接问题或akshare数据源变更

**解决**:
- 检查网络连接
- 更新akshare版本
- 查看错误日志定位问题

### 4. 缓存不生效

**原因**: 缓存键生成不一致或数据库权限问题

**解决**:
```bash
# 检查缓存数据库路径
ls src/python/cache/

# 手动清除缓存
curl -X POST http://localhost:5000/api/cache/clear

# 检查缓存统计
curl http://localhost:5000/api/cache/stats
```

### 5. Electron沙箱问题

**原因**: 用户数据目录权限不足

**解决**:
- 主进程已设置`app.setPath('userData', ...)`指向项目目录
- 确保项目目录有读写权限

## 调试清单

### 启动前检查

- [ ] Node.js版本 >= 18
- [ ] Python版本 >= 3.10
- [ ] Python依赖已安装
- [ ] 前端依赖已安装
- [ ] 前端已构建

### 服务检查

- [ ] Python服务运行在5000端口
- [ ] 健康检查接口正常
- [ ] 前端能正常访问
- [ ] Electron窗口能正常显示

### 功能检查

- [ ] 股票搜索功能正常
- [ ] 财务报表功能正常
- [ ] 公司分析功能正常
- [ ] 招股书下载功能正常
- [ ] 缓存功能正常

## 代码审查要点

### Python代码审查

- [ ] 输入参数验证
- [ ] 异常处理完善
- [ ] 缓存逻辑正确
- [ ] 代码格式符合PEP 8
- [ ] 没有魔法数字

### JavaScript代码审查

- [ ] 状态管理正确
- [ ] 错误处理完善
- [ ] API调用正确
- [ ] 代码格式符合ESLint规则
- [ ] 没有内存泄漏

### 通用审查

- [ ] 代码注释清晰
- [ ] 变量命名规范
- [ ] 性能优化考虑
- [ ] 安全性考虑

## 版本管理

### 分支策略

- `main`: 主分支，稳定版本
- `develop`: 开发分支，日常开发
- `feature/*`: 功能分支，开发新功能

### 提交规范

```
类型(模块): 描述

详细说明
```

类型:
- `feat`: 新功能
- `fix`: 修复bug
- `docs`: 文档更新
- `refactor`: 代码重构
- `test`: 测试更新
- `chore`: 构建或工具更新

示例:
```
feat(api): 添加股票搜索接口

- 实现A股和港股搜索功能
- 集成缓存机制
- 添加市场筛选参数
```

## 性能优化

### 前端优化

- 使用React.memo优化组件渲染
- 使用useCallback和useMemo缓存函数和计算结果
- 图片懒加载
- 虚拟列表（大数据量）

### 后端优化

- 使用连接池（数据库）
- 缓存热点数据
- 异步处理（大文件下载）
- 请求限流

### 通用优化

- 减少不必要的网络请求
- 压缩传输数据
- 合理的缓存策略

## 安全最佳实践

- 输入验证和过滤
- 参数化查询（防止SQL注入）
- 错误信息脱敏
- 文件路径安全检查
- HTTPS加密传输（生产环境）
