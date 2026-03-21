/**
 * MiniCalendar — Monthly calendar for the Activities right panel
 *
 * Days are color-coded by tracked hours. Click any day to navigate.
 */

import { useState, useCallback } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import type { WeeklyHistoryItem } from '../../types/ipc';

interface MiniCalendarProps {
  selectedDate: Date;
  onDateSelect: (date: Date) => void;
  weeklyHistory: WeeklyHistoryItem[];
}

const cardBase =
  'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

const MONTH_NAMES = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

const DAY_NAMES = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

function getHoursColor(hours: number): string {
  if (hours <= 0) return 'rgba(255,255,255,0.03)';
  if (hours < 2) return 'rgba(74,217,255,0.15)';
  if (hours < 4) return 'rgba(74,217,255,0.25)';
  if (hours < 6) return 'rgba(74,217,255,0.4)';
  return 'rgba(74,217,255,0.6)';
}

export function MiniCalendar({ selectedDate, onDateSelect, weeklyHistory }: MiniCalendarProps) {
  const [viewYear, setViewYear] = useState(selectedDate.getFullYear());
  const [viewMonth, setViewMonth] = useState(selectedDate.getMonth());

  const today = new Date();
  const daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate();
  const firstDayOfWeek = new Date(viewYear, viewMonth, 1).getDay();

  // Build a map of date -> hours from weeklyHistory
  const hoursMap = new Map<string, number>();
  for (const item of weeklyHistory) {
    hoursMap.set(item.date, item.hours);
  }

  const prevMonth = useCallback(() => {
    if (viewMonth === 0) {
      setViewMonth(11);
      setViewYear((y) => y - 1);
    } else {
      setViewMonth((m) => m - 1);
    }
  }, [viewMonth]);

  const nextMonth = useCallback(() => {
    if (viewYear === today.getFullYear() && viewMonth >= today.getMonth()) return;
    if (viewMonth === 11) {
      setViewMonth(0);
      setViewYear((y) => y + 1);
    } else {
      setViewMonth((m) => m + 1);
    }
  }, [viewMonth, viewYear, today]);

  const isFutureMonth =
    viewYear > today.getFullYear() ||
    (viewYear === today.getFullYear() && viewMonth >= today.getMonth());

  const cells: (number | null)[] = [];
  for (let i = 0; i < firstDayOfWeek; i++) cells.push(null);
  for (let d = 1; d <= daysInMonth; d++) cells.push(d);

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Calendario</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-3 px-4">
        {/* Month nav */}
        <div className="flex items-center justify-between mb-3">
          <button
            onClick={prevMonth}
            className="p-1 rounded hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]"
          >
            <ChevronLeft className="w-3.5 h-3.5" />
          </button>
          <span className="text-[11px] font-medium text-[rgba(245,247,251,0.7)]">
            {MONTH_NAMES[viewMonth]} {viewYear}
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

        {/* Day names header */}
        <div className="grid grid-cols-7 gap-1 mb-1">
          {DAY_NAMES.map((n, i) => (
            <span key={i} className="text-[8px] text-[rgba(245,247,251,0.25)] text-center font-medium">
              {n}
            </span>
          ))}
        </div>

        {/* Day cells */}
        <div className="grid grid-cols-7 gap-1">
          {cells.map((day, i) => {
            if (day === null) return <div key={i} />;

            const cellDate = new Date(viewYear, viewMonth, day);
            const dateKey = `${viewYear}-${String(viewMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
            const hours = hoursMap.get(dateKey) ?? 0;
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
                className={`w-full aspect-square flex items-center justify-center rounded-md text-[9px] transition-all relative ${
                  isSelected
                    ? 'ring-1 ring-[#4ad9ff] font-bold text-[#f5f7fb]'
                    : isFuture
                      ? 'text-[rgba(245,247,251,0.08)] cursor-not-allowed'
                      : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
                }`}
                style={{
                  backgroundColor: isFuture ? 'transparent' : getHoursColor(hours),
                }}
              >
                {day}
                {isCurrentDay && !isSelected && (
                  <span className="absolute bottom-[2px] left-1/2 -translate-x-1/2 w-1 h-1 rounded-full bg-[#4ad9ff]" />
                )}
              </button>
            );
          })}
        </div>

        {/* Legend */}
        <div className="flex items-center justify-center gap-3 mt-3">
          <div className="flex items-center gap-1">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: 'rgba(255,255,255,0.03)' }} />
            <span className="text-[7px] text-[rgba(245,247,251,0.25)]">0h</span>
          </div>
          <div className="flex items-center gap-1">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: 'rgba(74,217,255,0.25)' }} />
            <span className="text-[7px] text-[rgba(245,247,251,0.25)]">2-4h</span>
          </div>
          <div className="flex items-center gap-1">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: 'rgba(74,217,255,0.6)' }} />
            <span className="text-[7px] text-[rgba(245,247,251,0.25)]">6h+</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
