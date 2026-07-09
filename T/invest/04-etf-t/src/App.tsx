import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { ETFListPage } from '@/pages/ETFListPage';
import { ETFDetailPage } from '@/pages/ETFDetailPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ETFListPage />} />
        <Route path="/etf/:code" element={<ETFDetailPage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;