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
import { motion, AnimatePresence } from 'motion/react';
import {
  ArrowLeft,
  Calendar,
  RefreshCw,
  Users,
  ChevronDown,
  ChevronRight,
  Clock,
  Activity,
  Target,
  AlertCircle,
  Download,
  Briefcase,
  DollarSign,
  ListTodo,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Sidebar } from '../components/dashboard/Sidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useAuthStore } from '../stores/authStore';
import { useReportsData, useReportsSummary } from '../hooks/useReportsData';
import { listMembers } from '../services/memberApi';
import { listMyTasks, listProjects, type Task, type ProjectItem } from '../services/projectsApi';
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
} from '../components/reports';
import type { PeriodPreset, GroupByOption } from '../types/reports';

// ─── helpers ────────────────────────────────────────────────────────────────

function fmtDuration(s: number): string {
  if (s <= 0) return '0m';
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  if (h > 0 && m > 0) return `${h}h ${m}m`;
  if (h > 0) return `${h}h`;
  return `${m}m`;
}

function fmtCost(cost: number, currency: string): string {
  return `${currency} ${cost.toFixed(2)}`;
}

// ─── ProjectTasksAccordion ───────────────────────────────────────────────────

function ProjectTasksAccordion({
  tasks,
  projects,
  loading,
  expanded,
  onToggle,
}: {
  tasks: Task[];
  projects: ProjectItem[];
  loading: boolean;
  expanded: Set<string>;
  onToggle: (id: string) => void;
}) {
  const projectMap = new Map(projects.map((p) => [p.id, p]));

  // Group tasks by project, keeping only tasks that have worked time or are not done
  const byProject = new Map<string, Task[]>();
  tasks.forEach((t) => {
    const list = byProject.get(t.projectId) ?? [];
    list.push(t);
    byProject.set(t.projectId, list);
  });

  // Sort projects by total seconds desc
  const sorted = [...byProject.entries()].sort(
    ([, a], [, b]) =>
      b.reduce((s, t) => s + t.totalSecondsWorked, 0) -
      a.reduce((s, t) => s + t.totalSecondsWorked, 0),
  );

  return (
    <div className="bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-hidden">
      {/* Header */}
      <div className="px-5 py-4 border-b border-[rgba(255,255,255,0.06)] flex items-center gap-2">
        <Briefcase className="w-4 h-4 text-[#8B5CF6]" />
        <h2 className="text-[14px] font-semibold text-[#f5f7fb]">Projetos e Tarefas</h2>
        <span className="ml-auto text-[11px] text-[rgba(245,247,251,0.4)]">Tempo total acumulado</span>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-10">
          <RefreshCw className="w-4 h-4 text-[#8B5CF6] animate-spin" />
        </div>
      ) : sorted.length === 0 ? (
        <div className="px-5 py-8 text-center text-[12px] text-[rgba(245,247,251,0.35)] italic">
          Nenhuma tarefa encontrada.
        </div>
      ) : (
        <div className="divide-y divide-[rgba(255,255,255,0.04)]">
          {sorted.map(([projectId, ptasks]) => {
            const project = projectMap.get(projectId);
            const projectName = ptasks[0]?.projectName ?? 'Projeto desconhecido';
            const projectColor = ptasks[0]?.projectColor ?? '#8B5CF6';
            const totalSecs = ptasks.reduce((s, t) => s + t.totalSecondsWorked, 0);
            const isBillable = project?.isBillable ?? false;
            const hourlyRate = project?.hourlyRate ?? 0;
            const currency = project?.currency ?? 'USD';
            const totalCost = isBillable && hourlyRate ? (totalSecs / 3600) * hourlyRate : null;
            const todoCount = ptasks.filter((t) => t.status === 'Todo').length;
            const isOpen = expanded.has(projectId);

            return (
              <div key={projectId}>
                {/* Accordion header */}
                <button
                  className="w-full flex items-center gap-3 px-5 py-3.5 hover:bg-[rgba(255,255,255,0.03)] transition-colors text-left"
                  onClick={() => onToggle(projectId)}
                >
                  <span
                    className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                    style={{ backgroundColor: projectColor }}
                  />
                  <span className="flex-1 text-[13px] font-medium text-[#f5f7fb] truncate">{projectName}</span>

                  {/* Stats */}
                  <div className="flex items-center gap-3 flex-shrink-0">
                    {todoCount > 0 && (
                      <div className="flex items-center gap-1 text-[10px] text-[rgba(245,247,251,0.5)]">
                        <ListTodo className="w-3 h-3" />
                        {todoCount} a fazer
                      </div>
                    )}
                    <div className="flex items-center gap-1 text-[11px] text-[rgba(245,247,251,0.7)]">
                      <Clock className="w-3 h-3" />
                      {fmtDuration(totalSecs)}
                    </div>
                    {totalCost !== null && (
                      <div className="flex items-center gap-1 text-[11px] font-semibold text-[#c4b5fd]">
                        <DollarSign className="w-3 h-3" />
                        {fmtCost(totalCost, currency)}
                      </div>
                    )}
                    <ChevronRight className={`w-3.5 h-3.5 text-[rgba(245,247,251,0.3)] transition-transform ${isOpen ? 'rotate-90' : ''}`} />
                  </div>
                </button>

                {/* Task rows */}
                <AnimatePresence>
                  {isOpen && (
                    <motion.div
                      initial={{ height: 0, opacity: 0 }}
                      animate={{ height: 'auto', opacity: 1 }}
                      exit={{ height: 0, opacity: 0 }}
                      transition={{ duration: 0.2 }}
                      className="overflow-hidden"
                    >
                      <div className="divide-y divide-[rgba(255,255,255,0.03)]">
                        {ptasks
                          .sort((a, b) => b.totalSecondsWorked - a.totalSecondsWorked)
                          .map((task) => {
                            const taskCost = isBillable && hourlyRate
                              ? (task.totalSecondsWorked / 3600) * hourlyRate
                              : null;
                            return (
                              <div
                                key={task.id}
                                className="flex items-center gap-3 pl-10 pr-5 py-2.5 bg-[rgba(0,0,0,0.15)]"
                              >
                                <span
                                  className={`text-[9px] px-1.5 py-0.5 rounded font-medium flex-shrink-0 ${
                                    task.status === 'Done'
                                      ? 'bg-[rgba(5,223,114,0.12)] text-[#05df72]'
                                      : task.status === 'InProgress'
                                      ? 'bg-[rgba(251,191,36,0.12)] text-[#fbbf24]'
                                      : 'bg-[rgba(148,163,184,0.12)] text-[#94a3b8]'
                                  }`}
                                >
                                  {task.status === 'Done' ? 'Feito' : task.status === 'InProgress' ? 'Em progresso' : 'A fazer'}
                                </span>
                                <span className="flex-1 text-[12px] text-[rgba(245,247,251,0.8)] truncate">{task.title}</span>
                                <div className="flex items-center gap-3 flex-shrink-0">
                                  {task.totalSecondsWorked > 0 && (
                                    <span className="flex items-center gap-1 text-[11px] text-[rgba(245,247,251,0.5)]">
                                      <Clock className="w-3 h-3" />
                                      {fmtDuration(task.totalSecondsWorked)}
                                    </span>
                                  )}
                                  {taskCost !== null && task.totalSecondsWorked > 0 && (
                                    <span className="text-[11px] text-[#c4b5fd]">
                                      {fmtCost(taskCost, currency)}
                                    </span>
                                  )}
                                </div>
                              </div>
                            );
                          })}
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

// Period preset options for dropdown
const PERIOD_OPTIONS: { value: PeriodPreset; label: string }[] = [
  { value: 'today', label: 'Hoje' },
  { value: 'this_week', label: 'Esta semana' },
  { value: 'this_month', label: 'Este mês' },
  { value: 'last_30_days', label: 'Últimos 30 dias' },
  { value: 'last_90_days', label: 'Últimos 90 dias' },
];

// Group by options
const GROUP_BY_OPTIONS: { value: GroupByOption; label: string }[] = [
  { value: 'day', label: 'Dia' },
  { value: 'week', label: 'Semana' },
  { value: 'month', label: 'Mês' },
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
      listMyTasks(true).catch(() => ({ tasks: [] })),
      listProjects(false).catch(() => ({ projects: [], totalCount: 0 })),
    ]).then(([tasksRes, projRes]) => {
      setAllTasks(tasksRes.tasks);
      setProjectsList(projRes.projects);
    }).finally(() => setTasksLoading(false));
  }, []);

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
    if (!selectedUserId) return 'Meus dados';
    if (selectedUserId === 'all') return 'Toda a equipe';
    const member = members.find((m) => m.userId === selectedUserId);
    return member?.displayName || 'Usuário';
  };

  // Get period label
  const getPeriodLabel = () => {
    const option = PERIOD_OPTIONS.find((o) => o.value === selectedPeriod);
    return option?.label || 'Período customizado';
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
                <h1 className="text-[20px] font-semibold text-[#f5f7fb]">Relatórios</h1>
                <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                  Análise de tempo e produtividade
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
                      {PERIOD_OPTIONS.map((option) => (
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
                          {option.label}
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
                  <span>Agrupar: {GROUP_BY_OPTIONS.find(o => o.value === selectedGroupBy)?.label}</span>
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
                          {option.label}
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
                <span className="hidden sm:inline">Exportar CSV</span>
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
                  <span className="text-[13px] text-[rgba(245,247,251,0.6)]">Visualizando:</span>
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
                        Meus dados
                      </button>
                      <button
                        onClick={() => { handleUserChange('all'); setShowUserDropdown(false); }}
                        className={`w-full px-4 py-2.5 text-left text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors ${selectedUserId === 'all' ? 'text-[#8B5CF6] bg-[rgba(139,92,246,0.1)]' : 'text-[rgba(245,247,251,0.8)]'}`}
                      >
                        Toda a equipe
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
                  Tentar novamente
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
                  title="Tempo Ativo"
                  value={formatDuration(summary.totalActiveSeconds)}
                  subtitle={`${summary.daysWithData} dias com dados`}
                  icon={Clock}
                  iconBgColor="rgba(139,92,246,0.15)"
                  iconColor="#8B5CF6"
                  isLoading={isLoading}
                />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard
                  title="Tempo Idle"
                  value={formatDuration(summary.totalIdleSeconds)}
                  subtitle={summary.totalActiveSeconds > 0 ? `${Math.round((summary.totalIdleSeconds / (summary.totalActiveSeconds + summary.totalIdleSeconds)) * 100)}% do total` : '0% do total'}
                  icon={Activity}
                  iconBgColor="rgba(251,191,36,0.15)"
                  iconColor="#fbbf24"
                  isLoading={isLoading}
                />
              </motion.div>
              <motion.div variants={fadeUp}>
                <SummaryCard
                  title="Score de Foco"
                  value={`${summary.focusScore}%`}
                  subtitle="Baseado em tempo produtivo, distrações e blocos de foco"
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
                  title="Período"
                  value={getPeriodLabel()}
                  subtitle={`${filters.dateRange.startDate} a ${filters.dateRange.endDate}`}
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
                title="Mapa de Atividade"
                userId={selectedUserId}
                onCellClick={handleHeatmapCellClick}
              />
            </motion.div>

            {/* Productivity Trend */}
            <motion.div variants={fadeUp} initial="hidden" animate="visible">
              <ProductivityTrend
                periods={data.productivityTrend?.periods ?? []}
                isLoading={isLoading}
                title="Tendência de Produtividade"
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
                  title="Apps Mais Usados"
                  maxItems={15}
                />
              </motion.div>

              {/* Category Distribution */}
              <motion.div variants={fadeUp} className="h-full">
                <CategoryDonut
                  categories={data.categoryDistribution?.categories ?? []}
                  isLoading={isLoading}
                  title="Distribuição por Categoria"
                />
              </motion.div>

              {/* Top Paths */}
              <motion.div variants={fadeUp} className="h-full">
                <TopPathsSection
                  paths={data.topPaths?.paths ?? []}
                  isLoading={isLoading}
                  title="URLs e Caminhos Mais Acessados"
                  maxItems={10}
                />
              </motion.div>

              {/* Distraction Stats */}
              <motion.div variants={fadeUp} className="h-full">
                <DistractionSection
                  data={data.distractionStats}
                  isLoading={isLoading}
                  title="Análise de Distrações"
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
