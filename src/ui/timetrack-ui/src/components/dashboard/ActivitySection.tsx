/**
 * ActivitySection - Full-day timeline (ManicTime-style)
 *
 * Shows 0h → 23h axis with colored blocks for each app usage period.
 * Blocks are grouped by executable (browser tabs merged into "Chrome").
 * Hover shows app name, time range, duration, and list of tabs/windows used.
 * Tooltip renders via portal to avoid z-index/overflow issues.
 */

import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { useIpc } from '../../hooks/useIpc';

interface TabDetail {
  title: string;
  duration: number;
  subcategory: string;
  color: string;
}

interface ActivityBlock {
  id: string;
  name: string;
  startUtc: string;
  endUtc: string;
  duration: number;
  productivity: string;
  subcategory: string;
  color: string;
  tabs?: TabDetail[];
}

const POLL_INTERVAL = 10000;
const TOTAL_HOURS = 24;

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}

function fmtTime(iso: string) {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function fmtDuration(sec: number) {
  if (sec < 60) return `${sec}s`;
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  if (m < 60) return s > 0 ? `${m}m ${s}s` : `${m}m`;
  const h = Math.floor(m / 60);
  const rm = m % 60;
  return rm > 0 ? `${h}h ${rm}m` : `${h}h`;
}

interface ActivitySectionProps {
  /** When provided, skip internal fetch and use these activities */
  activities?: ActivityBlock[];
  /** When provided, use this date for dayStart instead of today */
  selectedDate?: Date;
}

export function ActivitySection({ activities: controlledActivities, selectedDate }: ActivitySectionProps = {}) {
  const { sendQuery, isConnected } = useIpc();
  const [internalActivities, setInternalActivities] = useState<ActivityBlock[]>([]);
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [hovered, setHovered] = useState<{ block: ActivityBlock; rect: DOMRect } | null>(null);

  const isControlled = controlledActivities !== undefined;
  const activities = isControlled ? controlledActivities : internalActivities;

  const fetchActivities = useCallback(async () => {
    if (isControlled) return;
    try {
      const res = await sendQuery('getRecentActivities');
      if (res.success && res.data) {
        const data = res.data as unknown as { activities: ActivityBlock[] };
        setInternalActivities(data.activities ?? []);
      }
    } catch { /* ignore */ }
  }, [sendQuery, isControlled]);

  useEffect(() => {
    if (isControlled || !isConnected) return;
    fetchActivities();
    const interval = setInterval(fetchActivities, POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [isConnected, fetchActivities, isControlled]);

  const dayStart = useMemo(() => {
    const d = selectedDate ? new Date(selectedDate) : new Date();
    d.setHours(0, 0, 0, 0);
    return d.getTime();
  }, [selectedDate]);
  const dayMs = TOTAL_HOURS * 3600000;

  const blocks = useMemo(() => {
    return activities.map(a => {
      const s = new Date(a.startUtc).getTime();
      const e = new Date(a.endUtc).getTime();
      const left = Math.max(0, ((s - dayStart) / dayMs) * 100);
      const width = Math.max(0.15, ((e - s) / dayMs) * 100);
      return { ...a, left: Math.min(left, 100), width: Math.min(width, 100 - left) };
    });
  }, [activities, dayStart, dayMs]);

  const legendItems = useMemo(() => {
    const seen = new Map<string, { color: string; name: string }>();
    for (const a of activities) {
      if (!seen.has(a.name)) seen.set(a.name, { color: a.color, name: a.name });
    }
    return Array.from(seen.values());
  }, [activities]);

  const hourLabels = [0, 3, 6, 9, 12, 15, 18, 21, 24];

  const isViewingToday = !selectedDate || isSameDay(selectedDate, new Date());
  const nowPct = useMemo(() => {
    if (!isViewingToday) return -1;
    return Math.min(100, Math.max(0, ((Date.now() - dayStart) / dayMs) * 100));
  }, [dayStart, dayMs, isViewingToday]);

  const handleMouseEnter = useCallback((block: ActivityBlock, e: React.MouseEvent) => {
    setHovered({ block, rect: e.currentTarget.getBoundingClientRect() });
  }, []);

  const handleMouseLeave = useCallback(() => setHovered(null), []);

  const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl";

  return (
    <>
      <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <button
              onClick={() => setIsCollapsed(!isCollapsed)}
              className="flex items-center gap-1.5 hover:opacity-80 transition-opacity"
            >
              {isCollapsed
                ? <ChevronRight className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
                : <ChevronDown className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
              }
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Atividade</span>
            </button>
            <span className="text-[10px] text-[rgba(245,247,251,0.3)] px-2 py-0.5 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)]">
              {!selectedDate || isSameDay(selectedDate, new Date()) ? 'Hoje' : selectedDate.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })}
            </span>
          </CardTitle>
        </CardHeader>

        {!isCollapsed && (
          <CardContent className="pt-3 pb-3 px-4">
            {activities.length === 0 ? (
              <div className="text-center py-6">
                <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Nenhuma atividade registrada hoje</p>
              </div>
            ) : (
              <div>
                {/* Hour labels */}
                <div className="relative h-4 mb-1">
                  {hourLabels.map((h) => (
                    <span
                      key={h}
                      className="absolute text-[8px] text-[rgba(245,247,251,0.25)] -translate-x-1/2"
                      style={{ left: `${(h / TOTAL_HOURS) * 100}%` }}
                    >
                      {h}h
                    </span>
                  ))}
                </div>

                {/* Timeline bar */}
                <div className="relative h-[28px] rounded-md bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)]">
                  {/* Hour grid */}
                  {hourLabels.slice(1, -1).map((h) => (
                    <div key={h} className="absolute top-0 bottom-0 w-px bg-[rgba(255,255,255,0.03)]" style={{ left: `${(h / TOTAL_HOURS) * 100}%` }} />
                  ))}

                  {/* Blocks */}
                  {blocks.map((block, i) => (
                    <div
                      key={block.id || i}
                      className="absolute top-[2px] bottom-[2px] rounded-[3px] cursor-pointer transition-all hover:brightness-125"
                      style={{
                        left: `${block.left}%`,
                        width: `${block.width}%`,
                        backgroundColor: block.color,
                        boxShadow: `0 0 6px ${block.color}25`,
                        minWidth: '1px',
                      }}
                      onMouseEnter={(e) => handleMouseEnter(block, e)}
                      onMouseLeave={handleMouseLeave}
                    />
                  ))}

                  {/* Now marker (only when viewing today) */}
                  {nowPct >= 0 && (
                    <div className="absolute top-0 bottom-0 w-px bg-[rgba(245,247,251,0.4)]" style={{ left: `${nowPct}%` }}>
                      <div className="absolute -top-[3px] left-1/2 -translate-x-1/2 w-[5px] h-[5px] rounded-full bg-[rgba(245,247,251,0.6)]" />
                    </div>
                  )}
                </div>

                {/* Legend */}
                <div className="flex flex-wrap gap-x-3 gap-y-1 mt-2">
                  {legendItems.map((item) => (
                    <div key={item.name} className="flex items-center gap-1">
                      <div className="w-2 h-2 rounded-sm" style={{ backgroundColor: item.color }} />
                      <span className="text-[8px] text-[rgba(245,247,251,0.35)]">{item.name}</span>
                    </div>
                  ))}
                  <div className="flex items-center gap-1">
                    <div className="w-2 h-2 rounded-sm bg-[rgba(255,255,255,0.06)]" />
                    <span className="text-[8px] text-[rgba(245,247,251,0.35)]">Inativo</span>
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        )}
      </Card>

      {/* Tooltip rendered via portal — always on top, never clipped */}
      {hovered && createPortal(
        <ActivityTooltip block={hovered.block} anchorRect={hovered.rect} />,
        document.body
      )}
    </>
  );
}

// ============================================================================
// TOOLTIP (rendered at document root via portal)
// ============================================================================

function ActivityTooltip({ block, anchorRect }: { block: ActivityBlock; anchorRect: DOMRect }) {
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ left: 0, top: 0 });

  useEffect(() => {
    if (!ref.current) return;
    const tw = ref.current.offsetWidth;
    const th = ref.current.offsetHeight;

    let left = anchorRect.left + anchorRect.width / 2 - tw / 2;
    let top = anchorRect.top - th - 10;

    // Clamp to viewport
    if (left < 8) left = 8;
    if (left + tw > window.innerWidth - 8) left = window.innerWidth - 8 - tw;
    if (top < 8) top = anchorRect.bottom + 8; // flip below if no room above

    setPos({ left, top });
  }, [anchorRect]);

  return (
    <div
      ref={ref}
      className="pointer-events-none"
      style={{ position: 'fixed', zIndex: 99999, left: pos.left, top: pos.top }}
    >
      <div className="px-3 py-2.5 rounded-lg bg-[#13151f] border border-[rgba(255,255,255,0.12)] shadow-[0_8px_30px_rgba(0,0,0,0.5)] max-w-[260px]">
        {/* App name + color */}
        <div className="flex items-center gap-2 mb-1.5">
          <div className="w-2.5 h-2.5 rounded-sm flex-shrink-0" style={{ backgroundColor: block.color }} />
          <p className="text-[11px] font-semibold text-[#f5f7fb] truncate">{block.name}</p>
        </div>

        {/* Time range + duration */}
        <p className="text-[10px] text-[rgba(245,247,251,0.5)] mb-2">
          {fmtTime(block.startUtc)} – {fmtTime(block.endUtc)}
          <span className="text-[rgba(245,247,251,0.7)] font-medium ml-1.5">{fmtDuration(block.duration)}</span>
        </p>

        {/* Tabs / windows */}
        {block.tabs && block.tabs.length > 0 && (
          <div className="border-t border-[rgba(255,255,255,0.08)] pt-1.5 space-y-[5px]">
            <p className="text-[8px] uppercase tracking-wider text-[rgba(245,247,251,0.25)] mb-1">Atividades</p>
            {block.tabs.map((tab, idx) => (
              <div key={idx} className="flex items-start gap-1.5">
                <div className="w-1.5 h-1.5 rounded-full flex-shrink-0 mt-[3px]" style={{ backgroundColor: tab.color }} />
                <span className="text-[9px] text-[rgba(245,247,251,0.7)] flex-1 leading-tight" style={{ wordBreak: 'break-word' }}>{tab.title}</span>
                <span className="text-[8px] text-[rgba(245,247,251,0.3)] flex-shrink-0 ml-1">{fmtDuration(tab.duration)}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
