import React, { useState } from 'react';
import { getCalendarEvents, searchStocks } from '../data/mockData';
import { Calendar, ArrowRightLeft, ArrowRight, AlertCircle, Gift, Users } from 'lucide-react';

interface CalendarPageProps {
  onSelectStock: (stock: ReturnType<typeof searchStocks>[0]) => void;
}

export const CalendarPage: React.FC<CalendarPageProps> = ({ onSelectStock }) => {
  const [currentDate, setCurrentDate] = useState(new Date());
  const [selectedDate, setSelectedDate] = useState<string | null>(null);

  const allStocks = searchStocks();
  const events = getCalendarEvents();

  const year = currentDate.getFullYear();
  const month = currentDate.getMonth();

  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const daysInMonth = lastDay.getDate();
  const startDay = firstDay.getDay();

  const prevMonth = () => {
    setCurrentDate(new Date(year, month - 1, 1));
  };

  const nextMonth = () => {
    setCurrentDate(new Date(year, month + 1, 1));
  };

  const getEventsForDate = (date: string) => {
    return events.filter(e => e.date === date);
  };

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'earnings':
        return <AlertCircle size={16} className="text-red-500" />;
      case 'dividend':
        return <Gift size={16} className="text-green-500" />;
      case 'meeting':
        return <Users size={16} className="text-blue-500" />;
      default:
        return <Calendar size={16} className="text-gray-500" />;
    }
  };

  const getEventLabel = (type: string) => {
    switch (type) {
      case 'earnings':
        return '财报';
      case 'dividend':
        return '分红';
      case 'meeting':
        return '会议';
      default:
        return '事件';
    }
  };

  const today = new Date().toISOString().split('T')[0];

  const days = [];
  for (let i = 0; i < startDay; i++) {
    days.push(null);
  }
  for (let i = 1; i <= daysInMonth; i++) {
    days.push(i);
  }

  const selectedEvents = selectedDate ? getEventsForDate(selectedDate) : [];

  return (
    <div className="bg-white rounded-xl shadow-sm p-6">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-xl font-semibold text-gray-800">财务日历</h2>
        <button
          onClick={() => window.location.href = '/'}
          className="text-sm text-blue-600 hover:text-blue-700 flex items-center gap-1"
        >
          返回首页 <ArrowRight size={16} />
        </button>
      </div>

      <div className="flex items-center justify-between mb-4">
        <button
          onClick={prevMonth}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowRightLeft size={20} className="rotate-180" />
        </button>
        <h3 className="text-lg font-semibold text-gray-800">
          {year}年{month + 1}月
        </h3>
        <button
          onClick={nextMonth}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowRightLeft size={20} />
        </button>
      </div>

      <div className="grid grid-cols-7 gap-1 mb-2">
        {['日', '一', '二', '三', '四', '五', '六'].map(day => (
          <div key={day} className="text-center text-sm font-medium text-gray-500 py-2">
            {day}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-1">
        {days.map((day, index) => {
          if (!day) return <div key={index} className="h-16" />;

          const dateStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
          const dayEvents = getEventsForDate(dateStr);
          const isToday = dateStr === today;
          const isSelected = selectedDate === dateStr;

          return (
            <button
              key={index}
              onClick={() => setSelectedDate(isSelected ? null : dateStr)}
              className={`relative h-16 rounded-lg border transition-colors text-sm ${
                isSelected
                  ? 'border-blue-500 bg-blue-50'
                  : isToday
                  ? 'border-blue-300 bg-blue-50'
                  : 'border-gray-100 hover:border-gray-200 hover:bg-gray-50'
              }`}
            >
              <div className="p-1 text-center">
                {day}
                {isToday && (
                  <div className="absolute top-1 right-1 w-2 h-2 bg-blue-500 rounded-full" />
                )}
              </div>
              {dayEvents.length > 0 && (
                <div className="absolute bottom-1 left-1 right-1 flex justify-center gap-0.5">
                  {dayEvents.slice(0, 3).map((event, i) => (
                    <div key={i} className="w-2 h-2 rounded-full" style={{
                      backgroundColor: event.eventType === 'earnings' ? '#ef4444' :
                        event.eventType === 'dividend' ? '#22c55e' : '#3b82f6'
                    }} />
                  ))}
                </div>
              )}
            </button>
          );
        })}
      </div>

      {selectedEvents.length > 0 && (
        <div className="mt-6 border-t border-gray-200 pt-4">
          <h4 className="text-sm font-medium text-gray-600 mb-3">
            {selectedDate} 的财务事件
          </h4>
          <div className="space-y-3">
            {selectedEvents.map(event => {
              const stock = allStocks.find(s => s.code === event.stockCode);
              return (
                <div
                  key={event.id}
                  className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg cursor-pointer hover:bg-gray-100 transition-colors"
                  onClick={() => stock && onSelectStock(stock)}
                >
                  {getEventIcon(event.eventType)}
                  <div className="flex-1">
                    <div className="font-medium text-gray-800">{event.stockName}</div>
                    <div className="text-sm text-gray-500">{event.title}</div>
                  </div>
                  <span className={`px-2 py-1 text-xs rounded-full ${
                    event.eventType === 'earnings'
                      ? 'bg-red-100 text-red-600'
                      : event.eventType === 'dividend'
                      ? 'bg-green-100 text-green-600'
                      : 'bg-blue-100 text-blue-600'
                  }`}>
                    {getEventLabel(event.eventType)}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="mt-6 flex items-center gap-6 text-sm">
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded-full bg-red-500" />
          <span className="text-gray-600">财报发布</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded-full bg-green-500" />
          <span className="text-gray-600">分红派息</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded-full bg-blue-500" />
          <span className="text-gray-600">股东大会</span>
        </div>
      </div>
    </div>
  );
};

export default CalendarPage;
