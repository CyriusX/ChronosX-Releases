/**
 * MiniCalendar — Monthly calendar for the Activities right panel
 *
 * Days are color-coded by tracked hours. Click any day to navigate.
 */

import { useState, useCallback, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { SPRING } from '../../lib/animation';
import type { WeeklyHistoryItem } from '../../types/ipc';

interface MiniCalendarProps {
  selectedDate: Date;
  onDateSelect: (date: Date) => void;
  weeklyHistory: WeeklyHistoryItem[];
}

const cardBase =
  'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

const MONTH_NAMES = [
  'Janeiro', 'Fevereiro', 'Marco', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

const DAY_NAMES = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

function getHoursColor(hours: number): string {
  if (hours <= 0) return 'rgba(255,255,255,0.03)';
  if (hours < 2) return 'rgba(139,92,246,0.15)';
  if (hours < 4) return 'rgba(139,92,246,0.25)';
  if (hours < 6) return 'rgba(139,92,246,0.4)';
  return 'rgba(139,92,246,0.6)';
}

export function MiniCalendar({ selectedDate, onDateSelect, weeklyHistory }: MiniCalendarProps) {
  const [viewYear, setViewYear] = useState(selectedDate.getFullYear());
  const [viewMonth, setViewMonth] = useState(selectedDate.getMonth());
  const directionRef = useRef(1); // 1 = forward, -1 = backward

  const today = new Date();
  const daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate();
  const firstDayOfWeek = new Date(viewYear, viewMonth, 1).getDay();

  // Build a map of date -> hours from weeklyHistory
  const hoursMap = new Map<string, number>();
  for (const item of weeklyHistory) {
    hoursMap.set(item.date, item.hours);
  }

  const prevMonth = useCallback(() => {
    directionRef.current = -1;
    if (viewMonth === 0) {
      setViewMonth(11);
      setViewYear((y) => y - 1);
    } else {
      setViewMonth((m) => m - 1);
    }
  }, [viewMonth]);

  const nextMonth = useCallback(() => {
    if (viewYear === today.getFullYear() && viewMonth >= today.getMonth()) return;
    directionRef.current = 1;
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

  const slideOffset = 40;

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
          <AnimatePresence mode="wait">
            <motion.span
              key={`${viewYear}-${viewMonth}`}
              className="text-[11px] font-medium text-[rgba(245,247,251,0.7)]"
              initial={{ opacity: 0, x: directionRef.current * slideOffset }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: directionRef.current * -slideOffset }}
              transition={{ duration: 0.2 }}
            >
              {MONTH_NAMES[viewMonth]} {viewYear}
            </motion.span>
          </AnimatePresence>
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
        <AnimatePresence mode="wait">
          <motion.div
            key={`${viewYear}-${viewMonth}`}
            className="grid grid-cols-7 gap-1"
            initial={{ opacity: 0, x: directionRef.current * slideOffset }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: directionRef.current * -slideOffset }}
            transition={{ duration: 0.2 }}
          >
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
                <motion.button
                  key={i}
                  disabled={isFuture}
                  onClick={() => onDateSelect(cellDate)}
                  className={`w-full aspect-square flex items-center justify-center rounded-md text-[9px] transition-colors relative ${
                    isSelected
                      ? 'font-bold text-[#f5f7fb]'
                      : isFuture
                        ? 'text-[rgba(245,247,251,0.08)] cursor-not-allowed'
                        : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
                  }`}
                  style={{
                    backgroundColor: isFuture ? 'transparent' : getHoursColor(hours),
                  }}
                  whileHover={!isFuture ? { scale: 1.2 } : undefined}
                  transition={SPRING.snappy}
                >
                  {day}
                  {isSelected && (
                    <motion.div
                      layoutId="calendar-selected"
                      className="absolute inset-0 rounded-md ring-1 ring-[#8B5CF6]"
                      transition={SPRING.snappy}
                    />
                  )}
                  {isCurrentDay && !isSelected && (
                    <span className="absolute bottom-[2px] left-1/2 -translate-x-1/2 w-1 h-1 rounded-full bg-[#8B5CF6]" />
                  )}
                </motion.button>
              );
            })}
          </motion.div>
        </AnimatePresence>

        {/* Legend */}
        <div className="flex items-center justify-center gap-3 mt-3">
          <div className="flex items-center gap-1">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: 'rgba(255,255,255,0.03)' }} />
            <span className="text-[7px] text-[rgba(245,247,251,0.25)]">0h</span>
          </div>
          <div className="flex items-center gap-1">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: 'rgba(139,92,246,0.25)' }} />
            <span className="text-[7px] text-[rgba(245,247,251,0.25)]">2-4h</span>
          </div>
          <div className="flex items-center gap-1">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: 'rgba(139,92,246,0.6)' }} />
            <span className="text-[7px] text-[rgba(245,247,251,0.25)]">6h+</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
