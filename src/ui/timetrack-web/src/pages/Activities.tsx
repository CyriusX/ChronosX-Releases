/**
 * Activities Page — Web Admin Portal
 *
 * Always shows member selector. Always fetches from backend API.
 * Reuses desktop activity components for consistent UI.
 */

import { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { Users, ChevronDown } from 'lucide-react';
import { AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { WebSidebar } from '../components/WebSidebar';
import { useActivitiesData } from '../hooks/useActivitiesData';
import { useTeamStatus } from '../hooks/useTeamStatus';
import { formatDuration } from '@desktop/lib/utils';
import { fadeUp, staggerContainer, STAGGER } from '@desktop/lib/animation';
import { Card, CardContent, CardHeader, CardTitle } from '@desktop/components/ui/card';
import { DateNavigator, DayInsights, ProductivityHeatmap, MiniCalendar, SessionList } from '@desktop/components/activities';
import { ActivitySection } from '@desktop/components/dashboard/ActivitySection';
import { AppIcon } from '@desktop/components/dashboard/shared';
import { cardBase } from '@desktop/components/dashboard/shared/styles';

export default function Activities() {
  const { t } = useTranslation();
  const data = useActivitiesData();
  const { summary, activities, isLoading } = data;

  // Member selector — useTeamStatus carries the live `isTracking` flag (heartbeat-based)
  // so the page can drive the "Ao vivo" badge from real status, not a stale today-flag.
  const { members, loadTeamStatus } = useTeamStatus();
  const [showMemberDropdown, setShowMemberDropdown] = useState(false);

  useEffect(() => {
    loadTeamStatus();
  }, [loadTeamStatus]);

  // Auto-select first member once the list is loaded.
  useEffect(() => {
    if (!data.selectedUserId && members.length > 0) {
      data.setSelectedUserId(members[0].userId);
    }
  }, [members, data]);

  const isLive = (() => {
    if (!data.isToday || !data.selectedUserId) return false;
    const m = members.find(mm => mm.userId === data.selectedUserId);
    return m?.isTracking ?? false;
  })();

  const getSelectedUserName = () => {
    if (!data.selectedUserId) return 'Selecione um membro';
    const member = members.find(m => m.userId === data.selectedUserId);
    return member?.displayName || 'Membro';
  };

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden pt-[52px] md:pt-0">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header */}
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <span className="text-[14px] font-medium text-[#f5f7fb]">{t('activities.title')}</span>
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

          {/* Member Selector */}
          <div className="relative mt-3">
            <motion.div
              className="flex items-center gap-3 px-4 py-2.5 bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl cursor-pointer hover:border-[rgba(255,255,255,0.1)] transition-colors"
              onClick={() => setShowMemberDropdown(!showMemberDropdown)}
              whileHover={{ scale: 1.005 }}
              whileTap={{ scale: 0.995 }}
            >
              <Users className="w-4 h-4 text-[#8B5CF6]" />
              <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('activities.viewing')}</span>
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
                {getSelectedUserName()}
              </span>
              <ChevronDown className={`w-4 h-4 text-[rgba(245,247,251,0.4)] ml-auto transition-transform ${showMemberDropdown ? 'rotate-180' : ''}`} />
            </motion.div>

            <AnimatePresence>
              {showMemberDropdown && (
                <motion.div
                  initial={{ opacity: 0, y: -8, scale: 0.95 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  exit={{ opacity: 0, y: -8, scale: 0.95 }}
                  transition={{ duration: 0.15 }}
                  className="absolute top-full left-0 right-0 mt-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-lg z-20 max-h-[300px] overflow-y-auto"
                >
                  {members.map((member) => (
                    <button
                      key={member.userId}
                      onClick={() => {
                        data.setSelectedUserId(member.userId);
                        setShowMemberDropdown(false);
                      }}
                      className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${
                        data.selectedUserId === member.userId
                          ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]'
                          : 'text-[rgba(245,247,251,0.8)]'
                      }`}
                    >
                      {member.displayName}
                    </button>
                  ))}
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>

        <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0">
          {/* Left Content */}
          <div className="flex-1 flex flex-col gap-4 overflow-y-auto min-w-0 pr-1">
            {!data.selectedUserId ? (
              <div className="flex items-center justify-center py-12">
                <div className="text-center">
                  <p className="text-[14px] text-[rgba(245,247,251,0.6)]">{t('activities.selectMember')}</p>
                  <p className="text-[11px] text-[rgba(245,247,251,0.3)] mt-1">
                    {t('activities.selectMemberInstructions')}
                  </p>
                </div>
              </div>
            ) : (
              <>
                <DayInsights
                  summary={summary}
                  comparisonText={data.comparisonText}
                  productivityComparison={data.productivityComparison}
                  isToday={data.isToday}
                  isLive={isLive}
                />

                <ActivitiesTopCards summary={summary} />

                {/* Activity Timeline */}
                <ActivitySection
                  activities={activities}
                  selectedDate={data.selectedDate}
                />

                <ProductivityHeatmap activities={activities} selectedDate={data.selectedDate} />

                <SessionList activities={activities} />

                {/* Right panel content inline on mobile/tablet (< lg) */}
                <div className="lg:hidden flex flex-col gap-4">
                  <MiniCalendar
                    selectedDate={data.selectedDate}
                    onDateSelect={data.setSelectedDate}
                    weeklyHistory={data.weeklyHistory}
                  />
                  <TopAppsPanel summary={summary} />
                </div>
              </>
            )}
          </div>

          {/* Right Panel — desktop only */}
          <div className="hidden lg:flex w-[280px] flex-shrink-0 flex-col gap-4 overflow-y-auto">
            <MiniCalendar
              selectedDate={data.selectedDate}
              onDateSelect={data.setSelectedDate}
              weeklyHistory={data.weeklyHistory}
            />
            <TopAppsPanel summary={summary} />
          </div>
        </div>
      </main>
    </div>
  );
}

// ============================================================================
// TOP CARDS
// ============================================================================

function ActivitiesTopCards({ summary }: { summary: ReturnType<typeof useActivitiesData>['summary'] }) {
  const { t } = useTranslation();
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

  const scoreColor =
    productivityScore >= 80 ? '#05df72'
    : productivityScore >= 60 ? '#4ade80'
    : productivityScore >= 40 ? '#fbbf24'
    : productivityScore >= 20 ? '#fb923c'
    : '#f87171';

  const progressPercentage = Math.min((totalSeconds / 28800) * 100, 100);

  return (
    <motion.div
      className="grid grid-cols-1 sm:grid-cols-3 gap-4 flex-shrink-0"
      variants={staggerContainer(STAGGER.cards)}
      initial="hidden"
      animate="visible"
    >
      {/* Tempo Rastreado */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              {t('activities.timeTracked')}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-3 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="8" />
                  <circle cx="50" cy="50" r="42" fill="none" stroke="url(#webActGrad1)" strokeWidth="8" strokeDasharray={`${progressPercentage * 2.64} 264`} strokeLinecap="round" />
                  <defs>
                    <linearGradient id="webActGrad1" x1="0%" y1="0%" x2="100%" y2="100%">
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
                <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-0.5">{t('activities.goal8h')}</p>
                <p className="text-[10px] text-[rgba(245,247,251,0.4)] mt-1">
                  {t('activities.idle')} <span className="text-[rgba(245,247,251,0.6)] font-medium">{formatDuration(idleSeconds)}</span>
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
              {t('activities.focus')}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="9" />
                  <circle
                    cx="50" cy="50" r="42" fill="none"
                    stroke={scoreColor}
                    strokeWidth="9"
                    strokeDasharray={`${productivityScore * 2.64} 264`}
                    strokeLinecap="round"
                  />
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-[26px] font-bold" style={{ color: scoreColor }}>{productivityScore}</span>
                  <span className="text-[8px] text-[rgba(245,247,251,0.4)] -mt-0.5">{t('dashboard.score')}</span>
                </div>
              </div>
              <div className="mt-2.5 w-full space-y-1">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#4ade80]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">{t('activities.productive')}</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(productiveSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#fbbf24]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">{t('activities.neutral')}</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(neutralSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#f87171]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">{t('activities.distraction')}</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(distractionSecs)}</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Resumo */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              {t('activities.dailySummary')}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-4 pb-3 px-4">
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('activities.productiveTime')}</span>
                <span className="text-[16px] font-bold text-[#4ade80]">{formatDuration(productiveSecs)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('activities.idleTime')}</span>
                <span className="text-[16px] font-bold text-[rgba(245,247,251,0.5)]">{formatDuration(idleSeconds)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Focus Score</span>
                <span className="text-[16px] font-bold" style={{ color: scoreColor }}>{productivityScore}</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </motion.div>
  );
}

// ============================================================================
// TOP APPS PANEL
// ============================================================================

function TopAppsPanel({ summary }: { summary: ReturnType<typeof useActivitiesData>['summary'] }) {
  const { t } = useTranslation();
  const apps = [...(summary?.topApplications ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 8);

  const totalTime = apps.reduce((sum, a) => sum + a.duration, 0);

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
          {t('activities.topApps')}
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-3 px-4">
        {apps.length === 0 ? (
          <p className="text-[11px] text-[rgba(245,247,251,0.3)] text-center py-3">{t('activities.noApps')}</p>
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
