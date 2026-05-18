/**
 * ActivitySection - Full-day timeline (ManicTime-style)
 *
 * Features:
 * - Real-time "now" pin that moves every 5 seconds
 * - Live "Tracking Stopped" block that grows while monitoring is paused
 * - Tooltip with activity details
 */

import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { createPortal } from 'react-dom';
import { ChevronDown, ChevronLeft, ChevronRight, MoreVertical, Camera } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { useIpc } from '../../hooks/useIpc';
import { useTrackingStore } from '../../stores/trackingStore';
import { getDailyActivities } from '../../services/reportApi';
import { getEvidenceByPeriod } from '../../services/evidenceApi';
import { closeMyOpenTaskTimer, getMyTaskEntries, getUserTaskEntries, type TaskEntryDto } from '../../services/projectsApi';
import { useHiddenAppsStore } from '../../stores/hiddenAppsStore';
import { cardBase } from './shared/styles';
import { EvidenceModal } from '../evidence/EvidenceModal';
import { ScreenshotStrip } from '../evidence/ScreenshotStrip';
import type { EvidenceItem } from '../../types/evidence';

interface TabDetail {
  title: string;
  duration: number;
  subcategory: string;
  color: string;
}

interface ActivityBlock {
  id: string;
  kind?: 'activity' | 'idle';
  name: string;
  startUtc: string;
  endUtc: string;
  duration: number;
  productivity: string;
  subcategory: string;
  color: string;
  domain?: string;

  reasonCode?: string;
  note?: string;
  submittedAtUtc?: string;
  tabs?: TabDetail[];
}

const POLL_INTERVAL = 10000;
const TICK_INTERVAL = 5000;
const TOTAL_HOURS = 24;
const TRACKING_STOPPED_COLOR = '#f87171';
const TRACKING_STOPPED_NAME = 'Tracking Stopped';

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}

function fmtTime(iso: string) {
  return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
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

// Module-level — survives component unmount/remount across page navigation
let _persistedGap: { start: number; end: number | null } | null = null;

type DateRange = 'today' | 'yesterday' | '7days';
type EvidenceFilter = 'all' | 'withEvidence' | 'byDomain';

interface ActivitySectionProps {
  activities?: ActivityBlock[];
  selectedDate?: Date;
  userId?: string;
  hideEvidence?: boolean;
}

export function ActivitySection({ activities: controlledActivities, selectedDate: externalSelectedDate, userId, hideEvidence }: ActivitySectionProps = {}) {
  const { t } = useTranslation();
  const [dateRange, setDateRange] = useState<DateRange>('today');

  // Compute selectedDate from dateRange when not externally controlled
  const selectedDate = useMemo(() => {
    if (externalSelectedDate) return externalSelectedDate;
    if (dateRange === 'yesterday') {
      const d = new Date();
      d.setDate(d.getDate() - 1);
      return d;
    }
    return undefined; // today
  }, [dateRange, externalSelectedDate]);
  const { sendQuery, subscribeToEvent, isConnected } = useIpc();
  const isTracking = useTrackingStore(s => s.isTracking);
  const isPaused = useTrackingStore(s => s.isPaused);
  const isActive = isTracking && !isPaused;
  const isLocalViewer = !userId;

  const [internalActivities, setInternalActivities] = useState<ActivityBlock[]>([]);
  const [evidenceItems, setEvidenceItems] = useState<EvidenceItem[]>([]);
  const [evidenceFilter, setEvidenceFilter] = useState<EvidenceFilter>('all');
  const [evidenceModalIndex, setEvidenceModalIndex] = useState<number | null>(null);
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [hovered, setHovered] = useState<{ block: ActivityBlock; rect: DOMRect } | null>(null);

  // Tick every 5s so the "now" pin and live "Tracking Stopped" block update
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), TICK_INTERVAL);
    return () => clearInterval(id);
  }, []);

  // Controlled vs uncontrolled mode — must be defined before any effects that use them
  const isControlled = controlledActivities !== undefined;
  const activities = isControlled ? controlledActivities : internalActivities;

  const dayStart = useMemo(() => {
    const d = selectedDate ? new Date(selectedDate) : new Date();
    d.setHours(0, 0, 0, 0);
    return d.getTime();
  }, [selectedDate]);
  const dayMs = TOTAL_HOURS * 3600000;

  const isViewingToday = !selectedDate || isSameDay(selectedDate, new Date());

  // Track pause/resume gap — initialized from module-level var so it survives page navigation.
  const [localGap, setLocalGap] = useState<{ start: number; end: number | null } | null>(_persistedGap);
  const prevIsActiveRef = useRef(isActive);

  useEffect(() => {
    const wasActive = prevIsActiveRef.current;
    prevIsActiveRef.current = isActive;

    if (wasActive && !isActive) {
      const gap = { start: Date.now(), end: null };
      setLocalGap(gap);
      _persistedGap = gap;
    } else if (!wasActive && isActive) {
      setLocalGap(prev => {
        const sealed = prev ? { ...prev, end: Date.now() } : null;
        _persistedGap = sealed;
        return sealed;
      });
    }
  }, [isActive]);

  // Clear local gap once poll data includes a real "Tracking Stopped" block
  useEffect(() => {
    if (!localGap) return;
    // In uncontrolled mode: check IPC-polled activities (now includes "Tracking Stopped" sessions)
    // In controlled mode: check the passed-in activities from the parent
    const dataToCheck = isControlled ? activities : internalActivities;
    const hasRealBlock = dataToCheck.some(a => a.name === TRACKING_STOPPED_NAME);
    if (hasRealBlock) {
      setLocalGap(null);
      _persistedGap = null;
    }
  }, [internalActivities, activities, localGap, isControlled]);

  // Controlled mode: if tracking is stopped and no gap is set yet, infer gap start
  // from the last activity's end time. This handles the case where the app starts
  // with tracking already paused (no isActive transition detected during this session).
  useEffect(() => {
    if (!isControlled || !isViewingToday || isActive || localGap) return;
    if (activities.length === 0) return;
    const hasStopBlock = activities.some(a => a.name === TRACKING_STOPPED_NAME);
    if (hasStopBlock) return;
    // Find the last activity's end time as an approximation of when tracking stopped
    const lastEnd = activities.reduce((max, a) => {
      const t = new Date(a.endUtc).getTime();
      return t > max ? t : max;
    }, 0);
    if (!lastEnd || lastEnd >= Date.now()) return;
    const gap = { start: lastEnd, end: null };
    setLocalGap(gap);
    _persistedGap = gap;
  }, [isControlled, isViewingToday, isActive, localGap, activities]);

  const hiddenApps = useHiddenAppsStore(s => s.hiddenApps);

  // ── Task time entries ────────────────────────────────────────────────────────
  const [taskEntries, setTaskEntries] = useState<TaskEntryDto[]>([]);
  const [closingTaskTimer, setClosingTaskTimer] = useState(false);
  const taskEntriesTick = (isViewingToday && !userId) ? now : 0;

  useEffect(() => {
    const d = selectedDate ?? new Date();
    const dateStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    let fetchPromise: Promise<{ entries: TaskEntryDto[] }>;
    if (userId) {
      if (import.meta.env.DEV) {
        // eslint-disable-next-line no-console
        console.debug('[ActivitySection] fetching user task entries', { userId, dateStr });
      }
      fetchPromise = getUserTaskEntries(userId, dateStr);
    } else {
      fetchPromise = getMyTaskEntries(dateStr);
    }

    // Avoid stale task bars when switching users/dates (especially when team endpoint returns 403).
    // Do NOT clear during the "today + me" tick refresh to avoid visible flicker every 5s.
    if (userId || !isViewingToday) {
      setTaskEntries([]);
    }

    fetchPromise
      .then(res => setTaskEntries(res.entries ?? []))
      .catch(() => setTaskEntries([]));
  // Re-fetch on date change; when viewing today (own timeline), `now` ticks every 5s to keep an open entry fresh
  }, [selectedDate, userId, taskEntriesTick]);

  const taskBlocks = useMemo(() => {
    const dayEnd = dayStart + dayMs;
    const blocks: Array<TaskEntryDto & { left: number; width: number; key: string }> = [];

    for (const e of taskEntries) {
      const startedMs = new Date(e.startedAt).getTime();
      const rawEndMs =
        e.endedAt ? new Date(e.endedAt).getTime()
          : e.pausedAt ? new Date(e.pausedAt).getTime()
            : now;

      const start = Math.max(startedMs, dayStart);
      const end = Math.min(rawEndMs, dayEnd);
      if (!isFinite(start) || !isFinite(end) || end <= start) continue;

      const left = Math.max(0, Math.min(100, ((start - dayStart) / dayMs) * 100));
      const unclampedWidth = ((end - start) / dayMs) * 100;
      const width = Math.max(0.2, Math.min(100 - left, unclampedWidth));

      blocks.push({ ...e, left, width, key: e.id });
    }

    return blocks;
  }, [taskEntries, dayStart, dayMs, now]);

  const hasOpenTaskEntry = useMemo(() => taskEntries.some(e => !e.endedAt), [taskEntries]);
  const canStopTaskTimer = isViewingToday && !userId && hasOpenTaskEntry;

  const stopTaskTimer = useCallback(async () => {
    if (!canStopTaskTimer || closingTaskTimer) return;
    setClosingTaskTimer(true);
    try {
      await closeMyOpenTaskTimer();
    } finally {
      setClosingTaskTimer(false);
      // force-refresh task entries after closing
      const d = selectedDate ?? new Date();
      const dateStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
      getMyTaskEntries(dateStr).then(res => setTaskEntries(res.entries ?? [])).catch(() => {});
    }
  }, [canStopTaskTimer, closingTaskTimer, selectedDate]);

  // ── Fast path: REST API fetch on mount (no IPC dependency) ──────────────────
  // Fires immediately without waiting for the named pipe connection.
  // Converts backend session DTOs to the same ActivityBlock shape IPC returns.
  useEffect(() => {
    if (isControlled) return;
    const today = new Date().toISOString().split('T')[0];
    getDailyActivities(today).then(result => {
      if (!result?.sessions?.length && !result?.idlePeriods?.length) return;
      const hiddenSet = new Set(hiddenApps.map(a => a.toLowerCase()));
      const APP_PALETTE = [
        '#38bdf8','#f472b6','#34d399','#fb923c','#a78bfa',
        '#fbbf24','#22d3ee','#f87171','#4ade80','#e879f9',
      ];
      const colorMap = new Map<string, string>();
      let ci = 0;
      const blocks: ActivityBlock[] = [];
      for (const s of result.sessions) {
        if (hiddenSet.has(s.processName.toLowerCase())) continue;
        if (!colorMap.has(s.processName)) {
          colorMap.set(s.processName, APP_PALETTE[ci % APP_PALETTE.length]);
          ci++;
        }
        blocks.push({
          id: `${s.processName}-${s.startedAt}`,
          name: s.processName,
          startUtc: s.startedAt,
          endUtc: s.endedAt,
          duration: s.durationSeconds,
          productivity: s.appCategory ?? 'neutral',
          subcategory: s.appCategory ?? 'unknown',
          color: colorMap.get(s.processName) ?? '#94a3b8',
        });
      }
      for (const idlePeriod of result.idlePeriods ?? []) {
        blocks.push({
          id: idlePeriod.id,
          kind: 'idle',
          name: 'Idle',
          startUtc: idlePeriod.startedAt,
          endUtc: idlePeriod.endedAt,
          duration: idlePeriod.durationSeconds,
          productivity: 'idle',
          subcategory: 'idle',
          color: '#64748b',
          reasonCode: idlePeriod.reasonCode,
          note: idlePeriod.note,
          submittedAtUtc: idlePeriod.submittedAtUtc,
          tabs: [],
        });
      }
      blocks.sort((a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime());
      setInternalActivities(blocks);
    }).catch(() => { /* ignore — IPC will fill in once connected */ });
  // Only run once on mount; IPC polling below keeps it fresh
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isControlled]);

  // ── Evidence fetch (parallel to activities) ────────────────────────────────
  // When viewing today, re-fetch every 30s so new screenshots appear automatically.
  // Skipped entirely when hideEvidence=true (Colaborador role).
  const evidenceTick = (isViewingToday && !hideEvidence) ? Math.floor(now / 30000) : 0;
  useEffect(() => {
    if (hideEvidence) { setEvidenceItems([]); return; }
    const d = selectedDate ?? new Date();
    const dateStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const nextDay = new Date(d);
    nextDay.setDate(nextDay.getDate() + 1);
    const nextDayStr = `${nextDay.getFullYear()}-${String(nextDay.getMonth() + 1).padStart(2, '0')}-${String(nextDay.getDate()).padStart(2, '0')}`;

    getEvidenceByPeriod(dateStr, nextDayStr, 1, 200, userId)
      .then(res => setEvidenceItems(res.items ?? []))
      .catch(() => setEvidenceItems([]));
  }, [selectedDate, userId, evidenceTick, hideEvidence]);

  // ── Real-time path: IPC poll (once connected) ────────────────────────────────
  // The agent reads from local SQLite (most up-to-date, includes in-flight session).
  // Replaces REST data as soon as the named pipe is ready.
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

  // React immediately to state changes so Stop/Idle show up without waiting for the poll interval.
  useEffect(() => {
    if (isControlled || !isConnected) return;
    const unsubTracking = subscribeToEvent('trackingStateChanged', () => fetchActivities());
    const unsubIdle = subscribeToEvent('idleStateChanged', () => fetchActivities());
    return () => {
      unsubTracking();
      unsubIdle();
    };
  }, [isControlled, isConnected, subscribeToEvent, fetchActivities]);

  // Build display blocks — extends or injects a live "Tracking Stopped" block when paused
  const blocks = useMemo(() => {
    // Find the most recent "Tracking Stopped" start time so we only ever stretch
    // the current placeholder (not historical short pauses from the same day).
    const latestStoppedStart = activities.reduce((max, a) => {
      if (a.name !== TRACKING_STOPPED_NAME) return max;
      const t = new Date(a.startUtc).getTime();
      return t > max ? t : max;
    }, 0);

    const processed = activities.map(a => {
      const s = new Date(a.startUtc).getTime();
      let e = new Date(a.endUtc).getTime();
      let dur = a.duration;
      let color = a.color;

      if (a.name === TRACKING_STOPPED_NAME) {
        if (isViewingToday && isLocalViewer && !isActive && s === latestStoppedStart) {
          // Only stretch the most recent placeholder while the user is currently paused/stopped.
          // Historical short pauses (e.g. a 2-second pause from earlier) must NOT be
          // stretched — they are finalized records, not live placeholders.
          const rawDurationMs = e - s;
          if (rawDurationMs < 10000) {
            e = now;
            dur = Math.floor((e - s) / 1000);
          }
        }
        color = TRACKING_STOPPED_COLOR;
      }

      const left = Math.max(0, ((s - dayStart) / dayMs) * 100);
      const width = Math.max(0.15, ((e - s) / dayMs) * 100);
      return {
        ...a,
        color,
        duration: dur,
        endUtc: new Date(e).toISOString(),
        left: Math.min(left, 100),
        width: Math.min(width, 100 - left),
      };
    });

    // If we have a locally tracked gap and poll data hasn't delivered a real
    // "Tracking Stopped" block yet, inject a synthetic one so the timeline
    // never shows an empty gap — even across quick pause/resume cycles.
    if (isViewingToday && localGap) {
      const hasBlock = processed.some(b => b.name === TRACKING_STOPPED_NAME);
      if (!hasBlock) {
        const gapEnd = localGap.end ?? now;   // grows while paused, fixed after resume
        const gapStart = localGap.start;
        if (gapEnd > gapStart) {
          const left = Math.max(0, ((gapStart - dayStart) / dayMs) * 100);
          const width = Math.max(0.15, ((gapEnd - gapStart) / dayMs) * 100);
          processed.push({
            id: 'tracking-stopped-live',
            name: TRACKING_STOPPED_NAME,
            startUtc: new Date(gapStart).toISOString(),
            endUtc: new Date(gapEnd).toISOString(),
            duration: Math.floor((gapEnd - gapStart) / 1000),
            productivity: 'neutral',
            subcategory: 'system_event',
            color: TRACKING_STOPPED_COLOR,
            left: Math.min(left, 100),
            width: Math.min(width, 100 - left),
          });
        }
      }
    }

    return processed;
  }, [activities, dayStart, dayMs, isViewingToday, isLocalViewer, isActive, now, localGap]);

  // Map evidence items to blocks they fall within (capturedAt within block's start-end range)
  const blockEvidenceMap = useMemo(() => {
    const map = new Map<string, EvidenceItem[]>();
    for (const ev of evidenceItems) {
      const evTime = new Date(ev.capturedAt).getTime();
      for (const block of blocks) {
        const bs = new Date(block.startUtc).getTime();
        const be = new Date(block.endUtc).getTime();
        if (evTime >= bs && evTime <= be) {
          const existing = map.get(block.id) ?? [];
          existing.push(ev);
          map.set(block.id, existing);
          break;
        }
      }
    }
    return map;
  }, [evidenceItems, blocks]);

  // Apply evidence/domain filter
  const filteredBlocks = useMemo(() => {
    if (evidenceFilter === 'all') return blocks;
    return blocks.filter(block => {
      if (block.name === TRACKING_STOPPED_NAME) return true;
      if (evidenceFilter === 'withEvidence') {
        return (blockEvidenceMap.get(block.id)?.length ?? 0) > 0;
      }
      if (evidenceFilter === 'byDomain') {
        return !!block.domain;
      }
      return true;
    });
  }, [blocks, evidenceFilter, blockEvidenceMap]);

  const evidenceCount = useMemo(() => {
    return evidenceItems.length;
  }, [evidenceItems]);

  const hourLabels = [0, 3, 6, 9, 12, 15, 18, 21, 24];

  // "Now" pin — recalculated on every tick
  const nowPct = useMemo(() => {
    if (!isViewingToday) return -1;
    return Math.min(100, Math.max(0, ((now - dayStart) / dayMs) * 100));
  }, [dayStart, dayMs, isViewingToday, now]);

  const handleMouseEnter = useCallback((block: ActivityBlock, e: React.MouseEvent) => {
    setHovered({ block, rect: e.currentTarget.getBoundingClientRect() });
  }, []);

  const handleMouseLeave = useCallback(() => setHovered(null), []);

  // Show the timeline even when only a "Tracking Stopped" live block exists
  const hasBlocks = filteredBlocks.length > 0;

  return (
    <>
      <div>
        <Card className={cardBase}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              {/* Left: Collapse + Title */}
              <button
                onClick={() => setIsCollapsed(!isCollapsed)}
                className="flex items-center gap-1.5 hover:opacity-80 transition-opacity"
              >
                <div
                  className="transition-transform duration-200"
                  style={{ transform: isCollapsed ? 'rotate(-90deg)' : 'rotate(0deg)' }}
                >
                  <ChevronDown className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
                </div>
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{t('dashboard.activity')}</span>
              </button>

              {/* Right: Date tabs + nav arrows + indicators */}
              <div className="flex items-center gap-2">
                {/* Date filter tabs */}
                <div className="flex items-center gap-0 bg-[rgba(255,255,255,0.03)] rounded-lg border border-[rgba(255,255,255,0.06)] p-0.5">
                  {(['today', 'yesterday', '7days'] as DateRange[]).map((range) => {
                    const labels: Record<DateRange, string> = { today: t('dashboard.todayTab'), yesterday: t('dashboard.yesterdayTab'), '7days': t('dashboard.sevenDaysTab') };
                    const isActive = dateRange === range;
                    return (
                      <button
                        key={range}
                        onClick={() => setDateRange(range)}
                        className={`px-2 py-0.5 rounded-md text-[9px] font-medium transition-all ${
                          isActive
                            ? 'bg-[rgba(139,92,246,0.12)] text-[#8B5CF6]'
                            : 'text-[rgba(245,247,251,0.35)] hover:text-[rgba(245,247,251,0.6)]'
                        }`}
                      >
                        {labels[range]}
                      </button>
                    );
                  })}
                </div>

                {/* Nav arrows */}
                <div className="flex items-center gap-0.5">
                  <button className="w-5 h-5 flex items-center justify-center rounded text-[rgba(245,247,251,0.3)] hover:text-[rgba(245,247,251,0.6)] transition-colors">
                    <ChevronLeft className="w-3 h-3" />
                  </button>
                  <button className="w-5 h-5 flex items-center justify-center rounded text-[rgba(245,247,251,0.3)] hover:text-[rgba(245,247,251,0.6)] transition-colors">
                    <ChevronRight className="w-3 h-3" />
                  </button>
                </div>

                {/* Metric badges */}
                <div className="flex items-center gap-1.5">
                  <span className="text-[9px] px-1.5 py-0.5 rounded bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.4)] font-mono">
                    {filteredBlocks.filter(b => b.name !== TRACKING_STOPPED_NAME).length}
                  </span>
                  {!hideEvidence && evidenceCount > 0 && (
                    <span className="flex items-center gap-0.5 text-[9px] px-1.5 py-0.5 rounded bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)] text-[#a78bfa] font-mono">
                      <Camera className="w-2.5 h-2.5" />
                      {evidenceCount}
                    </span>
                  )}
                </div>

                <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
              </div>
            </CardTitle>
          </CardHeader>

          {!isCollapsed && (
            <CardContent className="pt-3 pb-3 px-4">
              {!hasBlocks ? (
                <div className="text-center py-6">
                  <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{t('dashboard.noActivity')}</p>
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

                  {/* Timeline bar — apps */}
                  <div className="relative h-[28px] rounded-md bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)]">
                    {hourLabels.slice(1, -1).map((h) => (
                      <div key={h} className="absolute top-0 bottom-0 w-px bg-[rgba(255,255,255,0.03)]" style={{ left: `${(h / TOTAL_HOURS) * 100}%` }} />
                    ))}
                    {filteredBlocks.map((block, i) => {
                      const blockEvidence = blockEvidenceMap.get(block.id);
                      const hasEvidence = (blockEvidence?.length ?? 0) > 0;
                      return (
                        <div
                          key={block.id || i}
                          style={{
                            left: `${block.left}%`,
                            width: `${block.width}%`,
                            backgroundColor: block.color,
                            minWidth: '1px',
                          }}
                          className={`absolute top-[2px] bottom-[2px] rounded-[3px] cursor-pointer hover:brightness-125 ${
                            block.name === TRACKING_STOPPED_NAME ? 'opacity-60' : ''
                          }`}
                          onMouseEnter={(e) => handleMouseEnter(block, e)}
                          onMouseLeave={handleMouseLeave}
                          onClick={() => {
                            if (!hideEvidence && hasEvidence && blockEvidence) {
                              setEvidenceModalIndex(0);
                            }
                          }}
                        >
                          {!hideEvidence && hasEvidence && (
                            <Camera className="absolute top-0 right-0 w-2.5 h-2.5 text-white opacity-80 pointer-events-none" />
                          )}
                        </div>
                      );
                    })}
                    {nowPct >= 0 && (
                      <div className="absolute top-0 bottom-0 w-px bg-[rgba(245,247,251,0.4)]" style={{ left: `${nowPct}%` }}>
                        <div className="absolute -top-[3px] left-1/2 -translate-x-1/2 w-[5px] h-[5px] rounded-full bg-[rgba(245,247,251,0.6)]" />
                      </div>
                    )}
                  </div>

                  {/* Evidence filter tabs */}
                  {!hideEvidence && evidenceItems.length > 0 && (
                    <div className="flex items-center gap-1 mt-2">
                      {(['all', 'withEvidence', 'byDomain'] as EvidenceFilter[]).map((filter) => {
                        const labels: Record<EvidenceFilter, string> = {
                          all: t('evidence.filterAll'),
                          withEvidence: t('evidence.filterWithEvidence'),
                          byDomain: t('evidence.filterByDomain'),
                        };
                        const isActive = evidenceFilter === filter;
                        return (
                          <button
                            key={filter}
                            onClick={() => setEvidenceFilter(filter)}
                            className={`px-2 py-0.5 rounded-md text-[9px] font-medium transition-all ${
                              isActive
                                ? 'bg-[rgba(139,92,246,0.12)] text-[#8B5CF6]'
                                : 'text-[rgba(245,247,251,0.35)] hover:text-[rgba(245,247,251,0.6)] bg-[rgba(255,255,255,0.02)]'
                            }`}
                          >
                            {labels[filter]}
                          </button>
                        );
                      })}
                    </div>
                  )}

                  {/* Screenshot thumbnail strip — hidden for Colaborador role */}
                  {!hideEvidence && (
                    <ScreenshotStrip
                      evidenceItems={evidenceItems}
                      onThumbnailClick={(idx) => setEvidenceModalIndex(idx)}
                    />
                  )}

                  {/* Task timeline — thin row showing task work periods */}
                  {(taskBlocks.length > 0 || canStopTaskTimer) && (
                    <div className="mt-1.5">
                      <div className="flex items-center justify-between gap-2 mb-0.5">
                        <span className="text-[8px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.3)]">{t('dashboard.tasksTab')}</span>
                        {canStopTaskTimer && (
                          <button
                            type="button"
                            onClick={stopTaskTimer}
                            disabled={closingTaskTimer}
                            className="text-[9px] font-semibold text-[#fbbf24] hover:text-[#ffd66e] disabled:opacity-50"
                            title="Stop stuck task timer"
                          >
                            {closingTaskTimer ? 'Stopping…' : t('dashboard.stop')}
                          </button>
                        )}
                      </div>
                      <div className="relative h-[10px] rounded-sm bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)]">
                        {taskBlocks.map((block) => (
                          <div
                            key={block.key}
                            style={{
                              left: `${block.left}%`,
                              width: `${block.width}%`,
                              backgroundColor: block.projectColor,
                              minWidth: '2px',
                            }}
                            className="absolute top-[1px] bottom-[1px] rounded-[2px] opacity-80 hover:opacity-100 cursor-default"
                            title={`${block.taskTitle} · ${block.projectName}`}
                          />
                        ))}
                        {nowPct >= 0 && (
                          <div className="absolute top-0 bottom-0 w-px bg-[rgba(245,247,251,0.3)]" style={{ left: `${nowPct}%` }} />
                        )}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          )}
        </Card>
      </div>

      {/* Tooltip rendered via portal */}
      {hovered && createPortal(
        <ActivityTooltip block={hovered.block} anchorRect={hovered.rect} blockEvidence={blockEvidenceMap.get(hovered.block.id)} />,
        document.body
      )}

      {/* Evidence modal */}
      {!hideEvidence && evidenceModalIndex !== null && evidenceItems.length > 0 && (
        <EvidenceModal
          items={evidenceItems}
          initialIndex={Math.min(evidenceModalIndex, evidenceItems.length - 1)}
          onClose={() => setEvidenceModalIndex(null)}
        />
      )}
    </>
  );
}

function ActivityTooltip({ block, anchorRect, blockEvidence }: { block: ActivityBlock; anchorRect: DOMRect; blockEvidence?: EvidenceItem[] }) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ left: 0, top: 0 });

  useEffect(() => {
    if (!ref.current) return;
    const tw = ref.current.offsetWidth;
    const th = ref.current.offsetHeight;

    let left = anchorRect.left + anchorRect.width / 2 - tw / 2;
    let top = anchorRect.top - th - 10;

    if (left < 8) left = 8;
    if (left + tw > window.innerWidth - 8) left = window.innerWidth - 8 - tw;
    if (top < 8) top = anchorRect.bottom + 8;

    setPos({ left, top });
  }, [anchorRect]);

  return (
    <div
      ref={ref}
      className="pointer-events-none"
      style={{ position: 'fixed', zIndex: 99999, left: pos.left, top: pos.top }}
    >
      <div className="px-3 py-2.5 rounded-lg bg-[#13151f] border border-[rgba(255,255,255,0.12)] shadow-[0_8px_30px_rgba(0,0,0,0.5)] max-w-[260px]">
        <div className="flex items-center gap-2 mb-1.5">
          <div className="w-2.5 h-2.5 rounded-sm flex-shrink-0" style={{ backgroundColor: block.color }} />
          <p className="text-[11px] font-semibold text-[#f5f7fb] truncate">{block.name}</p>
        </div>
        <p className="text-[10px] text-[rgba(245,247,251,0.5)] mb-2">
          {fmtTime(block.startUtc)} – {fmtTime(block.endUtc)}
          <span className="text-[rgba(245,247,251,0.7)] font-medium ml-1.5">{fmtDuration(block.duration)}</span>
        </p>
        {(blockEvidence && blockEvidence.length > 0) && (
          <div className="flex items-center gap-1 mb-1.5">
            <Camera className="w-2.5 h-2.5 text-[#a78bfa]" />
            <span className="text-[9px] text-[#a78bfa]">{blockEvidence.length} {blockEvidence.length === 1 ? t('evidence.screenshot') : t('evidence.screenshots')}</span>
          </div>
        )}
        {block.domain && (
          <div className="mb-1.5">
            <span className="text-[9px] px-1.5 py-0.5 rounded bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)]">
              {block.domain}
            </span>
          </div>
        )}
        {(block.reasonCode || block.note) && (
          <div className="border-t border-[rgba(255,255,255,0.08)] pt-1.5 mb-2 space-y-1">
            {block.reasonCode && (
              <p className="text-[9px] text-[rgba(245,247,251,0.72)]">
                Reason: {block.reasonCode.split('_').join(' ')}
              </p>
            )}
            {block.note && (
              <p className="text-[9px] text-[rgba(245,247,251,0.55)] leading-relaxed">
                {block.note}
              </p>
            )}
          </div>
        )}
        {block.tabs && block.tabs.length > 0 && (
          <div className="border-t border-[rgba(255,255,255,0.08)] pt-1.5 space-y-[5px]">
            <p className="text-[8px] uppercase tracking-wider text-[rgba(245,247,251,0.25)] mb-1">{t('dashboard.activitiesTab')}</p>
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
