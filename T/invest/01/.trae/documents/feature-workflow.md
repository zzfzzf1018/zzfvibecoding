# 新功能开发工作流

## 一、开发流程总览

```
需求分析 → 技术方案设计 → 类型定义 → 数据层实现 → 状态管理 → 组件开发 → 页面集成 → 测试验证 → 代码审查
```

## 二、详细步骤

### 步骤1：需求分析

**目标**：明确功能需求和边界

**操作**：
1. 阅读PRD文档，理解功能需求
2. 分析功能涉及的数据模型
3. 确定功能的UI交互设计

**输出**：
- 功能需求清单
- 涉及的数据模型列表
- UI设计草图

### 步骤2：技术方案设计

**目标**：设计技术实现方案

**操作**：
1. 确定组件结构（新增/修改）
2. 设计状态管理方案
3. 规划API接口（如果需要）
4. 选择合适的库和工具

**输出**：
- 组件结构设计图
- 状态管理方案
- API接口设计

### 步骤3：类型定义

**目标**：定义数据类型

**操作**：
1. 在 `src/types/index.ts` 中添加新接口
2. 更新现有接口（如需要）
3. 导出新类型

**示例**：
```typescript
// src/types/index.ts
export interface NewFeatureData {
  id: string;
  name: string;
  value: number;
  createdAt: string;
}
```

### 步骤4：数据层实现

**目标**：实现数据获取和处理逻辑

**操作**：
1. 在 `src/data/mockData.ts` 中添加Mock数据（开发阶段）
2. 创建工具函数处理数据
3. 更新数据查询函数

**示例**：
```typescript
// src/data/mockData.ts
export const newFeatureData: NewFeatureData[] = [
  { id: '1', name: '示例1', value: 100, createdAt: '2024-01-01' },
  { id: '2', name: '示例2', value: 200, createdAt: '2024-01-02' },
];

export const getNewFeatureData = (): NewFeatureData[] => {
  return newFeatureData;
};
```

### 步骤5：状态管理

**目标**：管理功能相关状态

**操作**：
1. 创建新的Zustand store（如果需要）
2. 定义状态和actions
3. 添加localStorage持久化（如果需要）

**示例**：
```typescript
// src/store/useNewFeatureStore.ts
import { create } from 'zustand';

interface NewFeatureStore {
  items: NewFeatureData[];
  selectedItem: NewFeatureData | null;
  setItems: (items: NewFeatureData[]) => void;
  selectItem: (item: NewFeatureData) => void;
}

export const useNewFeatureStore = create<NewFeatureStore>((set) => ({
  items: [],
  selectedItem: null,
  setItems: (items) => set({ items }),
  selectItem: (item) => set({ selectedItem: item }),
}));
```

### 步骤6：组件开发

**目标**：实现功能组件

**操作**：
1. 在 `src/components/` 目录下创建组件文件夹
2. 创建组件文件，遵循编码规范
3. 实现UI和交互逻辑
4. 添加错误处理和空状态

**示例**：
```typescript
// src/components/NewFeature/NewFeature.tsx
import React from 'react';
import { NewFeatureData } from '../../types';

interface NewFeatureProps {
  data: NewFeatureData[];
  onSelect?: (item: NewFeatureData) => void;
}

export const NewFeature: React.FC<NewFeatureProps> = ({ data, onSelect }) => {
  if (data.length === 0) {
    return (
      <div className="text-center py-8 text-gray-500">
        暂无数据
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">新功能标题</h3>
      <div className="space-y-3">
        {data.map((item) => (
          <div
            key={item.id}
            className="flex justify-between items-center p-3 bg-gray-50 rounded-lg cursor-pointer hover:bg-gray-100"
            onClick={() => onSelect?.(item)}
          >
            <span className="text-gray-800">{item.name}</span>
            <span className="text-blue-600 font-medium">{item.value}</span>
          </div>
        ))}
      </div>
    </div>
  );
};

export default NewFeature;
```

### 步骤7：页面集成

**目标**：将组件集成到页面中

**操作**：
1. 在页面组件中导入新组件
2. 获取数据并传递给组件
3. 实现页面级交互逻辑

**示例**：
```typescript
// src/pages/HomePage.tsx
import { getNewFeatureData } from '../data/mockData';
import NewFeature from '../components/NewFeature/NewFeature';

export default function HomePage() {
  const newFeatureData = getNewFeatureData();

  const handleSelectItem = (item: NewFeatureData) => {
    console.log('Selected:', item);
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <NewFeature data={newFeatureData} onSelect={handleSelectItem} />
    </div>
  );
}
```

### 步骤8：测试验证

**目标**：确保功能正确性

**操作**：
1. 编写单元测试
2. 运行测试验证
3. 手动测试功能

**示例**：
```typescript
// src/data/mockData.test.ts
describe('getNewFeatureData', () => {
  it('should return new feature data', () => {
    const data = getNewFeatureData();
    expect(data.length).toBeGreaterThan(0);
    expect(data[0]).toHaveProperty('id');
    expect(data[0]).toHaveProperty('name');
  });
});
```

**测试命令**：
```bash
npx vitest run
```

### 步骤9：代码审查

**目标**：确保代码质量

**操作**：
1. 运行ESLint检查
2. 运行TypeScript类型检查
3. 检查代码是否符合编码规范
4. 确保测试覆盖率

**检查命令**：
```bash
npm run lint
npx tsc --noEmit
npx vitest run --coverage
```

## 三、常见功能开发模板

### 模板1：列表展示功能

```typescript
import React from 'react';
import { ListItem } from '../../types';

interface ListComponentProps {
  items: ListItem[];
  onItemClick?: (item: ListItem) => void;
}

export const ListComponent: React.FC<ListComponentProps> = ({ items, onItemClick }) => {
  if (items.length === 0) {
    return <div className="text-center py-8 text-gray-500">暂无数据</div>;
  }

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">列表标题</h3>
      <div className="space-y-3">
        {items.map((item) => (
          <div
            key={item.id}
            className="flex justify-between items-center p-3 bg-gray-50 rounded-lg cursor-pointer hover:bg-gray-100 transition-colors"
            onClick={() => onItemClick?.(item)}
          >
            <span className="text-gray-800">{item.name}</span>
            <span className="text-blue-600">{item.value}</span>
          </div>
        ))}
      </div>
    </div>
  );
};
```

### 模板2：表单输入功能

```typescript
import React, { useState } from 'react';

interface FormComponentProps {
  onSubmit: (data: FormData) => void;
}

interface FormData {
  name: string;
  value: number;
}

export const FormComponent: React.FC<FormComponentProps> = ({ onSubmit }) => {
  const [formData, setFormData] = useState<FormData>({
    name: '',
    value: 0,
  });

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: name === 'value' ? Number(value) : value,
    }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">表单标题</h3>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">名称</label>
          <input
            type="text"
            name="name"
            value={formData.name}
            onChange={handleChange}
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            placeholder="请输入名称"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">数值</label>
          <input
            type="number"
            name="value"
            value={formData.value}
            onChange={handleChange}
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            placeholder="请输入数值"
          />
        </div>
        <button
          type="submit"
          className="w-full px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors"
        >
          提交
        </button>
      </form>
    </div>
  );
};
```

### 模板3：图表展示功能

```typescript
import React from 'react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { ChartData } from '../../types';

interface ChartComponentProps {
  data: ChartData[];
  title: string;
}

export const ChartComponent: React.FC<ChartComponentProps> = ({ data, title }) => {
  if (data.length === 0) {
    return <div className="text-center py-8 text-gray-500">暂无数据</div>;
  }

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">{title}</h3>
      <div className="h-64">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="name" />
            <YAxis />
            <Tooltip />
            <Bar dataKey="value" fill="#3b82f6" />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
};
```

### 模板4：详情展示功能

```typescript
import React from 'react';
import { DetailData } from '../../types';

interface DetailComponentProps {
  data: DetailData;
}

export const DetailComponent: React.FC<DetailComponentProps> = ({ data }) => {
  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <h3 className="text-lg font-semibold text-gray-800 mb-4">{data.name}</h3>
      <div className="space-y-4">
        <div className="flex justify-between">
          <span className="text-gray-600">属性1</span>
          <span className="text-gray-800 font-medium">{data.property1}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-gray-600">属性2</span>
          <span className="text-gray-800 font-medium">{data.property2}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-gray-600">属性3</span>
          <span className="text-blue-600 font-medium">{data.property3}</span>
        </div>
      </div>
    </div>
  );
};
```

## 四、开发检查清单

在完成功能开发后，检查以下项目：

- [ ] 需求分析完成，功能边界清晰
- [ ] 类型定义已添加到 `src/types/index.ts`
- [ ] Mock数据已添加到 `src/data/mockData.ts`
- [ ] 状态管理已实现（如需要）
- [ ] 组件开发完成，包含错误处理和空状态
- [ ] 组件已集成到页面
- [ ] 单元测试已编写并通过
- [ ] ESLint检查通过
- [ ] TypeScript类型检查通过
- [ ] 手动测试功能正常
- [ ] 代码符合编码规范

## 五、常见问题处理

### 问题1：TypeScript类型错误

**处理步骤**：
1. 检查类型定义是否完整
2. 确保所有属性都有正确的类型
3. 使用类型断言（as）仅在必要时

### 问题2：组件渲染错误

**处理步骤**：
1. 检查组件props是否正确传递
2. 添加错误边界
3. 使用React DevTools调试

### 问题3：状态管理问题

**处理步骤**：
1. 检查Zustand store定义是否正确
2. 使用Redux DevTools调试状态变化
3. 确保actions正确更新状态

### 问题4：构建失败

**处理步骤**：
1. 运行 `npm run lint` 检查代码风格
2. 运行 `npx tsc --noEmit` 检查类型错误
3. 查看构建日志定位具体错误
