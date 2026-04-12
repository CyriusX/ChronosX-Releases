/**
 * Reports Page - Página de relatórios com CX-155 features
 *
 * RBAC:
 * - Admin/Gestor: Pode ver relatórios de qualquer usuário da org
 * - Colaborador: Pode ver apenas seus próprios relatórios
 *
 * Composition Pattern: Composto por componentes especializados
 * SOLID:
 * - SRP: Página apenas orquestra componentes e estado
 * - OCP: Novos componentes podem ser adicionados sem modificar a página
 * - DIP: Usa hook useReportsData para dados, componentes para UI
 */

import { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import {
  ArrowLeft,
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
import { useNavigate } from 'react-router-dom';
import { Sidebar } from '../components/dashboard/Sidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useAuthStore } from '../stores/authStore';
import { useReportsData, useReportsSummary } from '../hooks/useReportsData';
import { listMembers } from '../services/memberApi';
import { listUserTasks, listProjects, type Task, type ProjectItem } from '../services/projectsApi';
import { exportReportsToCSV } from '../lib/exportReports';
import { fadeUp, staggerContainer, STAGGER } from '../lib/animation';
import { toLocalDateStr } from '../types/reports';
import type { Member } from '../types/member';

// Components (Composition Pattern)
import {
  SummaryCard,
  ActivityHeatmap,
  ProductivityTrend,
  TopAppsSection,
  TopPathsSection,
  CategoryDonut,
  DistractionSection,
  ProjectTasksAccordion,
} from '../components/reports';
import type { PeriodPreset, GroupByOption } from '../types/reports';

// Period preset options for dropdown (labels resolved via i18n at render time)
const PERIOD_KEYS: { value: PeriodPreset; tKey: string }[] = [
  { value: 'today', tKey: 'reports.today' },
  { value: 'this_week', tKey: 'reports.thisWeek' },
  { value: 'this_month', tKey: 'reports.thisMonth' },
  { value: 'last_30_days', tKey: 'reports.last30' },
  { value: 'last_90_days', tKey: 'reports.last90' },
];

// Group by options
const GROUP_BY_KEYS: { value: GroupByOption; tKey: string }[] = [
  { value: 'day', tKey: 'reports.day' },
  { value: 'week', tKey: 'reports.week' },
  { value: 'month', tKey: 'reports.month' },
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
  const { canManageTeam } = usePermissions();
  const user = useAuthStore((state) => state.user);

  // Handler for heatmap cell click - navigates to activities page
  const handleHeatmapCellClick = (date: Date, userId?: string) => {
    const dateStr = toLocalDateStr(date);
    const url = userId
      ? `/activities?date=${dateStr}&userId=${userId}`
      : `/activities?date=${dateStr}`;
    navigate(url);
  };

  // Team members for RBAC filter
  const [members, setMembers] = useState<Member[]>([]);
  const [showUserDropdown, setShowUserDropdown] = useState(false);

  // Projects & Tasks accordion
  const [allTasks, setAllTasks] = useState<Task[]>([]);
  const [projectsList, setProjectsList] = useState<ProjectItem[]>([]);
  const [tasksLoading, setTasksLoading] = useState(true);
  const [expandedProjects, setExpandedProjects] = useState<Set<string>>(new Set());
  const [showPeriodDropdown, setShowPeriodDropdown] = useState(false);
  const [showGroupByDropdown, setShowGroupByDropdown] = useState(false);

  // Refs for dropdown click outside
  const periodDropdownRef = useRef<HTMLDivElement>(null);
  const groupByDropdownRef = useRef<HTMLDivElement>(null);
  const userDropdownRef = useRef<HTMLDivElement>(null);

  // Period and group state
  const [selectedPeriod, setSelectedPeriod] = useState<PeriodPreset>('last_30_days');
  const [selectedGroupBy, setSelectedGroupBy] = useState<GroupByOption>('day');
  const [selectedUserId, setSelectedUserId] = useState<string | undefined>(undefined);

  // Use centralized reports hook
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

  // Calculate summary statistics
  const summary = useReportsSummary(data);

  // Load team members for Admin/Manager
  useEffect(() => {
    if (canManageTeam && user?.orgId) {
      loadMembers();
    }
  }, [canManageTeam, user?.orgId]);

  const loadMembers = async () => {
    try {
      const result = await listMembers();
      setMembers(result.members);
    } catch (err) {
      console.error('[Reports] Error loading members:', err);
    }
  };

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

  // Handle period change
  const handlePeriodChange = (period: PeriodPreset) => {
    setSelectedPeriod(period);
    setPeriodPreset(period);
  };

  // Handle user change (RBAC)
  const handleUserChange = (userId: string | undefined) => {
    setSelectedUserId(userId);
    setUserId(userId);
  };

  // Handle group by change
  const handleGroupByChange = (groupBy: GroupByOption) => {
    setSelectedGroupBy(groupBy);
    setGroupBy(groupBy);
  };

  // Get selected user name
  const getSelectedUserName = () => {
    if (!selectedUserId) return t('reports.myData');
    if (selectedUserId === 'all') return t('reports.allTeam');
    const member = members.find((m) => m.userId === selectedUserId);
    return member?.displayName || t('common.user');
  };

  // Get period label
  const getPeriodLabel = () => {
    const option = PERIOD_KEYS.find((o) => o.value === selectedPeriod);
    return option ? t(option.tKey) : t('reports.period');
  };

  // Handle export
  const handleExport = () => {
    exportReportsToCSV(data, {
      periodLabel: getPeriodLabel(),
      startDate: filters.dateRange.startDate,
      endDate: filters.dateRange.endDate,
      userName: getSelectedUserName(),
    });
  };

  return (
    <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0">
      <Sidebar />

      {/* Main content */}
      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          {/* Header */}
          <div className="flex flex-col sm:flex-row sm:items-center gap-3">
            <div className="flex items-center gap-4">
              <button
                onClick={() => navigate(-1)}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors flex-shrink-0"
              >
                <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              </button>
              <div>
                <h1 className="text-[20px] font-semibold text-[#f5f7fb]">{t('reports.title')}</h1>
                <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                  {t('reports.subtitle')}
                </p>
              </div>
            </div>

            {/* Controls */}
            <div className="flex flex-wrap items-center gap-2 sm:ml-auto">
              {/* Period Selector */}
              <div className="relative" ref={periodDropdownRef}>
                <button
                  onClick={() => {
                    setShowPeriodDropdown(!showPeriodDropdown);
                    setShowGroupByDropdown(false);
                    setShowUserDropdown(false);
                  }}
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
                      {PERIOD_KEYS.map((option) => (
                        <button
                          key={option.value}
                          onClick={() => {
                            handlePeriodChange(option.value);
                            setShowPeriodDropdown(false);
                          }}
                          className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${
                            selectedPeriod === option.value
                              ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]'
                              : 'text-[rgba(245,247,251,0.8)]'
                          }`}
                        >
                          {t(option.tKey)}
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>

              {/* Group By Selector */}
              <div className="relative" ref={groupByDropdownRef}>
                <button
                  onClick={() => {
                    setShowGroupByDropdown(!showGroupByDropdown);
                    setShowPeriodDropdown(false);
                    setShowUserDropdown(false);
                  }}
                  className="flex items-center gap-2 px-3 py-2 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg text-[12px] text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                >
                  <span>{t('reports.groupBy')} {t(GROUP_BY_KEYS.find(o => o.value === selectedGroupBy)?.tKey ?? '')}</span>
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
                      {GROUP_BY_KEYS.map((option) => (
                        <button
                          key={option.value}
                          onClick={() => {
                            handleGroupByChange(option.value);
                            setShowGroupByDropdown(false);
                          }}
                          className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${
                            selectedGroupBy === option.value
                              ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]'
                              : 'text-[rgba(245,247,251,0.8)]'
                          }`}
                        >
                          {t(option.tKey)}
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>

              {/* Export Button */}
              <motion.button
                onClick={handleExport}
                disabled={isLoading}
                whileHover={{ scale: 1.02 }}
                whileTap={{ scale: 0.98 }}
                className="flex items-center gap-2 px-3 py-2 bg-[rgba(139,92,246,0.15)] border border-[rgba(139,92,246,0.3)] rounded-lg text-[12px] text-[#8B5CF6] hover:bg-[rgba(139,92,246,0.2)] transition-colors disabled:opacity-50"
              >
                <Download className="w-4 h-4" />
                <span className="hidden sm:inline">{t('reports.exportCsv')}</span>
              </motion.button>

              {/* Refresh Button */}
              <motion.button
                onClick={refresh}
                disabled={isLoading}
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors disabled:opacity-50"
              >
                <RefreshCw className={`w-4 h-4 text-[rgba(245,247,251,0.6)] ${isLoading ? 'animate-spin' : ''}`} />
              </motion.button>
            </div>
          </div>
        </div>

        {/* Scrollable Content Area */}
        <div className="flex-1 overflow-y-auto px-5 pb-4">
          <div className="flex flex-col gap-4">
            {/* User Filter (Admin/Manager only) - RBAC */}
            {canManageTeam && (
              <div className="relative" ref={userDropdownRef}>
                <motion.div
                  className="flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl cursor-pointer hover:border-[rgba(255,255,255,0.1)] transition-colors"
                  onClick={() => {
                    setShowUserDropdown(!showUserDropdown);
                    setShowPeriodDropdown(false);
                    setShowGroupByDropdown(false);
                  }}
                  whileHover={{ scale: 1.005 }}
                  whileTap={{ scale: 0.995 }}
                >
                  <Users className="w-4 h-4 text-[#8B5CF6]" />
                  <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('reports.viewing')}</span>
                  <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
                    {getSelectedUserName()}
                  </span>
                  <ChevronDown className={`w-4 h-4 text-[rgba(245,247,251,0.4)] ml-auto transition-transform ${showUserDropdown ? 'rotate-180' : ''}`} />
                </motion.div>

                {/* Dropdown */}
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
                        onClick={() => { handleUserChange(undefined); setShowUserDropdown(false); }}
                        className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${!selectedUserId ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                      >
                        {t('reports.myData')}
                      </button>
                      <button
                        onClick={() => { handleUserChange('all'); setShowUserDropdown(false); }}
                        className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedUserId === 'all' ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                      >
                        {t('reports.allTeam')}
                      </button>
                      <div className="border-t border-[rgba(255,255,255,0.06)]" />
                      {members.filter((member) => member.userId !== user?.id).map((member) => (
                        <button
                          key={member.userId}
                          onClick={() => { handleUserChange(member.userId); setShowUserDropdown(false); }}
                          className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedUserId === member.userId ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                        >
                          {member.displayName}
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            )}

            {/* Error State */}
            {error && (
              <div className="flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(248,113,113,0.1)] to-[rgba(248,113,113,0.05)] border border-[rgba(248,113,113,0.2)] rounded-xl">
                <AlertCircle className="w-4 h-4 text-[#f87171]" />
                <span className="text-[13px] text-[#f87171]">{error}</span>
                <button
                  onClick={refresh}
                  className="ml-auto text-[12px] text-[#8B5CF6] hover:underline"
                >
                  {t('common.retry')}
                </button>
              </div>
            )}

            {/* Summary Cards */}
            <motion.div
              className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4"
              variants={staggerContainer(STAGGER.cards)}
              initial="hidden"
              animate="visible"
            >
              <motion.div variants={fadeUp}>
                <SummaryCard
                  title={t('reports.activeTime')}
                  value={formatDuration(summary.totalActiveSeconds)}
                  subtitle={`${summary.daysWithData} ${t('reports.daysWithData')}`}
                  icon={Clock}
                  iconBgColor="rgba(139,92,246,0.15)"
                  iconColor="#8B5CF6"
                  isLoading={isLoading}
                />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard
                  title={t('reports.idleTime')}
                  value={formatDuration(summary.totalIdleSeconds)}
                  subtitle={summary.totalActiveSeconds > 0 ? `${Math.round((summary.totalIdleSeconds / (summary.totalActiveSeconds + summary.totalIdleSeconds)) * 100)}% ${t('reports.ofTotal')}` : `0% ${t('reports.ofTotal')}`}
                  icon={Activity}
                  iconBgColor="rgba(251,191,36,0.15)"
                  iconColor="#fbbf24"
                  isLoading={isLoading}
                />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard
                  title={t('reports.focusScore')}
                  value={`${summary.focusScore}%`}
                  subtitle={t('reports.focusScoreDesc')}
                  icon={Target}
                  iconBgColor="rgba(5,223,114,0.15)"
                  iconColor="#05df72"
                  progress={summary.focusScore}
                  progressColor="#05df72"
                  isLoading={isLoading}
                />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard
                  title={t('reports.period')}
                  value={getPeriodLabel()}
                  subtitle={`${filters.dateRange.startDate} ${t('reports.to')} ${filters.dateRange.endDate}`}
                  icon={Calendar}
                  iconBgColor="rgba(138,92,246,0.15)"
                  iconColor="#8a5cf6"
                  isLoading={isLoading}
                />
              </motion.div>
            </motion.div>

            {/* Activity Heatmap */}
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
              <ProductivityTrend
                periods={data.productivityTrend?.periods ?? []}
                isLoading={isLoading}
                title={t('reports.productivityTrend')}
              />
            </motion.div>

            {/* Main Grid - Apps, Paths, Categories, Distractions */}
            <motion.div
              className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-stretch"
              variants={staggerContainer(STAGGER.cards)}
              initial="hidden"
              animate="visible"
            >
              {/* Top Apps */}
              <motion.div variants={fadeUp} className="h-full">
                <TopAppsSection
                  apps={data.topApps?.apps ?? []}
                  isLoading={isLoading}
                  title={t('reports.topApps')}
                  maxItems={15}
                />
              </motion.div>

              {/* Category Distribution */}
              <motion.div variants={fadeUp} className="h-full">
                <CategoryDonut
                  categories={data.categoryDistribution?.categories ?? []}
                  isLoading={isLoading}
                  title={t('reports.categoryDistribution')}
                />
              </motion.div>

              {/* Top Paths */}
              <motion.div variants={fadeUp} className="h-full">
                <TopPathsSection
                  paths={data.topPaths?.paths ?? []}
                  isLoading={isLoading}
                  title={t('reports.topPaths')}
                  maxItems={10}
                />
              </motion.div>

              {/* Distraction Stats */}
              <motion.div variants={fadeUp} className="h-full">
                <DistractionSection
                  data={data.distractionStats}
                  isLoading={isLoading}
                  title={t('reports.distractionAnalysis')}
                />
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
