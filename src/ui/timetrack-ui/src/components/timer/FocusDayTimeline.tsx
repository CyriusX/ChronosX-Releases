/**
 * FocusDayTimeline — Vertical full-day calendar (0h–24h) showing focus session blocks
 *
 * Always renders the full 24-hour day with scroll, auto-scrolling to "now" on mount.
 * Focus sessions are positioned by their actual time on the vertical axis.
 * Hovering a session shows category breakdown (from tracked activities).
 */

import { useState, useMemo, useCallback, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Focus } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { formatDuration } from '../../lib/utils';

// ============================================================================
// TYPES
// ============================================================================

export interface FocusSession {
  id: number;
  name?: string;
  mode: 'pomodoro' | 'ultradian';
  phase: 'focus' | 'break';
  cycle: number;
  startedAt: Date;
  completedAt: Date;
  durationMs: number;
  productivity: number; // -1 = not scored (< 5min)
}

export interface TimelineActivityBlock {
  name: string;
  startUtc: string;
  endUtc: string;
  duration: number;
  subcategory: string;
  color: string;
}

interface CategoryBreakdown {
  name: string;
  duration: number;
  percentage: number;
  color: string;
}

interface FocusDayTimelineProps {
  sessions: FocusSession[];
  activities: TimelineActivityBlock[];
  currentPhase: 'idle' | 'focus' | 'break';
  currentMode: 'pomodoro' | 'ultradian';
  currentPhaseStartMs: number;
  currentCycle: number;
  currentSessionName?: string;
}

// ============================================================================
// CONSTANTS & HELPERS
// ============================================================================

const TOTAL_HOURS = 24;
const HOUR_HEIGHT = 60; // px per hour
const TIMELINE_HEIGHT = TOTAL_HOURS * HOUR_HEIGHT; // 1440px
const DAY_MS = TOTAL_HOURS * 3600000;
const MIN_BLOCK_HEIGHT = 44; // px — enough for 2 lines of text

const HOUR_LABELS = Array.from({ length: TOTAL_HOURS + 1 }, (_, i) => i);

function fmtTime(date: Date) {
  return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function fmtDurationShort(ms: number) {
  const totalMin = Math.round(ms / 60000);
  if (totalMin < 60) return `${totalMin}m`;
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return m > 0 ? `${h}h ${m}m` : `${h}h`;
}

function getCategoryBreakdown(
  session: FocusSession,
  activities: TimelineActivityBlock[],
): CategoryBreakdown[] {
  const sessionStart = session.startedAt.getTime();
  const sessionEnd = session.completedAt.getTime();

  const categoryMap = new Map<string, { duration: number; color: string }>();

  for (const a of activities) {
    const aStart = new Date(a.startUtc).getTime();
    const aEnd = new Date(a.endUtc).getTime();
    if (aStart >= sessionEnd || aEnd <= sessionStart) continue;

    const overlapStart = Math.max(aStart, sessionStart);
    const overlapEnd = Math.min(aEnd, sessionEnd);
    const overlapSec = (overlapEnd - overlapStart) / 1000;
    if (overlapSec <= 0) continue;

    const key = a.subcategory || a.name;
    const existing = categoryMap.get(key) || { duration: 0, color: a.color };
    existing.duration += overlapSec;
    categoryMap.set(key, existing);
  }

  const total = Array.from(categoryMap.values()).reduce((sum, c) => sum + c.duration, 0);

  return Array.from(categoryMap.entries())
    .map(([name, { duration, color }]) => ({
      name,
      duration,
      percentage: total > 0 ? (duration / total) * 100 : 0,
      color,
    }))
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 5);
}

// ============================================================================
// MAIN COMPONENT
// ============================================================================

export function FocusDayTimeline({
  sessions,
  activities,
  currentPhase,
  currentMode,
  currentPhaseStartMs,
  currentCycle,
  currentSessionName,
}: FocusDayTimelineProps) {
  const [hovered, setHovered] = useState<{ session: FocusSession; rect: DOMRect } | null>(null);
  const [nowMs, setNowMs] = useState(Date.now());
  const nowMarkerRef = useRef<HTMLDivElement>(null);

  // Tick every 30s to update "now" marker and live session
  useEffect(() => {
    const interval = setInterval(() => setNowMs(Date.now()), 30000);
    return () => clearInterval(interval);
  }, []);

  // Auto-scroll to "now" on mount
  useEffect(() => {
    const timer = setTimeout(() => {
      nowMarkerRef.current?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    }, 100);
    return () => clearTimeout(timer);
  }, []);

  const focusSessions = useMemo(() => sessions.filter(s => s.phase === 'focus'), [sessions]);

  // Day start (midnight today)
  const dayStartMs = useMemo(() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d.getTime();
  }, []);

  // Live focus session (currently running)
  const liveSession = useMemo<FocusSession | null>(() => {
    if (currentPhase !== 'focus' || currentPhaseStartMs <= 0) return null;
    const startedAt = new Date(currentPhaseStartMs);
    const now = new Date(nowMs);
    return {
      id: -1,
      name: currentSessionName,
      mode: currentMode,
      phase: 'focus',
      cycle: currentCycle + 1,
      startedAt,
      completedAt: now,
      durationMs: nowMs - currentPhaseStartMs,
      productivity: 0,
    };
  }, [currentPhase, currentMode, currentPhaseStartMs, currentCycle, nowMs, currentSessionName]);

  // All sessions to display (completed focus + live)
  const displaySessions = useMemo(() => {
    const result = [...focusSessions];
    if (liveSession) result.push(liveSession);
    return result;
  }, [focusSessions, liveSession]);

  // Now marker position (px from top)
  const nowPx = useMemo(() => {
    return Math.min(TIMELINE_HEIGHT, Math.max(0, ((nowMs - dayStartMs) / DAY_MS) * TIMELINE_HEIGHT));
  }, [nowMs, dayStartMs]);

  const handleMouseEnter = useCallback((session: FocusSession, e: React.MouseEvent) => {
    setHovered({ session, rect: e.currentTarget.getBoundingClientRect() });
  }, []);

  const handleMouseLeave = useCallback(() => setHovered(null), []);

  const cardBase =
    'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

  return (
    <>
      <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              Hist&oacute;rico
            </span>
            <span className="text-[10px] text-[rgba(245,247,251,0.3)] px-2 py-0.5 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)]">
              Hoje
            </span>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-3 px-4">
          <div className="relative" style={{ height: TIMELINE_HEIGHT }}>
            {/* Hour grid lines and labels */}
            {HOUR_LABELS.map(h => {
              const top = (h / TOTAL_HOURS) * TIMELINE_HEIGHT;
              return (
                <div key={h} className="absolute left-0 right-0" style={{ top }}>
                  <div className="flex items-center gap-1.5">
                    <span className="text-[9px] text-[rgba(245,247,251,0.2)] w-[32px] text-right flex-shrink-0 -translate-y-1/2 tabular-nums">
                      {h < 24 ? `${String(h).padStart(2, '0')}:00` : ''}
                    </span>
                    <div className="flex-1 h-px bg-[rgba(255,255,255,0.04)]" />
                  </div>
                </div>
              );
            })}

            {/* Focus session blocks */}
            {displaySessions.map(s => {
              const sMs = s.startedAt.getTime();
              const topPx = Math.max(0, ((sMs - dayStartMs) / DAY_MS) * TIMELINE_HEIGHT);
              const heightPx = Math.max(
                MIN_BLOCK_HEIGHT,
                (s.durationMs / DAY_MS) * TIMELINE_HEIGHT,
              );
              const isLive = s.id === -1;
              const color = s.mode === 'ultradian' ? '#c27aff' : '#4ad9ff';
              const label = s.name || `Foco #${s.cycle}`;

              return (
                <div
                  key={s.id}
                  className="absolute rounded-md cursor-pointer transition-all hover:brightness-125"
                  style={{
                    left: '40px',
                    right: '2px',
                    top: topPx,
                    height: heightPx,
                    backgroundColor: `${color}18`,
                    border: `1px solid ${color}40`,
                    boxShadow: `inset 0 0 12px ${color}08, 0 0 8px ${color}10`,
                  }}
                  onMouseEnter={e => handleMouseEnter(s, e)}
                  onMouseLeave={handleMouseLeave}
                >
                  {/* Animated left accent bar for live sessions */}
                  {isLive && (
                    <div
                      className="absolute left-0 top-0 bottom-0 w-[3px] rounded-l-md animate-pulse"
                      style={{ backgroundColor: color }}
                    />
                  )}
                  <div className="px-2 py-1.5 h-full flex flex-col justify-center gap-0.5">
                    <p className="text-[10px] font-medium leading-tight" style={{ color }}>
                      {isLive && (
                        <span
                          className="inline-block w-1.5 h-1.5 rounded-full mr-1 align-middle animate-pulse"
                          style={{ backgroundColor: color }}
                        />
                      )}
                      {label}
                    </p>
                    <p className="text-[9px] text-[rgba(245,247,251,0.4)] leading-tight">
                      {fmtTime(s.startedAt)}
                      {!isLive ? ` – ${fmtTime(s.completedAt)}` : ''} ·{' '}
                      {fmtDurationShort(s.durationMs)}
                    </p>
                  </div>
                </div>
              );
            })}

            {/* Now marker */}
            <div
              ref={nowMarkerRef}
              className="absolute left-[32px] right-0"
              style={{ top: nowPx }}
            >
              <div className="relative flex items-center">
                <div className="w-[6px] h-[6px] rounded-full bg-[#05df72] flex-shrink-0 -ml-[3px]" />
                <div className="flex-1 h-px bg-[#05df72] opacity-40" />
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tooltip rendered via portal — always on top, never clipped */}
      {hovered &&
        createPortal(
          <SessionTooltip
            session={hovered.session}
            activities={activities}
            anchorRect={hovered.rect}
          />,
          document.body,
        )}
    </>
  );
}

// ============================================================================
// TOOLTIP (rendered at document root via portal)
// ============================================================================

function SessionTooltip({
  session,
  activities,
  anchorRect,
}: {
  session: FocusSession;
  activities: TimelineActivityBlock[];
  anchorRect: DOMRect;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ left: 0, top: 0 });

  const categories = useMemo(
    () => getCategoryBreakdown(session, activities),
    [session, activities],
  );

  useEffect(() => {
    if (!ref.current) return;
    const tw = ref.current.offsetWidth;
    const th = ref.current.offsetHeight;

    // Position to the left of the block
    let left = anchorRect.left - tw - 10;
    let top = anchorRect.top + anchorRect.height / 2 - th / 2;

    // If no room on left, show on right
    if (left < 8) left = anchorRect.right + 10;
    // Clamp vertically
    if (top < 8) top = 8;
    if (top + th > window.innerHeight - 8) top = window.innerHeight - 8 - th;

    setPos({ left, top });
  }, [anchorRect]);

  const color = session.mode === 'ultradian' ? '#c27aff' : '#4ad9ff';
  const isLive = session.id === -1;
  const isTooShort = session.productivity === -1;
  const label = session.name || `Foco #${session.cycle}`;

  return (
    <div
      ref={ref}
      className="pointer-events-none"
      style={{ position: 'fixed', zIndex: 99999, left: pos.left, top: pos.top }}
    >
      <div className="px-3 py-2.5 rounded-lg bg-[#13151f] border border-[rgba(255,255,255,0.12)] shadow-[0_8px_30px_rgba(0,0,0,0.5)] min-w-[180px] max-w-[240px]">
        {/* Header */}
        <div className="flex items-center gap-2 mb-1">
          <Focus className="w-3 h-3 flex-shrink-0" style={{ color }} />
          <p className="text-[11px] font-semibold text-[#f5f7fb] truncate">
            {label}
            <span className="text-[rgba(245,247,251,0.4)] font-normal ml-1.5">
              {session.mode === 'pomodoro' ? 'Pomodoro' : 'Ultradian'}
            </span>
          </p>
        </div>

        {/* Time range + duration */}
        <p className="text-[10px] text-[rgba(245,247,251,0.5)] mb-1.5">
          {fmtTime(session.startedAt)}
          {!isLive ? ` – ${fmtTime(session.completedAt)}` : ''}
          <span className="text-[rgba(245,247,251,0.7)] font-medium ml-1.5">
            {fmtDurationShort(session.durationMs)}
          </span>
        </p>

        {/* Score / live indicator / too-short indicator */}
        <div className="mb-2">
          {isLive ? (
            <span className="text-[9px] font-medium px-1.5 py-0.5 rounded-full bg-[rgba(5,223,114,0.15)] text-[#05df72]">
              Em andamento
            </span>
          ) : isTooShort ? (
            <span className="text-[9px] font-medium px-1.5 py-0.5 rounded-full bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.35)]">
              Sess&atilde;o curta (sem score)
            </span>
          ) : (
            <span
              className={`text-[9px] font-medium px-1.5 py-0.5 rounded-full ${
                session.productivity >= 80
                  ? 'bg-[rgba(74,222,128,0.15)] text-[#4ade80]'
                  : session.productivity >= 50
                    ? 'bg-[rgba(251,191,36,0.15)] text-[#fbbf24]'
                    : 'bg-[rgba(248,113,113,0.15)] text-[#f87171]'
              }`}
            >
              Score: {session.productivity}
            </span>
          )}
        </div>

        {/* Category breakdown */}
        {categories.length > 0 ? (
          <div className="border-t border-[rgba(255,255,255,0.08)] pt-2 space-y-1.5">
            <p className="text-[8px] uppercase tracking-wider text-[rgba(245,247,251,0.25)]">
              Categorias
            </p>
            {categories.map((cat, i) => (
              <div key={i} className="flex items-center gap-1.5">
                <span className="text-[9px] text-[rgba(245,247,251,0.4)] w-[26px] text-right flex-shrink-0 tabular-nums">
                  {Math.round(cat.percentage)}%
                </span>
                <div
                  className="w-2 h-2 rounded-full flex-shrink-0"
                  style={{ backgroundColor: cat.color }}
                />
                <span className="text-[9px] text-[rgba(245,247,251,0.7)] flex-1 truncate">
                  {cat.name}
                </span>
                <span className="text-[8px] text-[rgba(245,247,251,0.3)] flex-shrink-0">
                  {formatDuration(cat.duration)}
                </span>
              </div>
            ))}
          </div>
        ) : (
          <div className="border-t border-[rgba(255,255,255,0.08)] pt-2">
            <p className="text-[9px] text-[rgba(245,247,251,0.25)] italic">
              Sem dados de atividade
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
