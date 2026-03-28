/**
 * ActivityHeatmap - GitHub-style activity heatmap for daily summary
 *
 * SRP: Apenas exibe o grid de atividade estilo GitHub
 * OCP: Extensível via props
 * DIP: Recebe dados via props
 *
 * Composition: Composto por HeatmapCell
 */

import { useState, useMemo, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { ChevronLeft, ChevronRight, Info } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { toLocalDateStr } from '../../../types/reports';
import type { DailySummaryDayItem } from '../../../types/reports';

export interface ActivityHeatmapProps {
  /** Daily summary data */
  days: DailySummaryDayItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Date range title */
  title?: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function getColorForValue(value: number, max: number): string {
  if (value === 0) return 'rgba(255,255,255,0.03)';
  const ratio = value / max;
  if (ratio < 0.25) return 'rgba(5,223,114,0.2)';
  if (ratio < 0.5) return 'rgba(5,223,114,0.4)';
  if (ratio < 0.75) return 'rgba(5,223,114,0.6)';
  return 'rgba(5,223,114,0.8)';
}

function formatHours(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

interface TooltipData {
  date: string;
  hours: number;
  productivity: number;
  x: number;
  y: number;
}

export function ActivityHeatmap({
  days,
  isLoading = false,
  title = 'Heatmap de Atividade',
}: ActivityHeatmapProps) {
  const navigate = useNavigate();
  const [tooltip, setTooltip] = useState<TooltipData | null>(null);
  const [currentYear, setCurrentYear] = useState(() => new Date().getFullYear());

  // Calculate max value for color scaling
  const maxActiveSeconds = useMemo(() => {
    if (days.length === 0) return 28800; // 8 hours default
    return Math.max(...days.map(d => d.totalActiveSeconds), 3600);
  }, [days]);

  // Create a map for quick lookup
  const daysMap = useMemo(() => {
    const map = new Map<string, DailySummaryDayItem>();
    days.forEach(d => map.set(d.date, d));
    return map;
  }, [days]);

  // Generate weeks for the current year
  const weeks = useMemo(() => {
    const yearStart = new Date(currentYear, 0, 1);
    const yearEnd = new Date(currentYear, 11, 31);

    // Find first Sunday on or before Jan 1
    const firstDay = yearStart.getDay();
    const startDate = new Date(yearStart);
    startDate.setDate(startDate.getDate() - firstDay);

    const weeksData: { date: Date; dayData?: DailySummaryDayItem }[][] = [];
    let currentWeek: { date: Date; dayData?: DailySummaryDayItem }[] = [];

    const currentDate = new Date(startDate);
    while (currentDate <= yearEnd || currentWeek.length > 0) {
      const dateStr = toLocalDateStr(currentDate);
      const dayData = daysMap.get(dateStr);

      currentWeek.push({
        date: new Date(currentDate),
        dayData,
      });

      if (currentDate.getDay() === 6) {
        // Saturday - end of week
        weeksData.push(currentWeek);
        currentWeek = [];
      }

      currentDate.setDate(currentDate.getDate() + 1);

      // Safety limit
      if (weeksData.length > 54) break;
    }

    return weeksData;
  }, [currentYear, daysMap]);

  const handleCellClick = useCallback((date: Date) => {
    const dateStr = toLocalDateStr(date);
    navigate(`/activities?date=${dateStr}`);
  }, [navigate]);

  const handleCellHover = useCallback((date: Date, dayData: DailySummaryDayItem | undefined, e: React.MouseEvent) => {
    setTooltip({
      date: toLocalDateStr(date),
      hours: dayData?.totalActiveSeconds ?? 0,
      productivity: dayData?.productivityRatio ?? 0,
      x: e.clientX,
      y: e.clientY,
    });
  }, []);

  const handleCellLeave = useCallback(() => {
    setTooltip(null);
  }, []);

  // Month labels
  const monthLabels = useMemo(() => {
    const labels: { month: number; weekIndex: number }[] = [];
    let lastMonth = -1;

    weeks.forEach((week, weekIndex) => {
      const firstDayOfMonth = week[0]?.date;
      if (firstDayOfMonth) {
        const month = firstDayOfMonth.getMonth();
        if (month !== lastMonth && firstDayOfMonth.getFullYear() === currentYear) {
          labels.push({ month, weekIndex });
          lastMonth = month;
        }
      }
    });

    return labels;
  }, [weeks, currentYear]);

  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
        <CardHeader className="pb-2 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="flex items-center justify-center h-[120px]">
            <div className="w-6 h-6 border-2 border-[#4ad9ff] border-t-transparent rounded-full animate-spin" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-visible h-full flex flex-col">
      <CardHeader className="pb-2 pt-3 px-4 shrink-0">
        <CardTitle className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
            <div className="group relative">
              <Info className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)] cursor-help" />
              <div className="absolute left-0 bottom-full mb-2 px-2 py-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded text-[10px] text-[rgba(245,247,251,0.7)] whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity z-10">
                Click em um dia para ver detalhes
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setCurrentYear(y => y - 1)}
              className="w-6 h-6 rounded-md bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <ChevronLeft className="w-3 h-3 text-[rgba(245,247,251,0.5)]" />
            </button>
            <span className="text-[12px] font-medium text-[rgba(245,247,251,0.7)] w-[50px] text-center">
              {currentYear}
            </span>
            <button
              onClick={() => setCurrentYear(y => y + 1)}
              className="w-6 h-6 rounded-md bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <ChevronRight className="w-3 h-3 text-[rgba(245,247,251,0.5)]" />
            </button>
          </div>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4 flex-1">
        {/* Month labels */}
        <div className="relative mb-2 ml-[28px]">
          <div className="flex w-full">
            {monthLabels.map(({ month, weekIndex }, idx) => {
              const nextLabel = monthLabels[idx + 1];
              const widthPercent = nextLabel
                ? ((nextLabel.weekIndex - weekIndex) / weeks.length) * 100
                : ((weeks.length - weekIndex) / weeks.length) * 100;
              return (
                <span
                  key={`${month}-${weekIndex}`}
                  className="text-[10px] text-[rgba(245,247,251,0.4)] font-medium"
                  style={{
                    position: 'relative',
                    width: `${widthPercent}%`,
                    marginLeft: idx === 0 ? `${(weekIndex / weeks.length) * 100}%` : 0,
                  }}
                >
                  {MONTHS[month]}
                </span>
              );
            })}
          </div>
        </div>

        {/* Grid */}
        <div className="flex gap-1 w-full">
          {/* Day labels */}
          <div className="flex flex-col gap-1 mr-2">
            {DAYS.map((day, i) => (
              <div
                key={day}
                className="h-[14px] flex items-center justify-end"
              >
                {i % 2 === 0 && (
                  <span className="text-[9px] text-[rgba(245,247,251,0.35)] w-[22px] text-right font-medium">
                    {day}
                  </span>
                )}
              </div>
            ))}
          </div>

          {/* Weeks - flex-1 to fill available space */}
          <div className="flex gap-1 flex-1 relative">
            {weeks.map((week, weekIndex) => (
              <div key={weekIndex} className="flex flex-col gap-1 flex-1">
                {week.map((day, dayIndex) => {
                  const isActiveYear = day.date.getFullYear() === currentYear;
                  const color = isActiveYear
                    ? getColorForValue(day.dayData?.totalActiveSeconds ?? 0, maxActiveSeconds)
                    : 'rgba(255,255,255,0.02)';

                  return (
                    <motion.div
                      key={dayIndex}
                      className="w-full aspect-square rounded-[3px] cursor-pointer hover:ring-1 hover:ring-[rgba(255,255,255,0.4)]"
                      style={{ backgroundColor: color }}
                      onClick={() => handleCellClick(day.date)}
                      onMouseEnter={(e) => handleCellHover(day.date, day.dayData, e)}
                      onMouseLeave={handleCellLeave}
                      whileHover={{ scale: 1.3 }}
                      initial={{ opacity: 0, scale: 0.8 }}
                      animate={{ opacity: 1, scale: 1 }}
                      transition={{ duration: 0.15 }}
                    />
                  );
                })}
              </div>
            ))}
          </div>
        </div>

        {/* Legend */}
        <div className="flex items-center justify-end gap-2 mt-4">
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">Menos</span>
          <div className="flex gap-1">
            {[0, 0.25, 0.5, 0.75, 1].map((ratio, i) => (
              <div
                key={i}
                className="w-3.5 h-3.5 rounded-[3px]"
                style={{ backgroundColor: getColorForValue(ratio * maxActiveSeconds, maxActiveSeconds) }}
              />
            ))}
          </div>
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">Mais</span>
        </div>

        {/* Tooltip - rendered via Portal to escape container constraints */}
        {tooltip && createPortal(
          <div
            className="fixed z-9999 px-2 py-1.5 bg-[#13151f] border border-[rgba(255,255,255,0.12)] rounded-lg shadow-lg pointer-events-none"
            style={{
              left: tooltip.x,
              top: tooltip.y - 12,
              transform: 'translate(-50%, -100%)',
            }}
          >
            <p className="text-[10px] font-medium text-[#f5f7fb]">{tooltip.date}</p>
            <p className="text-[9px] text-[rgba(245,247,251,0.6)]">
              {formatHours(tooltip.hours)} trabalhadas
            </p>
            {tooltip.hours > 0 && (
              <p className="text-[9px] text-[rgba(5,223,114,0.8)]">
                {Math.round(tooltip.productivity * 100)}% produtivo
              </p>
            )}
          </div>,
          document.body
        )}
      </CardContent>
    </Card>
  );
}
