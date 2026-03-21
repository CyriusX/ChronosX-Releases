/**
 * ProductivityHeatmap — 24-cell horizontal grid showing productivity by hour
 *
 * Each hour cell is colored by the dominant productivity type during that hour.
 */

import { useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import type { ActivityBlock } from '../../hooks/useActivitiesData';

interface ProductivityHeatmapProps {
  activities: ActivityBlock[];
  selectedDate?: Date;
}

const PRODUCTIVITY_COLORS: Record<string, string> = {
  productive: '#4ade80',
  neutral: '#fbbf24',
  distraction: '#f87171',
};

const cardBase =
  'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

interface HourData {
  productive: number;
  neutral: number;
  distraction: number;
  total: number;
  dominant: string | null;
  color: string;
}

export function ProductivityHeatmap({ activities, selectedDate }: ProductivityHeatmapProps) {
  const [hovered, setHovered] = useState<{ hour: number; data: HourData; rect: DOMRect } | null>(null);

  const hourData = useMemo(() => {
    const hours: HourData[] = Array.from({ length: 24 }, () => ({
      productive: 0,
      neutral: 0,
      distraction: 0,
      total: 0,
      dominant: null,
      color: 'rgba(255,255,255,0.04)',
    }));

    if (activities.length === 0) return hours;

    // Use the same dayStart as ActivitySection — local midnight of the selected date
    const dayRef = selectedDate ? new Date(selectedDate) : new Date();
    dayRef.setHours(0, 0, 0, 0);
    const dayStartMs = dayRef.getTime();

    const nowMs = Date.now();

    for (const a of activities) {
      const start = new Date(a.startUtc).getTime();
      // Cap end time to now — prevents showing data for future hours
      const end = Math.min(new Date(a.endUtc).getTime(), nowMs);
      if (end <= start) continue; // skip if activity is entirely in the future
      const prod = a.productivity || 'neutral';

      // Only check hours that could overlap with this activity
      const startHour = Math.max(0, Math.floor((start - dayStartMs) / 3600000));
      const endHour = Math.min(23, Math.floor((end - dayStartMs) / 3600000));

      for (let h = startHour; h <= endHour; h++) {
        const hourStartMs = dayStartMs + h * 3600000;
        const hourEndMs = dayStartMs + (h + 1) * 3600000;

        const overlapStart = Math.max(start, hourStartMs);
        const overlapEnd = Math.min(end, hourEndMs);
        const overlapSec = Math.max(0, (overlapEnd - overlapStart) / 1000);

        if (overlapSec > 0) {
          const key = prod as 'productive' | 'neutral' | 'distraction';
          if (key in hours[h]) {
            hours[h][key] += overlapSec;
          } else {
            hours[h].neutral += overlapSec;
          }
          hours[h].total += overlapSec;
        }
      }
    }

    // Determine dominant productivity per hour
    // Minimum 60 seconds of activity required to color a cell —
    // filters out brief window-focus blips that are invisible on the timeline
    const MIN_ACTIVITY_SEC = 60;
    for (const h of hours) {
      if (h.total < MIN_ACTIVITY_SEC) {
        h.total = 0; // treat as empty
        continue;
      }
      if (h.productive >= h.neutral && h.productive >= h.distraction) {
        h.dominant = 'productive';
        h.color = PRODUCTIVITY_COLORS.productive;
      } else if (h.distraction >= h.productive && h.distraction >= h.neutral) {
        h.dominant = 'distraction';
        h.color = PRODUCTIVITY_COLORS.distraction;
      } else {
        h.dominant = 'neutral';
        h.color = PRODUCTIVITY_COLORS.neutral;
      }
    }

    return hours;
  }, [activities, selectedDate]);

  const hourLabels = [0, 3, 6, 9, 12, 15, 18, 21];

  return (
    <>
      <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            Produtividade por hora
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-3 px-4">
          {/* Heatmap cells */}
          <div className="flex gap-[2px]">
            {hourData.map((h, i) => (
              <div
                key={i}
                className="flex-1 h-[28px] rounded-[3px] cursor-pointer transition-all hover:brightness-125"
                style={{
                  backgroundColor: h.total > 0 ? `${h.color}40` : 'rgba(255,255,255,0.03)',
                  borderBottom: h.total > 0 ? `2px solid ${h.color}` : '2px solid transparent',
                }}
                onMouseEnter={(e) =>
                  setHovered({ hour: i, data: h, rect: e.currentTarget.getBoundingClientRect() })
                }
                onMouseLeave={() => setHovered(null)}
              />
            ))}
          </div>

          {/* Hour labels */}
          <div className="relative h-4 mt-1">
            {hourLabels.map((h) => (
              <span
                key={h}
                className="absolute text-[8px] text-[rgba(245,247,251,0.2)] -translate-x-1/2"
                style={{ left: `${(h / 24) * 100}%` }}
              >
                {h}h
              </span>
            ))}
          </div>

          {/* Legend */}
          <div className="flex gap-4 mt-1">
            {(['productive', 'neutral', 'distraction'] as const).map((key) => (
              <div key={key} className="flex items-center gap-1">
                <div
                  className="w-2 h-2 rounded-sm"
                  style={{ backgroundColor: PRODUCTIVITY_COLORS[key] }}
                />
                <span className="text-[8px] text-[rgba(245,247,251,0.3)]">
                  {key === 'productive' ? 'Produtivo' : key === 'neutral' ? 'Neutro' : 'Distração'}
                </span>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Hover tooltip */}
      {hovered &&
        hovered.data.total > 0 &&
        createPortal(<HeatmapTooltip {...hovered} />, document.body)}
    </>
  );
}

function HeatmapTooltip({
  hour,
  data,
  rect,
}: {
  hour: number;
  data: HourData;
  rect: DOMRect;
}) {
  const left = rect.left + rect.width / 2;
  const top = rect.top - 8;

  const fmtSec = (s: number) => {
    if (s < 60) return `${Math.round(s)}s`;
    return `${Math.round(s / 60)}m`;
  };

  return (
    <div
      className="pointer-events-none fixed z-[99999] -translate-x-1/2 -translate-y-full"
      style={{ left, top }}
    >
      <div className="px-2.5 py-2 rounded-lg bg-[#13151f] border border-[rgba(255,255,255,0.12)] shadow-[0_8px_30px_rgba(0,0,0,0.5)] text-center min-w-[100px]">
        <p className="text-[10px] font-medium text-[rgba(245,247,251,0.8)] mb-1">
          {String(hour).padStart(2, '0')}:00 – {String(hour + 1).padStart(2, '0')}:00
        </p>
        <div className="space-y-0.5">
          {data.productive > 0 && (
            <p className="text-[9px] text-[#4ade80]">Produtivo: {fmtSec(data.productive)}</p>
          )}
          {data.neutral > 0 && (
            <p className="text-[9px] text-[#fbbf24]">Neutro: {fmtSec(data.neutral)}</p>
          )}
          {data.distraction > 0 && (
            <p className="text-[9px] text-[#f87171]">Distração: {fmtSec(data.distraction)}</p>
          )}
        </div>
      </div>
    </div>
  );
}
