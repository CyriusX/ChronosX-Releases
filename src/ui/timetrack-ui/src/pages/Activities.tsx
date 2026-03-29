/**
 * Activities Page — Historical view of tracked time with date navigation
 *
 * Mirrors the Dashboard layout but lets the user browse any date.
 * Includes: day insights, tracked time ring, productivity ring,
 * activity timeline, productivity heatmap, categories/apps/projects,
 * session list, and a mini calendar in the right panel.
 */

import { motion } from 'motion/react';
import { Sidebar } from '../components/dashboard';
import { BottomCards, ActivitySection } from '../components/dashboard';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { formatDuration } from '../lib/utils';
import { useActivitiesData } from '../hooks/useActivitiesData';
import {
  DateNavigator,
  DayInsights,
  ProductivityHeatmap,
  MiniCalendar,
  SessionList,
} from '../components/activities';
import { AppIcon } from '../components/dashboard/shared';
import { useTimerStore, selectCurrentUserSessions } from '../stores/timerStore';
import { fadeUp, staggerContainer, STAGGER } from '../lib/animation';

// ============================================================================
// CONSTANTS
// ============================================================================

import { cardBase } from '../components/dashboard/shared/styles';

// ============================================================================
// PAGE
// ============================================================================

export default function Activities() {
  const data = useActivitiesData();
  const { summary, activities, isLoading } = data;

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden">
      <Sidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header with DateNavigator */}
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-[14px] font-medium text-[#f5f7fb]">Atividades</span>
              {isLoading && (
                <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
              )}
            </div>
            <DateNavigator
              selectedDate={data.selectedDate}
              isToday={data.isToday}
              onPrevDay={data.goToPrevDay}
              onNextDay={data.goToNextDay}
              onToday={data.goToToday}
              onDateSelect={data.setSelectedDate}
            />
          </div>
        </div>

        <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0">
          {/* Left Content — scrollable */}
          <div className="flex-1 flex flex-col gap-4 overflow-y-auto min-w-0 pr-1">
            {/* Day Insights */}
            <DayInsights
              summary={summary}
              comparisonText={data.comparisonText}
              productivityComparison={data.productivityComparison}
              isToday={data.isToday}
            />

            {/* Top Cards: Tempo Rastreado + Produtividade + Resumo */}
            <ActivitiesTopCards summary={summary} />

            {/* Activity Timeline */}
            <ActivitySection
              activities={activities}
              selectedDate={data.selectedDate}
            />

            {/* Productivity Heatmap */}
            <ProductivityHeatmap activities={activities} selectedDate={data.selectedDate} />

            {/* Categories, Apps & Sites, Projects */}
            <BottomCards summary={summary} />

            {/* Session List */}
            <SessionList activities={activities} />
          </div>

          {/* Right Panel */}
          <div className="w-[280px] flex-shrink-0 flex flex-col gap-4 overflow-y-auto">
            {/* Mini Calendar */}
            <MiniCalendar
              selectedDate={data.selectedDate}
              onDateSelect={data.setSelectedDate}
              weeklyHistory={data.weeklyHistory}
            />

            {/* Top Apps for the day */}
            <TopAppsPanel summary={summary} />
          </div>
        </div>
      </main>
    </div>
  );
}

// ============================================================================
// TOP CARDS (Tempo Rastreado + Produtividade + Resumo do dia)
// ============================================================================

function ActivitiesTopCards({ summary }: { summary: ReturnType<typeof useActivitiesData>['summary'] }) {
  const totalSeconds = summary?.totalDuration ?? 0;
  const idleSeconds = summary?.idleTime ?? 0;

  // Focus Score = average from Pomodoro/Ultradian focus sessions (timer store)
  const timerSessions = useTimerStore(selectCurrentUserSessions);
  const scoredFocusSessions = timerSessions.filter(s => s.phase === 'focus' && s.productivity >= 0);
  const focusSessionScore = scoredFocusSessions.length > 0
    ? Math.round(scoredFocusSessions.reduce((sum, s) => sum + s.productivity, 0) / scoredFocusSessions.length)
    : 0;
  const focusSessionCount = timerSessions.filter(s => s.phase === 'focus').length;

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

  const scoreColor =
    productivityScore >= 80 ? '#05df72'
    : productivityScore >= 60 ? '#4ade80'
    : productivityScore >= 40 ? '#fbbf24'
    : productivityScore >= 20 ? '#fb923c'
    : '#f87171';

  // Ring chart segments
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
    };
    accumulatedOffset += arcLen;
    return segment;
  });

  const progressPercentage = Math.min((totalSeconds / 28800) * 100, 100);

  return (
    <motion.div
      className="grid grid-cols-3 gap-4 flex-shrink-0"
      variants={staggerContainer(STAGGER.cards)}
      initial="hidden"
      animate="visible"
    >
      {/* Tempo Rastreado */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              Tempo rastreado
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-3 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="8" />
                  <circle cx="50" cy="50" r="42" fill="none" stroke="url(#actGrad1)" strokeWidth="8" strokeDasharray={`${progressPercentage * 2.64} 264`} strokeLinecap="round" />
                  <defs>
                    <linearGradient id="actGrad1" x1="0%" y1="0%" x2="100%" y2="100%">
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
                <p className="text-[18px] font-semibold text-[rgba(245,247,251,0.9)]">{Math.round(progressPercentage)}%</p>
                <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-0.5">Meta de 8h</p>
                <p className="text-[10px] text-[rgba(245,247,251,0.4)] mt-1">
                  Idle: <span className="text-[rgba(245,247,251,0.6)] font-medium">{formatDuration(idleSeconds)}</span>
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Foco */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              Foco
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r={ringRadius} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="9" />
                  {ringSegments.map((seg, i) => (
                    <circle key={i} cx="50" cy="50" r={ringRadius} fill="none" stroke={seg.color} strokeWidth="9"
                      strokeDasharray={seg.dasharray} strokeDashoffset={seg.offset} strokeLinecap="butt" />
                  ))}
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-[26px] font-bold" style={{ color: scoreColor }}>{productivityScore}</span>
                  <span className="text-[8px] text-[rgba(245,247,251,0.4)] -mt-0.5">SCORE</span>
                </div>
              </div>
              <div className="mt-2.5 w-full space-y-1">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#4ade80]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">Produtivo</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(productiveSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#fbbf24]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">Neutro</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(neutralSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#f87171]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">Distração</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(distractionSecs)}</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Resumo do dia */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              Resumo do dia
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-4 pb-3 px-4">
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Tempo produtivo</span>
                <span className="text-[16px] font-bold text-[#4ade80]">{formatDuration(productiveSecs)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Tempo ocioso</span>
                <span className="text-[16px] font-bold text-[rgba(245,247,251,0.5)]">{formatDuration(idleSeconds)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Focus Score</span>
                <div className="flex items-center gap-1.5">
                  <span className="text-[16px] font-bold" style={{
                    color: focusSessionScore >= 80 ? '#4ade80' : focusSessionScore >= 50 ? '#fbbf24' : '#f87171'
                  }}>{focusSessionScore}</span>
                  <span className="text-[9px] text-[rgba(245,247,251,0.3)]">
                    ({focusSessionCount} {focusSessionCount === 1 ? 'sessao' : 'sessoes'})
                  </span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </motion.div>
  );
}

// ============================================================================
// TOP APPS PANEL (right side)
// ============================================================================

function TopAppsPanel({ summary }: { summary: ReturnType<typeof useActivitiesData>['summary'] }) {
  const apps = [...(summary?.topApplications ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 8);

  const totalTime = apps.reduce((sum, a) => sum + a.duration, 0);

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
          Top Apps
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-3 px-4">
        {apps.length === 0 ? (
          <p className="text-[11px] text-[rgba(245,247,251,0.3)] text-center py-3">Nenhum app</p>
        ) : (
          <motion.div
            className="space-y-2"
            variants={staggerContainer(STAGGER.listItems)}
            initial="hidden"
            animate="visible"
          >
            {apps.map((app, i) => {
              const pct = totalTime > 0 ? Math.round((app.duration / totalTime) * 100) : 0;
              return (
                <motion.div key={i} className="flex items-center gap-2" variants={fadeUp}>
                  <AppIcon name={app.name} size={14} />
                  <span className="text-[10px] text-[rgba(245,247,251,0.7)] flex-1 truncate">{app.name}</span>
                  <span className="text-[9px] text-[rgba(245,247,251,0.4)] tabular-nums">{formatDuration(app.duration)}</span>
                  <span className="text-[8px] text-[rgba(245,247,251,0.3)] w-[28px] text-right tabular-nums">{pct}%</span>
                </motion.div>
              );
            })}
          </motion.div>
        )}
      </CardContent>
    </Card>
  );
}
