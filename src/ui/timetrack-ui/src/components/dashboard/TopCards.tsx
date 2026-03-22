/**
 * TopCards - Dashboard top cards component
 *
 * Displays three summary cards:
 * 1. Tempo Rastreado - Time tracked today with circular progress
 * 2. Foco - Focus percentage and sessions
 * 3. Timer - Timer card with optional Focus Mode support (CX-139)
 */

import { useEffect, useRef } from 'react';
import { MoreVertical } from 'lucide-react';
import { motion } from 'motion/react';
import { animate } from 'animejs';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse } from '../../types/ipc';
import { TimerFocusCard } from './TimerFocusCard';
import { fadeUp, staggerContainer, STAGGER, SPRING, TIMING_MS } from '../../lib/animation';
import { useAnimatedCounter } from '../../hooks/useAnimatedCounter';

interface TopCardsProps {
  summary: TodaySummaryResponse | null;
  isPaused?: boolean;
  isTracking?: boolean;
  focusModePolicy?: unknown;
  onStartTracking?: () => void;
  onPauseTracking?: () => void;
  onStopTracking?: () => void;
  isTeamTab?: boolean;
}

const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl hover:border-[rgba(74,217,255,0.15)] hover:shadow-[0_0_0_1px_rgba(74,217,255,0.08),0_4px_20px_rgba(74,217,255,0.04)] transition-all duration-200";

export function TopCards({
  summary,
  isPaused: _isPaused,
  isTracking: _isTracking,
  focusModePolicy: _focusModePolicy,
  onStartTracking: _onStartTracking,
  onPauseTracking: _onPauseTracking,
  onStopTracking: _onStopTracking,
  isTeamTab = false,
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

  const progressPercentage = Math.min((totalSeconds / 28800) * 100, 100);

  // Animated counters
  const animatedScore = useAnimatedCounter(productivityScore);
  const animatedProgress = useAnimatedCounter(Math.round(progressPercentage));

  // SVG ring animation for time tracked
  const timeRingRef = useRef<SVGCircleElement>(null);
  const prevProgressRef = useRef(0);

  useEffect(() => {
    if (!timeRingRef.current) return;
    const targetDash = progressPercentage * 2.64;
    animate(timeRingRef.current, {
      strokeDasharray: [`${prevProgressRef.current * 2.64} 264`, `${targetDash} 264`],
      duration: TIMING_MS.ring,
      ease: 'inOutQuart',
    });
    prevProgressRef.current = progressPercentage;
  }, [progressPercentage]);

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

  return (
    <motion.div
      variants={staggerContainer(STAGGER.cards)}
      initial="hidden"
      animate="visible"
      className={`grid ${isTeamTab ? 'grid-cols-2' : 'grid-cols-3'} gap-4 flex-shrink-0`}
    >
      {/* Tempo Rastreado Card */}
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
                    ref={timeRingRef}
                    cx="50" cy="50" r="42" fill="none"
                    stroke="url(#gradient1)" strokeWidth="8"
                    strokeDasharray={`0 264`}
                    strokeLinecap="round"
                  />
                  <defs>
                    <linearGradient id="gradient1" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor="#4ad9ff" />
                      <stop offset="100%" stopColor="#3c7bff" />
                    </linearGradient>
                  </defs>
                </svg>
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-[20px] font-semibold text-[#f5f7fb]">{formatDuration(totalSeconds)}</span>
                </div>
              </div>
              <div className="mt-3 text-center">
                <p className="text-[18px] font-semibold text-[rgba(245,247,251,0.9)]">{animatedProgress}%</p>
                <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-0.5">Meta de hoje</p>
                <p className="text-[10px] text-[rgba(245,247,251,0.4)] mt-1">
                  Idle: <span className="text-[rgba(245,247,251,0.6)] font-medium">{formatDuration(idleSeconds)}</span>
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Produtividade Card */}
      <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Produtividade</span>
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

      {/* Pomodoro / Ultradian Timer Card */}
      {!isTeamTab && (
        <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }}>
          <TimerFocusCard summary={summary} />
        </motion.div>
      )}
    </motion.div>
  );
}
