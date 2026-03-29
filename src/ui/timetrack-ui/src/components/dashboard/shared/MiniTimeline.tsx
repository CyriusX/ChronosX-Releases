/**
 * MiniTimeline - Compact activity timeline for BottomCards "Hoje" card
 */

import { useMemo, useState, useEffect, useCallback } from 'react';
import { useIpc } from '../../../hooks/useIpc';

interface ActivityBlock {
  id: string;
  name: string;
  startUtc: string;
  endUtc: string;
  duration: number;
  color: string;
}

const WORK_START = 8;
const WORK_END = 18;
const WORK_HOURS = WORK_END - WORK_START;

export function MiniTimeline() {
  const { sendQuery, isConnected } = useIpc();
  const [activities, setActivities] = useState<ActivityBlock[]>([]);

  const fetchActivities = useCallback(async () => {
    try {
      const res = await sendQuery('getRecentActivities');
      if (res.success && res.data) {
        const data = res.data as unknown as { activities: ActivityBlock[] };
        setActivities(data.activities ?? []);
      }
    } catch { /* ignore */ }
  }, [sendQuery]);

  useEffect(() => {
    if (!isConnected) return;
    fetchActivities();
    const interval = setInterval(fetchActivities, 30000);
    return () => clearInterval(interval);
  }, [isConnected, fetchActivities]);

  const dayStart = useMemo(() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d.getTime();
  }, []);

  const blocks = useMemo(() => {
    const workStartMs = dayStart + WORK_START * 3600000;
    const workRangeMs = WORK_HOURS * 3600000;

    return activities.map(a => {
      const s = Math.max(new Date(a.startUtc).getTime(), workStartMs);
      const e = Math.min(new Date(a.endUtc).getTime(), workStartMs + workRangeMs);
      if (e <= s) return null;
      const left = ((s - workStartMs) / workRangeMs) * 100;
      const width = Math.max(0.3, ((e - s) / workRangeMs) * 100);
      return { ...a, left: Math.min(left, 100), width: Math.min(width, 100 - left) };
    }).filter(Boolean) as (ActivityBlock & { left: number; width: number })[];
  }, [activities, dayStart]);

  const hourLabels = Array.from({ length: WORK_HOURS + 1 }, (_, i) => WORK_START + i);

  return (
    <div>
      {/* Hour labels */}
      <div className="relative h-3 mb-0.5">
        {hourLabels.filter((_, i) => i % 2 === 0).map((h) => (
          <span
            key={h}
            className="absolute text-[7px] text-[rgba(245,247,251,0.2)] -translate-x-1/2"
            style={{ left: `${((h - WORK_START) / WORK_HOURS) * 100}%` }}
          >
            {h}
          </span>
        ))}
      </div>

      {/* Timeline bar */}
      <div className="relative h-[20px] rounded-md bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)]">
        {hourLabels.slice(1, -1).map((h) => (
          <div
            key={h}
            className="absolute top-0 bottom-0 w-px bg-[rgba(255,255,255,0.03)]"
            style={{ left: `${((h - WORK_START) / WORK_HOURS) * 100}%` }}
          />
        ))}
        {blocks.map((block, i) => (
          <div
            key={block.id || i}
            style={{
              left: `${block.left}%`,
              width: `${block.width}%`,
              backgroundColor: block.color,
              minWidth: '1px',
            }}
            className="absolute top-[2px] bottom-[2px] rounded-[2px]"
          />
        ))}
      </div>
    </div>
  );
}
