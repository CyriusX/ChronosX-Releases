/**
 * ProjectTasksAccordion — Displays projects and their tasks in a collapsible accordion.
 *
 * SRP: Only handles rendering of grouped project/task data.
 * DIP: Receives all data via props — no direct API calls.
 * OCP: Can be extended with new task metadata without modifying the component.
 */

import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import {
  Briefcase,
  Clock,
  DollarSign,
  ListTodo,
  RefreshCw,
  ChevronRight,
} from 'lucide-react';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ProjectTasksAccordionProps {
  tasks: TaskItem[];
  projects: ProjectItem[];
  loading: boolean;
  expanded: Set<string>;
  onToggle: (id: string) => void;
}

export interface TaskItem {
  id: string;
  projectId: string;
  projectName: string;
  projectColor: string;
  title: string;
  status: 'Todo' | 'InProgress' | 'InReview' | 'Done';
  totalSecondsWorked: number;
}

export interface ProjectItem {
  id: string;
  isBillable: boolean;
  hourlyRate: number | null;
  currency: string | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

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

// ─── Component ────────────────────────────────────────────────────────────────

export function ProjectTasksAccordion({
  tasks,
  projects,
  loading,
  expanded,
  onToggle,
}: ProjectTasksAccordionProps) {
  const { t } = useTranslation();
  const projectMap = new Map(projects.map((p) => [p.id, p]));

  // Group tasks by project
  const byProject = new Map<string, TaskItem[]>();
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
        <h2 className="text-[14px] font-semibold text-[#f5f7fb]">{t('reports.projectsAndTasks')}</h2>
        <span className="ml-auto text-[11px] text-[rgba(245,247,251,0.4)]">{t('reports.totalAccumulatedTime')}</span>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-10">
          <RefreshCw className="w-4 h-4 text-[#8B5CF6] animate-spin" />
        </div>
      ) : sorted.length === 0 ? (
        <div className="px-5 py-8 text-center text-[12px] text-[rgba(245,247,251,0.35)] italic">
          {t('reports.noTaskFound')}
        </div>
      ) : (
        <div className="divide-y divide-[rgba(255,255,255,0.04)]">
          {sorted.map(([projectId, ptasks]) => {
            const project = projectMap.get(projectId);
            const projectName = ptasks[0]?.projectName ?? t('reports.unknownProject');
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
                        {todoCount} {t('reports.toDo')}
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
                                  {task.status === 'Done' ? t('reports.done') : task.status === 'InProgress' ? t('reports.inProgress') : t('reports.toDoStatus')}
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
