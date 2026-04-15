/**
 * Reports Page — Web Admin Portal
 *
 * Same as desktop but always API-based and member selector always visible.
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import {
  Calendar,
  RefreshCw,
  Users,
  ChevronDown,
  Clock,
  Activity,
  Target,
  AlertCircle,
  Download,
} from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { useReportsData, useReportsSummary } from '../hooks/useReportsData';
import { listMembers } from '../services/memberApi';
import { fadeUp, staggerContainer, STAGGER } from '@desktop/lib/animation';
import { exportReportsToCSV } from '@desktop/lib/exportReports';
import type { Member } from '@desktop/types/member';
import {
  SummaryCard,
  ActivityHeatmap,
  ProductivityTrend,
  TopAppsSection,
  TopPathsSection,
  CategoryDonut,
  DistractionSection,
  ProjectTasksAccordion,
} from '@desktop/components/reports';
import type { PeriodPreset, GroupByOption } from '@desktop/types/reports';
import { listUserTasks, listProjects } from '../services/projectsApi';

const PERIOD_OPTIONS: { value: PeriodPreset; labelKey: string }[] = [
  { value: 'today', labelKey: 'reports.today' },
  { value: 'this_week', labelKey: 'reports.thisWeek' },
  { value: 'this_month', labelKey: 'reports.thisMonth' },
  { value: 'last_30_days', labelKey: 'reports.last30' },
  { value: 'last_90_days', labelKey: 'reports.last90' },
];

const GROUP_BY_OPTIONS: { value: GroupByOption; labelKey: string }[] = [
  { value: 'day', labelKey: 'reports.day' },
  { value: 'week', labelKey: 'reports.week' },
  { value: 'month', labelKey: 'reports.month' },
];

function formatDuration(seconds: number): string {
  if (seconds === 0) return '0h';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

export default function Reports() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [members, setMembers] = useState<Member[]>([]);
  const [showUserDropdown, setShowUserDropdown] = useState(false);
  const [showPeriodDropdown, setShowPeriodDropdown] = useState(false);
  const [showGroupByDropdown, setShowGroupByDropdown] = useState(false);

  // Projects & Tasks accordion
  const [allTasks, setAllTasks] = useState<any[]>([]);
  const [projectsList, setProjectsList] = useState<any[]>([]);
  const [tasksLoading, setTasksLoading] = useState(true);
  const [expandedProjects, setExpandedProjects] = useState<Set<string>>(new Set());

  const periodDropdownRef = useRef<HTMLDivElement>(null);
  const groupByDropdownRef = useRef<HTMLDivElement>(null);
  const userDropdownRef = useRef<HTMLDivElement>(null);

  const [selectedPeriod, setSelectedPeriod] = useState<PeriodPreset>('last_30_days');
  const [selectedGroupBy, setSelectedGroupBy] = useState<GroupByOption>('day');
  // Default to "all team" so the admin sees aggregated data immediately
  const [selectedUserId, setSelectedUserId] = useState<string | undefined>('all');

  const {
    data,
    isLoading,
    error,
    filters,
    setPeriodPreset,
    setUserId,
    setGroupBy,
    refresh,
  } = useReportsData({
    initialPeriod: selectedPeriod,
    userId: selectedUserId,
    autoFetch: true,
  });

  const summary = useReportsSummary(data);

  // Load team members
  useEffect(() => {
    loadMembers();
  }, []);

  // Load projects & tasks for the accordion — reacts to selected user
  useEffect(() => {
    setTasksLoading(true);
    Promise.all([
      listUserTasks(selectedUserId, true).catch(() => ({ tasks: [] })),
      listProjects(false).catch(() => ({ projects: [], totalCount: 0 })),
    ]).then(([tasksRes, projRes]) => {
      setAllTasks(tasksRes.tasks);
      setProjectsList(projRes.projects);
    }).finally(() => setTasksLoading(false));
  }, [selectedUserId]);

  const loadMembers = async () => {
    try {
      const result = await listMembers();
      setMembers(result.members);
    } catch (err) {
      console.error('[Reports] Error loading members:', err);
    }
  };

  const handlePeriodChange = (period: PeriodPreset) => {
    setSelectedPeriod(period);
    setPeriodPreset(period);
  };

  const handleUserChange = (userId: string | undefined) => {
    setSelectedUserId(userId);
    setUserId(userId);
  };

  const handleGroupByChange = (groupBy: GroupByOption) => {
    setSelectedGroupBy(groupBy);
    setGroupBy(groupBy);
  };

  const getSelectedUserName = () => {
    if (!selectedUserId || selectedUserId === 'all') return t('reports.allTeam');
    const member = members.find((m) => m.userId === selectedUserId);
    return member?.displayName || t('common.user');
  };

  const getPeriodLabel = () => {
    const option = PERIOD_OPTIONS.find((o) => o.value === selectedPeriod);
    return option ? t(option.labelKey) : t('reports.customPeriod');
  };

  const handleExport = () => {
    exportReportsToCSV(data, {
      periodLabel: getPeriodLabel(),
      startDate: filters.dateRange.startDate,
      endDate: filters.dateRange.endDate,
      userName: getSelectedUserName(),
    });
  };

  // Handler for heatmap cell click - navigates to activities page
  const handleHeatmapCellClick = useCallback((date: Date, userId?: string) => {
    const dateStr = date.toISOString().split('T')[0];
    const url = userId && userId !== 'all'
      ? `/activities?date=${dateStr}&userId=${userId}`
      : `/activities?date=${dateStr}`;
    navigate(url);
  }, [navigate]);

  return (
    <div className="flex h-screen bg-[#0b0d14]">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          {/* Header */}
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-[20px] font-semibold text-[#f5f7fb]">{t('reports.title')}</h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                {t('reports.subtitle')}
              </p>
            </div>

            {/* Controls */}
            <div className="flex items-center gap-3">
              {/* Period Selector */}
              <div className="relative" ref={periodDropdownRef}>
                <button
                  onClick={() => { setShowPeriodDropdown(!showPeriodDropdown); setShowGroupByDropdown(false); setShowUserDropdown(false); }}
                  className="flex items-center gap-2 px-3 py-2 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg text-[12px] text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                >
                  <Calendar className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
                  <span>{getPeriodLabel()}</span>
                  <ChevronDown className={`w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] transition-transform ${showPeriodDropdown ? 'rotate-180' : ''}`} />
                </button>
                <AnimatePresence>
                  {showPeriodDropdown && (
                    <motion.div
                      initial={{ opacity: 0, y: -8, scale: 0.95 }}
                      animate={{ opacity: 1, y: 0, scale: 1 }}
                      exit={{ opacity: 0, y: -8, scale: 0.95 }}
                      transition={{ duration: 0.15 }}
                      className="absolute top-full left-0 mt-1 min-w-[160px] bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-lg z-30 overflow-hidden"
                    >
                      {PERIOD_OPTIONS.map((option) => (
                        <button
                          key={option.value}
                          onClick={() => { handlePeriodChange(option.value); setShowPeriodDropdown(false); }}
                          className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedPeriod === option.value ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                        >
                          {t(option.labelKey)}
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>

              {/* Group By Selector */}
              <div className="relative" ref={groupByDropdownRef}>
                <button
                  onClick={() => { setShowGroupByDropdown(!showGroupByDropdown); setShowPeriodDropdown(false); setShowUserDropdown(false); }}
                  className="flex items-center gap-2 px-3 py-2 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg text-[12px] text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                >
                  <span>{t('reports.groupBy')} {t(GROUP_BY_OPTIONS.find(o => o.value === selectedGroupBy)?.labelKey ?? '')}</span>
                  <ChevronDown className={`w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] transition-transform ${showGroupByDropdown ? 'rotate-180' : ''}`} />
                </button>
                <AnimatePresence>
                  {showGroupByDropdown && (
                    <motion.div
                      initial={{ opacity: 0, y: -8, scale: 0.95 }}
                      animate={{ opacity: 1, y: 0, scale: 1 }}
                      exit={{ opacity: 0, y: -8, scale: 0.95 }}
                      transition={{ duration: 0.15 }}
                      className="absolute top-full left-0 mt-1 min-w-[140px] bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-lg z-30 overflow-hidden"
                    >
                      {GROUP_BY_OPTIONS.map((option) => (
                        <button
                          key={option.value}
                          onClick={() => { handleGroupByChange(option.value); setShowGroupByDropdown(false); }}
                          className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedGroupBy === option.value ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                        >
                          {t(option.labelKey)}
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>

              {/* Export */}
              <motion.button onClick={handleExport} disabled={isLoading} whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.98 }}
                className="flex items-center gap-2 px-3 py-2 bg-[rgba(139,92,246,0.15)] border border-[rgba(139,92,246,0.3)] rounded-lg text-[12px] text-[#8B5CF6] hover:bg-[rgba(139,92,246,0.2)] transition-colors disabled:opacity-50">
                <Download className="w-4 h-4" />
                {t('reports.exportCsv')}
              </motion.button>

              {/* Refresh */}
              <motion.button onClick={refresh} disabled={isLoading} whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors disabled:opacity-50">
                <RefreshCw className={`w-4 h-4 text-[rgba(245,247,251,0.6)] ${isLoading ? 'animate-spin' : ''}`} />
              </motion.button>
            </div>
          </div>
        </div>

        {/* Scrollable Content */}
        <div className="flex-1 overflow-y-auto px-5 pb-4">
          <div className="flex flex-col gap-4">
            {/* User Filter — always visible in web portal */}
            <div className="relative" ref={userDropdownRef}>
              <motion.div
                className="flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl cursor-pointer hover:border-[rgba(255,255,255,0.1)] transition-colors"
                onClick={() => { setShowUserDropdown(!showUserDropdown); setShowPeriodDropdown(false); setShowGroupByDropdown(false); }}
                whileHover={{ scale: 1.005 }}
                whileTap={{ scale: 0.995 }}
              >
                <Users className="w-4 h-4 text-[#8B5CF6]" />
                <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('reports.viewing')}</span>
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{getSelectedUserName()}</span>
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
                    <button onClick={() => { handleUserChange('all'); setShowUserDropdown(false); }}
                      className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedUserId === 'all' ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}>
                      {t('reports.allTeam')}
                    </button>
                    <div className="border-t border-[rgba(255,255,255,0.06)]" />
                    {members.map((member) => (
                      <button key={member.userId}
                        onClick={() => { handleUserChange(member.userId); setShowUserDropdown(false); }}
                        className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedUserId === member.userId ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}>
                        {member.displayName}
                      </button>
                    ))}
                  </motion.div>
                )}
              </AnimatePresence>
            </div>

            {/* Error State */}
            {error && (
              <div className="flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(248,113,113,0.1)] to-[rgba(248,113,113,0.05)] border border-[rgba(248,113,113,0.2)] rounded-xl">
                <AlertCircle className="w-4 h-4 text-[#f87171]" />
                <span className="text-[13px] text-[#f87171]">{error}</span>
                <button onClick={refresh} className="ml-auto text-[12px] text-[#8B5CF6] hover:underline">
                  {t('common.retry')}
                </button>
              </div>
            )}

            {/* Summary Cards */}
            <motion.div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4"
              variants={staggerContainer(STAGGER.cards)} initial="hidden" animate="visible">
              <motion.div variants={fadeUp}>
                <SummaryCard title={t('reports.activeTime')} value={formatDuration(summary.totalActiveSeconds)}
                  subtitle={`${summary.daysWithData} ${t('reports.daysWithData')}`}
                  icon={Clock} iconBgColor="rgba(139,92,246,0.15)" iconColor="#8B5CF6" isLoading={isLoading} />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard title={t('reports.idleTime')} value={formatDuration(summary.totalIdleSeconds)}
                  subtitle={summary.totalActiveSeconds > 0 ? `${Math.round((summary.totalIdleSeconds / (summary.totalActiveSeconds + summary.totalIdleSeconds)) * 100)}% ${t('reports.ofTotal')}` : `0% ${t('reports.ofTotal')}`}
                  icon={Activity} iconBgColor="rgba(251,191,36,0.15)" iconColor="#fbbf24" isLoading={isLoading} />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard title={t('reports.focusScore')} value={`${summary.focusScore}%`}
                  subtitle={t('reports.focusScoreDesc')}
                  icon={Target} iconBgColor="rgba(5,223,114,0.15)" iconColor="#05df72"
                  progress={summary.focusScore} progressColor="#05df72" isLoading={isLoading} />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard title={t('reports.period')} value={getPeriodLabel()}
                  subtitle={`${filters.dateRange.startDate} ${t('reports.to')} ${filters.dateRange.endDate}`}
                  icon={Calendar} iconBgColor="rgba(138,92,246,0.15)" iconColor="#8a5cf6" isLoading={isLoading} />
              </motion.div>
            </motion.div>

            {/* Heatmap */}
            <motion.div variants={fadeUp} initial="hidden" animate="visible">
              <ActivityHeatmap
                days={data.dailySummaryRange?.days ?? []}
                isLoading={isLoading}
                title={t('reports.activityHeatmap')}
                userId={selectedUserId}
                onCellClick={handleHeatmapCellClick}
              />
            </motion.div>

            {/* Productivity Trend */}
            <motion.div variants={fadeUp} initial="hidden" animate="visible">
              <ProductivityTrend periods={data.productivityTrend?.periods ?? []} isLoading={isLoading}
                title={t('reports.productivityTrend')} />
            </motion.div>

            {/* Grid */}
            <motion.div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-stretch"
              variants={staggerContainer(STAGGER.cards)} initial="hidden" animate="visible">
              <motion.div variants={fadeUp} className="h-full">
                <TopAppsSection apps={data.topApps?.apps ?? []} isLoading={isLoading} title={t('reports.topApps')} maxItems={15} />
              </motion.div>
              <motion.div variants={fadeUp} className="h-full">
                <CategoryDonut categories={data.categoryDistribution?.categories ?? []} isLoading={isLoading} title={t('reports.categoryDistribution')} />
              </motion.div>
              <motion.div variants={fadeUp} className="h-full">
                <TopPathsSection paths={data.topPaths?.paths ?? []} isLoading={isLoading} title={t('reports.topPaths')} maxItems={10} />
              </motion.div>
              <motion.div variants={fadeUp} className="h-full">
                <DistractionSection data={data.distractionStats} isLoading={isLoading} title={t('reports.distractionAnalysis')} />
              </motion.div>
            </motion.div>

            {/* Projects & Tasks Accordion */}
            <motion.div variants={fadeUp} initial="hidden" animate="visible">
              <ProjectTasksAccordion
                tasks={allTasks}
                projects={projectsList}
                loading={tasksLoading}
                expanded={expandedProjects}
                onToggle={(id) => setExpandedProjects((prev) => {
                  const next = new Set(prev);
                  if (next.has(id)) next.delete(id); else next.add(id);
                  return next;
                })}
              />
            </motion.div>
          </div>
        </div>
      </main>
    </div>
  );
}
