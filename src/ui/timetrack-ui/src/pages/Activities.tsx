/**
 * Activities Page — Historical view of tracked time with date navigation
 *
 * Mirrors the Dashboard layout but lets the user browse any date.
 * Includes: day insights, tracked time ring, productivity ring,
 * activity timeline, productivity heatmap, categories/apps/projects,
 * session list, and a mini calendar in the right panel.
 */

import { useState, useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { AnimatePresence, motion } from 'motion/react';
import { ChevronDown, Users } from 'lucide-react';
import { Sidebar } from '../components/dashboard';
import { BottomCards, ActivitySection } from '../components/dashboard';
import { useIpc } from '../hooks/useIpc';
import { isDesktopRuntime } from '../lib/runtime';
import { useActivitiesData } from '../hooks/useActivitiesData';
import { usePermissions } from '../hooks/usePermissions';
import { listMembers } from '../services/memberApi';
import type { Member } from '../types/member';
import {
  DateNavigator,
  DayInsights,
  ProductivityHeatmap,
  MiniCalendar,
  SessionList,
  ActivitiesTopCards,
  TopAppsPanel,
} from '../components/activities';
import { ActivitiesFoldersCard } from '../components/activities/ActivitiesFoldersCard';

// ============================================================================
// PAGE
// ============================================================================

export default function Activities() {
  const { t } = useTranslation();
  const data = useActivitiesData();
  const { summary, activities, isLoading } = data;
  const { sendQuery, isConnected } = useIpc();
  const desktopRuntime = isDesktopRuntime();
  const { canManageTeam } = usePermissions();
  const userId = data.userId;
  const [workGoalSeconds, setWorkGoalSeconds] = useState(28800);

  // Team member selector (Activities should drive ?userId= so task timeline uses /users/{id}/task-entries)
  const [members, setMembers] = useState<Member[]>([]);
  const [showUserDropdown, setShowUserDropdown] = useState(false);
  const userDropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!canManageTeam) return;
    listMembers().then(r => setMembers(r.members ?? [])).catch(() => {});
  }, [canManageTeam]);

  useEffect(() => {
    if (!showUserDropdown) return;
    const onDocClick = (e: MouseEvent) => {
      if (!userDropdownRef.current) return;
      if (userDropdownRef.current.contains(e.target as Node)) return;
      setShowUserDropdown(false);
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [showUserDropdown]);

  const selectedUserName = useMemo(() => {
    if (!userId) return t('reports.myData');
    const m = members.find(mm => mm.userId === userId);
    return m?.displayName ?? userId;
  }, [members, t, userId]);

  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;
    sendQuery('getSettings').then((res) => {
      if (res.success && res.data) {
        const d = res.data as { workGoalSeconds?: number | null };
        if (d.workGoalSeconds) setWorkGoalSeconds(d.workGoalSeconds);
      }
    });
  }, [desktopRuntime, isConnected, sendQuery]);

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0">
      <Sidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header with DateNavigator */}
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
        </div>

        <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0">
          {/* Left Content — scrollable */}
          <div className="flex-1 flex flex-col gap-4 overflow-y-auto min-w-0 pr-1">
            {/* Team filter (Admin/Manager only) */}
            {canManageTeam && (
              <div className="relative" ref={userDropdownRef}>
                <motion.div
                  className="flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl cursor-pointer hover:border-[rgba(255,255,255,0.1)] transition-colors"
                  onClick={() => setShowUserDropdown(v => !v)}
                  whileHover={{ scale: 1.005 }}
                  whileTap={{ scale: 0.995 }}
                >
                  <Users className="w-4 h-4 text-[#8B5CF6]" />
                  <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('activities.viewing')}</span>
                  <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
                    {selectedUserName}
                  </span>
                  <ChevronDown className={`w-4 h-4 text-[rgba(245,247,251,0.4)] ml-auto transition-transform ${showUserDropdown ? 'rotate-180' : ''}`} />
                </motion.div>

                <AnimatePresence>
                  {showUserDropdown && (
                    <motion.div
                      initial={{ opacity: 0, y: -8, scale: 0.95 }}
                      animate={{ opacity: 1, y: 0, scale: 1 }}
                      exit={{ opacity: 0, y: -8, scale: 0.95 }}
                      transition={{ duration: 0.15 }}
                      className="absolute top-full left-0 right-0 mt-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-lg z-20 max-h-[300px] overflow-y-auto"
                    >
                      <button
                        onClick={() => { data.setSelectedUserId(undefined); setShowUserDropdown(false); }}
                        className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${!userId ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                      >
                        {t('reports.myData')}
                      </button>
                      <div className="border-t border-[rgba(255,255,255,0.06)]" />
                      {members.map((m) => (
                        <button
                          key={m.userId}
                          onClick={() => { data.setSelectedUserId(m.userId); setShowUserDropdown(false); }}
                          className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${userId === m.userId ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                        >
                          {m.displayName}
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            )}

            {/* Day Insights */}
            <DayInsights
              summary={summary}
              comparisonText={data.comparisonText}
              productivityComparison={data.productivityComparison}
              isToday={data.isToday}
            />

            {/* Top Cards: Tempo Rastreado + Produtividade + Resumo */}
            <ActivitiesTopCards summary={summary} workGoalSeconds={workGoalSeconds} />

            {/* Activity Timeline */}
            {isLoading && activities.length === 0 ? (
              <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-6 flex items-center justify-center">
                <div className="flex items-center gap-3">
                  <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                  <span className="text-[12px] text-[rgba(245,247,251,0.5)]">{t('activities.loadingActivities')}</span>
                </div>
              </div>
            ) : (
              <ActivitySection
                activities={activities}
                selectedDate={data.selectedDate}
                userId={userId}
              />
            )}

            {/* Productivity Heatmap */}
            {isLoading && activities.length === 0 ? (
              <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-6 flex items-center justify-center">
                <div className="flex items-center gap-3">
                  <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                  <span className="text-[12px] text-[rgba(245,247,251,0.5)]">{t('activities.loadingProductivity')}</span>
                </div>
              </div>
            ) : (
              <ProductivityHeatmap activities={activities} selectedDate={data.selectedDate} />
            )}

            {/* Categories, Apps & Sites, Projects */}
            <BottomCards summary={summary} />

            {/* Session List */}
            <SessionList activities={activities} defaultCollapsed />

            {/* Folders (collapsed section) */}
            <ActivitiesFoldersCard date={data.selectedDate} userId={userId} />

            {/* Right panel content — shown inline on mobile/tablet (< lg) */}
            <div className="lg:hidden flex flex-col gap-4">
              <MiniCalendar
                selectedDate={data.selectedDate}
                onDateSelect={data.setSelectedDate}
                weeklyHistory={data.weeklyHistory}
              />
              <TopAppsPanel summary={summary} />
            </div>
          </div>

          {/* Right Panel — desktop only */}
          <div className="hidden lg:flex w-[280px] flex-shrink-0 flex-col gap-4 overflow-y-auto">
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

// (ActivitiesTopCards and TopAppsPanel extracted to src/components/activities for reuse in LP demos.)
