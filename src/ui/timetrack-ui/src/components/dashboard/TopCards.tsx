/**
 * TopCards - Dashboard top cards component
 *
 * Displays three summary cards:
 * 1. Tempo Rastreado - Time tracked today with circular progress + VS ONTEM
 * 2. Foco - Focus score with distractions/pauses summary
 * 3. Timer - Status card with project/tags and controls
 *
 * Plus a 4th "Resumo" card replacing the old Equipe agora:
 * Shows idle time, focus sessions, distractions, focus score
 */

import { useEffect, useRef } from 'react';
import { MoreVertical, Heart, Clock, Target, AlertTriangle, Zap } from 'lucide-react';
import { motion } from 'motion/react';
import { animate } from 'animejs';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../../types/ipc';
import { TimerFocusCard } from './TimerFocusCard';
import { useTimerStore, selectCurrentUserSessions } from '../../stores/timerStore';
import { fadeUp, staggerContainer, STAGGER, SPRING, TIMING_MS } from '../../lib/animation';
import { useAnimatedCounter } from '../../hooks/useAnimatedCounter';
import { cardBase } from './shared/styles';

interface TopCardsProps {
  summary: TodaySummaryResponse | null;
  isPaused?: boolean;
  isTracking?: boolean;
  focusModePolicy?: unknown;
  onStartTracking?: () => void;
  onPauseTracking?: () => void;
  onStopTracking?: () => void;
  isTeamTab?: boolean;
  weeklyHistory?: WeeklyHistoryItem[];
}

export function TopCards({
  summary,
  isPaused: _isPaused,
  isTracking: _isTracking,
  focusModePolicy: _focusModePolicy,
  onStartTracking: _onStartTracking,
  onPauseTracking: _onPauseTracking,
  onStopTracking: _onStopTracking,
  isTeamTab = false,
  weeklyHistory = [],
}: TopCardsProps) {
  const totalSeconds = summary?.totalDuration ?? 0;
  const idleSeconds = summary?.idleTime ?? 0;

  const categories = summary?.categories ?? [];

  const productiveSecs = categories
    .filter(c => c.productivity === 'productive')
    .reduce((sum, c) => sum + c.duration, 0);
  const distractionSecs = categories
    .filter(c => c.productivity === 'distraction')
    .reduce((sum, c) => sum + c.duration, 0);
  const neutralSecs = Math.max(0, totalSeconds - productiveSecs - distractionSecs);

  const distractionCount = categories.filter(c => c.productivity === 'distraction').length;

  // Focus sessions from timer store (pomodoro/ultradian)
  const timerSessions = useTimerStore(selectCurrentUserSessions);
  const focusSessions = timerSessions.filter(s => s.phase === 'focus');
  const focusSessionCount = focusSessions.length;
  const scoredSessions = focusSessions.filter(s => s.productivity >= 0);
  const focusScoreAvg = scoredSessions.length > 0
    ? Math.round(scoredSessions.reduce((sum, s) => sum + s.productivity, 0) / scoredSessions.length)
    : 0;

  const productivityScore = totalSeconds > 0
    ? Math.round((productiveSecs / totalSeconds) * 100)
    : 0;

  const getScoreColor = (score: number): string => {
    if (score >= 80) return '#05df72';
    if (score >= 60) return '#4ade80';
    if (score >= 40) return '#fbbf24';
    if (score >= 20) return '#fb923c';
    return '#f87171';
  };
  const scoreColor = getScoreColor(productivityScore);

  const ringRadius = 42;
  const circumference = 2 * Math.PI * ringRadius;
  const sortedCategories = [...categories].sort((a, b) => b.duration - a.duration);

  let accumulatedOffset = 0;
  const ringSegments = sortedCategories.map((cat) => {
    const fraction = totalSeconds > 0 ? cat.duration / totalSeconds : 0;
    const arcLen = fraction * circumference;
    const gap = sortedCategories.length > 1 ? 2 : 0;
    const segment = {
      color: cat.color,
      dasharray: `${Math.max(0, arcLen - gap)} ${circumference - Math.max(0, arcLen - gap)}`,
      offset: -accumulatedOffset,
      name: cat.name,
      pct: Math.round(fraction * 100),
    };
    accumulatedOffset += arcLen;
    return segment;
  });

  // VS ONTEM comparison
  const todayIdx = weeklyHistory.findIndex(w => w.isToday);
  const yesterdayEntry = todayIdx > 0 ? weeklyHistory[todayIdx - 1] : null;
  const yesterdaySeconds = yesterdayEntry ? yesterdayEntry.hours * 3600 : 0;
  const deltaSeconds = totalSeconds - yesterdaySeconds;
  const deltaPositive = deltaSeconds >= 0;
  const deltaText = deltaSeconds !== 0
    ? `${deltaPositive ? '+' : '-'}${formatDuration(Math.abs(deltaSeconds))}`
    : '—';

  // Animated counters
  const animatedScore = useAnimatedCounter(productivityScore);

  // SVG ring animation for productivity segments
  const segmentRefs = useRef<(SVGCircleElement | null)[]>([]);

  useEffect(() => {
    segmentRefs.current.forEach((el, i) => {
      if (!el) return;
      const seg = ringSegments[i];
      if (!seg) return;
      animate(el, {
        strokeDasharray: [`0 ${circumference}`, seg.dasharray],
        strokeDashoffset: [0, seg.offset],
        duration: TIMING_MS.ring,
        ease: 'inOutQuart',
        delay: i * 100,
      });
    });
  }, [ringSegments.length, totalSeconds]);

  // Grid: 3-col on both tabs (Meu dia: Tempo + Produtividade + Timer; Team: Tempo + Produtividade + Resumo)
  const gridCols = 'grid-cols-3';

  return (
    <motion.div
      variants={staggerContainer(STAGGER.cards)}
      initial="hidden"
      animate="visible"
      className={`grid ${gridCols} gap-4 flex-shrink-0`}
    >
      {/* ─── Tempo Rastreado Card ─── */}
      <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Tempo rastreado</span>
              <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-3 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="8" />
                  <circle
                    cx="50" cy="50" r="42" fill="none"
                    stroke="url(#gradient1)" strokeWidth="8"
                    strokeDasharray="264 264"
                    strokeLinecap="round"
                    opacity="0.3"
                  />
                  <defs>
                    <linearGradient id="gradient1" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor="#8B5CF6" />
                      <stop offset="100%" stopColor="#22D3EE" />
                    </linearGradient>
                  </defs>
                </svg>
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-[20px] font-semibold text-[#f5f7fb]">{formatDuration(totalSeconds)}</span>
                </div>
              </div>

              <div className="mt-3 text-center">
                {/* Idle time label */}
                <div className="flex items-center justify-center gap-1">
                  <Clock className="w-3 h-3 text-[rgba(245,247,251,0.35)]" />
                  <span className="text-[10px] text-[rgba(245,247,251,0.45)]">Ocioso: {formatDuration(idleSeconds)}</span>
                </div>

                {/* VS ONTEM comparison */}
                <div className="flex items-center justify-center gap-1.5 mt-1">
                  <Heart className="w-3 h-3" style={{ color: deltaPositive ? '#f87171' : 'rgba(245,247,251,0.3)' }} />
                  <span
                    className="text-[10px] font-medium"
                    style={{ color: deltaPositive ? '#4ade80' : '#f87171' }}
                  >
                    {deltaText}
                  </span>
                  <span className="text-[8px] uppercase tracking-wider text-[rgba(245,247,251,0.35)]">VS ONTEM</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* ─── Foco Card (Score + Produtivo/Neutro/Distração) ─── */}
      <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Foco</span>
              <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r={ringRadius} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="9" />
                  {ringSegments.map((seg, i) => (
                    <circle
                      key={i}
                      ref={(el) => { segmentRefs.current[i] = el; }}
                      cx="50" cy="50" r={ringRadius}
                      fill="none"
                      stroke={seg.color}
                      strokeWidth="9"
                      strokeDasharray={`0 ${circumference}`}
                      strokeDashoffset={0}
                      strokeLinecap="butt"
                    />
                  ))}
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-[26px] font-bold" style={{ color: scoreColor }}>{animatedScore}</span>
                  <span className="text-[8px] text-[rgba(245,247,251,0.4)] -mt-0.5">SCORE</span>
                </div>
              </div>

              {/* Produtivo / Neutro / Distração breakdown */}
              <div className="mt-2.5 w-full space-y-1">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5">
                    <div className="w-2 h-2 rounded-full bg-[#4ade80]" />
                    <span className="text-[10px] text-[rgba(245,247,251,0.6)]">Produtivo</span>
                  </div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(productiveSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5">
                    <div className="w-2 h-2 rounded-full bg-[#fbbf24]" />
                    <span className="text-[10px] text-[rgba(245,247,251,0.6)]">Neutro</span>
                  </div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(neutralSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5">
                    <div className="w-2 h-2 rounded-full bg-[#f87171]" />
                    <span className="text-[10px] text-[rgba(245,247,251,0.6)]">Distração</span>
                  </div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(distractionSecs)}</span>
                </div>
              </div>

              {/* Top category dots */}
              <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 justify-center">
                {sortedCategories.slice(0, 3).map((cat, i) => (
                  <div key={i} className="flex items-center gap-1">
                    <div className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: cat.color }} />
                    <span className="text-[8px] text-[rgba(245,247,251,0.4)]">{cat.name} {Math.round(cat.percentage)}%</span>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* ─── Timer Card (Meu dia only) ─── */}
      {!isTeamTab && (
        <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }}>
          <TimerFocusCard summary={summary} />
        </motion.div>
      )}

      {/* ─── Resumo Card (Teams tab only) ─── */}
      {isTeamTab && (
        <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
          <Card className={`${cardBase} h-full`}>
            <CardHeader className="pb-0 pt-3 px-4">
              <CardTitle className="flex items-center justify-between">
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Resumo</span>
                <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-3 pb-3 px-4">
              <div className="space-y-3">
                {/* Idle time */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-5 h-5 rounded-md bg-[rgba(161,161,170,0.1)] border border-[rgba(161,161,170,0.2)] flex items-center justify-center">
                      <Clock className="w-3 h-3 text-[rgba(245,247,251,0.5)]" />
                    </div>
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Tempo ocioso</span>
                  </div>
                  <span className="text-[14px] font-bold text-[rgba(245,247,251,0.6)]">{formatDuration(idleSeconds)}</span>
                </div>

                {/* Focus sessions */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-5 h-5 rounded-md bg-[rgba(5,223,114,0.1)] border border-[rgba(5,223,114,0.2)] flex items-center justify-center">
                      <Target className="w-3 h-3 text-[#05df72]" />
                    </div>
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Sessões de foco</span>
                  </div>
                  <span className="text-[14px] font-bold text-[#f5f7fb]">{focusSessionCount}</span>
                </div>

                {/* Distractions */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-5 h-5 rounded-md bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.2)] flex items-center justify-center">
                      <AlertTriangle className="w-3 h-3 text-[#f87171]" />
                    </div>
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Distrações</span>
                  </div>
                  <span className="text-[14px] font-bold text-[#f5f7fb]">{distractionCount}</span>
                </div>

                {/* Focus score */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-5 h-5 rounded-md bg-[rgba(139,92,246,0.1)] border border-[rgba(139,92,246,0.2)] flex items-center justify-center">
                      <Zap className="w-3 h-3 text-[#8B5CF6]" />
                    </div>
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Focus Score</span>
                  </div>
                  <span className="text-[14px] font-bold" style={{ color: getScoreColor(focusScoreAvg) }}>
                    {focusScoreAvg}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>
      )}
    </motion.div>
  );
}
