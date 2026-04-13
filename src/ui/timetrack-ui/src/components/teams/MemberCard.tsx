/**
 * MemberCard — displays a single team member's status in the Teams page grid.
 * Duration and productivity data come pre-corrected from the useTeamStatus hook.
 * Only task data is lazily fetched on mount.
 */

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { Clock, CheckSquare, ListTodo, ChevronRight } from 'lucide-react';
import { listUserTasks, type Task } from '../../services/projectsApi';
import type { TeamMemberStatus } from '../../types/member';
import { getMemberGradient } from '../dashboard/shared/styles';
import { fadeUp } from '../../lib/animation';

interface MemberCardProps {
  member: TeamMemberStatus;
  index: number;
  onSelect: (userId: string) => void;
}

function formatDuration(seconds: number): string {
  if (!seconds || seconds <= 0) return '0m';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${String(m).padStart(2, '0')}m`;
  return `${m}m`;
}

function getInitials(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map(w => w[0].toUpperCase())
    .join('');
}

// roleLabel is resolved via i18n inside the component

function roleColor(role: string): string {
  switch (role) {
    case 'Admin': return 'text-[#8B5CF6] bg-[rgba(139,92,246,0.12)] border-[rgba(139,92,246,0.25)]';
    case 'Gestor': return 'text-[#22D3EE] bg-[rgba(34,211,238,0.1)] border-[rgba(34,211,238,0.2)]';
    default: return 'text-[rgba(245,247,251,0.5)] bg-[rgba(255,255,255,0.05)] border-[rgba(255,255,255,0.1)]';
  }
}

function productivityColor(pct: number): string {
  if (pct >= 70) return '#05df72';
  if (pct >= 40) return '#fbbf24';
  return '#f87171';
}

function formatRelativeTime(isoDate: string | null | undefined): string | null {
  if (!isoDate) return null;
  const diff = (Date.now() - new Date(isoDate).getTime()) / 1000;
  if (diff < 60) return 'Now';
  if (diff < 3600) {
    const m = Math.floor(diff / 60);
    return `${m}min ago`;
  }
  if (diff < 86400) {
    const h = Math.floor(diff / 3600);
    return `${h}h ago`;
  }
  return `${Math.floor(diff / 86400)}d ago`;
}

export function MemberCard({ member, index, onSelect }: MemberCardProps) {
  const { t } = useTranslation();

  // Duration and productivity are pre-corrected by useTeamStatus hook
  const totalDuration = member.todayDurationSeconds;
  const productivityPct = member.productivityRatio != null
    ? Math.round(member.productivityRatio * 100)
    : null;

  const [inProgressTask, setInProgressTask] = useState<Task | null>(null);
  const [todoCount, setTodoCount] = useState<number>(0);
  const [doneCount, setDoneCount] = useState<number>(0);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const tasksRes = await listUserTasks(member.userId, true);
        if (cancelled) return;
        const tasks = tasksRes.tasks ?? [];
        setInProgressTask(tasks.find(t => t.status === 'InProgress') ?? null);
        setTodoCount(tasks.filter(t => t.status === 'Todo').length);
        setDoneCount(tasks.filter(t => t.status === 'Done').length);
      } catch {
        // Silently degrade
      }
    }

    load();
    return () => { cancelled = true; };
  }, [member.userId]);

  const gradient = getMemberGradient(member.displayName);
  const prodColor = productivityPct !== null ? productivityColor(productivityPct) : undefined;

  return (
    <motion.div
      variants={fadeUp}
      custom={index}
      transition={{ delay: index * 0.06 }}
      className="glass-card-interactive rounded-[22px] overflow-hidden cursor-pointer group"
      onClick={() => onSelect(member.userId)}
    >
      {/* Header */}
      <div className="p-4 pb-3 flex items-start gap-3">
        {/* Avatar */}
        <div className={`w-11 h-11 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center flex-shrink-0 shadow-[0_4px_12px_rgba(0,0,0,0.3)]`}>
          <span className="text-white text-[13px] font-bold">{getInitials(member.displayName)}</span>
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-[13px] font-semibold text-[#f5f7fb] truncate">{member.displayName}</span>
            <ChevronRight className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)] group-hover:text-[rgba(245,247,251,0.6)] transition-colors flex-shrink-0" />
          </div>
          <span className={`mt-0.5 inline-flex text-[10px] font-medium px-1.5 py-0.5 rounded-md border ${roleColor(member.role)}`}>
            {member.role === 'Admin' ? t('teams.adminRole') : member.role === 'Gestor' ? t('teams.gestorRole') : t('teams.memberRole')}
          </span>
        </div>

        {/* Online status + last sync */}
        <div className="flex flex-col items-end gap-0.5 flex-shrink-0">
          <div className="flex items-center gap-1.5">
            <div className="relative">
              {member.isTracking && (
                <motion.div
                  className="absolute inset-0 rounded-full bg-[#05df72]"
                  animate={{ opacity: [0.4, 0.1, 0.4], scale: [1, 1.8, 1] }}
                  transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
                />
              )}
              <div className={`w-2 h-2 rounded-full relative ${member.isTracking ? 'bg-[#05df72]' : 'bg-[rgba(245,247,251,0.2)]'}`} />
            </div>
            <span className={`text-[10px] ${member.isTracking ? 'text-[#05df72]' : 'text-[rgba(245,247,251,0.3)]'}`}>
              {member.isTracking ? t('teams.online') : t('teams.offline')}
            </span>
          </div>
          {member.lastSyncAt && (
            <span className="text-[9px] text-[rgba(245,247,251,0.25)]">
              {t('teams.lastSync', { time: formatRelativeTime(member.lastSyncAt) })}
            </span>
          )}
        </div>
      </div>

      {/* Stats row */}
      <div className="px-4 py-2.5 border-t border-[rgba(255,255,255,0.05)] flex items-center gap-0 divide-x divide-[rgba(255,255,255,0.05)]">
        <div className="flex-1 flex flex-col items-center gap-0.5 px-2 first:pl-0 last:pr-0">
          <div className="flex items-center gap-1">
            <Clock className="w-3 h-3 text-[rgba(245,247,251,0.4)]" />
            <span className="text-[12px] font-semibold text-[#f5f7fb]">{formatDuration(totalDuration)}</span>
          </div>
          <span className="text-[9px] text-[rgba(245,247,251,0.35)] uppercase tracking-wide">{t('teams.today')}</span>
        </div>

        <div className="flex-1 flex flex-col items-center gap-0.5 px-2">
          <span className="text-[12px] font-semibold" style={{ color: prodColor ?? 'rgba(245,247,251,0.4)' }}>
            {productivityPct !== null ? `${productivityPct}%` : '—'}
          </span>
          <span className="text-[9px] text-[rgba(245,247,251,0.35)] uppercase tracking-wide">{t('teams.productivity')}</span>
        </div>

        <div className="flex-1 flex flex-col items-center gap-0.5 px-2">
          <div className="flex items-center gap-1">
            <CheckSquare className="w-3 h-3 text-[rgba(245,247,251,0.4)]" />
            <span className="text-[12px] font-semibold text-[#f5f7fb]">{doneCount}</span>
          </div>
          <span className="text-[9px] text-[rgba(245,247,251,0.35)] uppercase tracking-wide">{t('teams.done')}</span>
        </div>
      </div>

      {/* Current task & todo count */}
      <div className="px-4 py-3 border-t border-[rgba(255,255,255,0.05)] space-y-2">
        {inProgressTask ? (
          <div className="flex items-start gap-2 rounded-lg bg-[rgba(5,223,114,0.06)] border border-[rgba(5,223,114,0.15)] px-2.5 py-2">
            <div className="w-1.5 h-1.5 rounded-full bg-[#05df72] mt-1.5 flex-shrink-0" />
            <div className="flex-1 min-w-0">
              <p className="text-[11px] text-[rgba(245,247,251,0.9)] truncate leading-tight">{inProgressTask.title}</p>
              <p className="text-[10px] text-[rgba(245,247,251,0.4)] mt-0.5 truncate">{inProgressTask.projectName}</p>
            </div>
            <span className="text-[9px] text-[#05df72] font-medium flex-shrink-0 mt-0.5">{t('teams.inProgress')}</span>
          </div>
        ) : (
          <div className="flex items-center gap-2 rounded-lg bg-[rgba(255,255,255,0.03)] px-2.5 py-2">
            <div className="w-1.5 h-1.5 rounded-full bg-[rgba(245,247,251,0.15)] flex-shrink-0" />
            <span className="text-[11px] text-[rgba(245,247,251,0.35)] italic">{t('teams.noTaskInProgress')}</span>
          </div>
        )}

        <div className="flex items-center gap-1.5 px-1">
          <ListTodo className="w-3.5 h-3.5 text-[rgba(245,247,251,0.35)]" />
          <span className="text-[11px] text-[rgba(245,247,251,0.5)]">
            {todoCount === 0 ? t('teams.noTodoTasks') : t('teams.todoTasksCount', { count: todoCount })}
          </span>
        </div>
      </div>
    </motion.div>
  );
}
