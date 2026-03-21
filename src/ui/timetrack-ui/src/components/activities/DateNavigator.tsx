/**
 * DateNavigator — Arrow-based date navigation with calendar dropdown
 *
 * ◀  Sábado, 22 Mar 2025  ▶  [Hoje]  📅
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import { ChevronLeft, ChevronRight, CalendarDays } from 'lucide-react';

interface DateNavigatorProps {
  selectedDate: Date;
  isToday: boolean;
  onPrevDay: () => void;
  onNextDay: () => void;
  onToday: () => void;
  onDateSelect: (date: Date) => void;
}

export function DateNavigator({
  selectedDate,
  isToday,
  onPrevDay,
  onNextDay,
  onToday,
  onDateSelect,
}: DateNavigatorProps) {
  const [showCalendar, setShowCalendar] = useState(false);
  const calendarRef = useRef<HTMLDivElement>(null);

  // Close calendar on outside click
  useEffect(() => {
    if (!showCalendar) return;
    const handler = (e: MouseEvent) => {
      if (calendarRef.current && !calendarRef.current.contains(e.target as Node)) {
        setShowCalendar(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [showCalendar]);

  const dateLabel = selectedDate.toLocaleDateString('pt-BR', {
    weekday: 'long',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
  // Capitalize first letter
  const formattedDate = dateLabel.charAt(0).toUpperCase() + dateLabel.slice(1);

  return (
    <div className="flex items-center gap-2">
      {/* Prev */}
      <button
        onClick={onPrevDay}
        className="p-1.5 rounded-lg hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] transition-colors"
      >
        <ChevronLeft className="w-4 h-4" />
      </button>

      {/* Date label */}
      <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)] min-w-[200px] text-center">
        {formattedDate}
      </span>

      {/* Next */}
      <button
        onClick={onNextDay}
        disabled={isToday}
        className={`p-1.5 rounded-lg transition-colors ${
          isToday
            ? 'text-[rgba(245,247,251,0.15)] cursor-not-allowed'
            : 'hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
        }`}
      >
        <ChevronRight className="w-4 h-4" />
      </button>

      {/* Hoje button */}
      {!isToday && (
        <button
          onClick={onToday}
          className="px-3 py-1 rounded-full text-[10px] font-medium bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-white hover:opacity-90 transition-opacity"
        >
          Hoje
        </button>
      )}

      {/* Calendar toggle */}
      <div className="relative" ref={calendarRef}>
        <button
          onClick={() => setShowCalendar(!showCalendar)}
          className={`p-1.5 rounded-lg transition-colors ${
            showCalendar
              ? 'bg-[rgba(255,255,255,0.08)] text-[#4ad9ff]'
              : 'hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.4)]'
          }`}
        >
          <CalendarDays className="w-4 h-4" />
        </button>

        {showCalendar && (
          <CalendarDropdown
            selectedDate={selectedDate}
            onDateSelect={(d) => {
              onDateSelect(d);
              setShowCalendar(false);
            }}
          />
        )}
      </div>
    </div>
  );
}

// ============================================================================
// CALENDAR DROPDOWN
// ============================================================================

function CalendarDropdown({
  selectedDate,
  onDateSelect,
}: {
  selectedDate: Date;
  onDateSelect: (date: Date) => void;
}) {
  const [viewYear, setViewYear] = useState(selectedDate.getFullYear());
  const [viewMonth, setViewMonth] = useState(selectedDate.getMonth());

  const today = new Date();
  const daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate();
  const firstDayOfWeek = new Date(viewYear, viewMonth, 1).getDay(); // 0=Sunday
  const dayNames = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];
  const monthNames = [
    'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
    'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
  ];

  const prevMonth = useCallback(() => {
    if (viewMonth === 0) { setViewMonth(11); setViewYear(y => y - 1); }
    else setViewMonth(m => m - 1);
  }, [viewMonth]);

  const nextMonth = useCallback(() => {
    // Don't go past current month
    const now = new Date();
    if (viewYear === now.getFullYear() && viewMonth >= now.getMonth()) return;
    if (viewMonth === 11) { setViewMonth(0); setViewYear(y => y + 1); }
    else setViewMonth(m => m + 1);
  }, [viewMonth, viewYear]);

  const isFutureMonth =
    viewYear > today.getFullYear() ||
    (viewYear === today.getFullYear() && viewMonth >= today.getMonth());

  const cells: (number | null)[] = [];
  for (let i = 0; i < firstDayOfWeek; i++) cells.push(null);
  for (let d = 1; d <= daysInMonth; d++) cells.push(d);

  return (
    <div className="absolute right-0 top-full mt-2 z-50 w-[240px] p-3 rounded-xl bg-[#13151f] border border-[rgba(255,255,255,0.1)] shadow-[0_8px_30px_rgba(0,0,0,0.5)]">
      {/* Month nav */}
      <div className="flex items-center justify-between mb-3">
        <button
          onClick={prevMonth}
          className="p-1 rounded hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]"
        >
          <ChevronLeft className="w-3.5 h-3.5" />
        </button>
        <span className="text-[11px] font-medium text-[rgba(245,247,251,0.8)]">
          {monthNames[viewMonth]} {viewYear}
        </span>
        <button
          onClick={nextMonth}
          disabled={isFutureMonth}
          className={`p-1 rounded ${
            isFutureMonth
              ? 'text-[rgba(245,247,251,0.1)] cursor-not-allowed'
              : 'hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]'
          }`}
        >
          <ChevronRight className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Day names */}
      <div className="grid grid-cols-7 gap-0.5 mb-1">
        {dayNames.map((n, i) => (
          <span
            key={i}
            className="text-[8px] text-[rgba(245,247,251,0.25)] text-center font-medium"
          >
            {n}
          </span>
        ))}
      </div>

      {/* Day cells */}
      <div className="grid grid-cols-7 gap-0.5">
        {cells.map((day, i) => {
          if (day === null) return <div key={i} />;

          const cellDate = new Date(viewYear, viewMonth, day);
          const isFuture = cellDate > today;
          const isSelected =
            selectedDate.getFullYear() === viewYear &&
            selectedDate.getMonth() === viewMonth &&
            selectedDate.getDate() === day;
          const isCurrentDay =
            today.getFullYear() === viewYear &&
            today.getMonth() === viewMonth &&
            today.getDate() === day;

          return (
            <button
              key={i}
              disabled={isFuture}
              onClick={() => onDateSelect(cellDate)}
              className={`w-full aspect-square flex items-center justify-center rounded-md text-[10px] transition-all ${
                isSelected
                  ? 'bg-[#4ad9ff] text-[#0b0d14] font-bold'
                  : isCurrentDay
                    ? 'bg-[rgba(74,217,255,0.15)] text-[#4ad9ff] font-medium'
                    : isFuture
                      ? 'text-[rgba(245,247,251,0.1)] cursor-not-allowed'
                      : 'text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.06)]'
              }`}
            >
              {day}
            </button>
          );
        })}
      </div>
    </div>
  );
}
