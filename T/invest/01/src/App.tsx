import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import HomePage from '@/pages/HomePage';
import ComparePage from '@/pages/ComparePage';
import FavoritesPage from '@/pages/FavoritesPage';
import NewsPage from '@/pages/NewsPage';
import CalendarPage from '@/pages/CalendarPage';
import { Stock } from './types';

const handleSelectStock = (stock: Stock) => {
  window.location.href = '/';
};

export default function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/compare" element={<ComparePage />} />
        <Route path="/favorites" element={<FavoritesPage onSelectStock={handleSelectStock} />} />
        <Route path="/news" element={<NewsPage onSelectStock={handleSelectStock} />} />
        <Route path="/calendar" element={<CalendarPage onSelectStock={handleSelectStock} />} />
      </Routes>
    </Router>
  );
}
