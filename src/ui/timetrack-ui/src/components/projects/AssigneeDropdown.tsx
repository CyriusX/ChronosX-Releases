/**
 * AssigneeDropdown — inline popover for assigning a task to an org member.
 * Uses a portal to render the dropdown outside any overflow: hidden containers.
 */

import { useState, useRef, useEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { User, Check, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { listMembers } from '../../services/memberApi';
import { updateTask, type Task } from '../../services/projectsApi';
import { emitTaskUpdated } from '../../lib/appEvents';

interface OrgMember {
  userId: string;
  displayName: string;
  email: string;
}

function initials(name: string | null | undefined): string {
  if (!name) return '?';
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0][0]?.toUpperCase() ?? '?';
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function AssigneeDropdown({
  task,
  projectColor,
  onAssigned,
  children,
}: {
  task: Task;
  projectColor: string;
  /** Called after assignment changes so the parent can refetch. */
  onAssigned?: () => void;
  /** Optional custom trigger (defaults to inline assignee display). */
  children?: React.ReactNode;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [members, setMembers] = useState<OrgMember[]>([]);
  const [loading, setLoading] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);
  const triggerRef = useRef<HTMLDivElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);

  // Position the portal popover relative to the trigger
  const updatePos = useCallback(() => {
    if (!triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    setPos({
      top: rect.bottom + 4,
      left: rect.left,
    });
  }, []);

  // Load org members when opening
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    updatePos();
    listMembers()
      .then((res) => {
        if (!cancelled) {
          setMembers(
            (res.members ?? []).map((m: any) => ({
              userId: m.userId,
              displayName: m.displayName,
              email: m.email,
            })),
          );
        }
      })
      .catch(() => {
        if (!cancelled) setMembers([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, updatePos]);

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      const target = e.target as Node;
      if (
        triggerRef.current?.contains(target) ||
        popoverRef.current?.contains(target)
      ) {
        return;
      }
      setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  const handleSelect = async (userId: string | null) => {
    if (userId === task.assignedUserId) {
      setOpen(false);
      return;
    }
    setSaving(true);
    setSavingId(userId);
    const payload = {
      title: task.title,
      description: task.description ?? undefined,
      assignedUserId: userId,
      priority: task.priority,
      dueDate: task.dueDate,
    };
    console.log('[AssigneeDropdown] sending payload:', JSON.stringify(payload, null, 2));
    try {
      const updated = await updateTask(task.id, payload);
      emitTaskUpdated(updated);
      setOpen(false);
      onAssigned?.();
    } catch (err: any) {
      console.error('[AssigneeDropdown] updateTask FAILED:', {
        message: err?.message,
        status: err?.status,
        code: err?.code,
        payload,
      });
      setOpen(false);
      onAssigned?.();
    } finally {
      setSaving(false);
      setSavingId(null);
    }
  };

  const defaultTrigger = task.assignedUserDisplayName ? (
    <div className="flex items-center gap-1 text-[9px] text-[rgba(245,247,251,0.6)]">
      <div
        className="w-4 h-4 rounded-full flex items-center justify-center text-[8px] font-bold text-white"
        style={{ backgroundColor: projectColor }}
      >
        {initials(task.assignedUserDisplayName)}
      </div>
      <span className="truncate max-w-[80px]">{task.assignedUserDisplayName.split(' ')[0]}</span>
    </div>
  ) : (
    <div className="flex items-center gap-1 text-[9px] text-[rgba(245,247,251,0.3)]">
      <User className="w-3 h-3" />
      {t('projects.unassigned')}
    </div>
  );

  return (
    <>
      <div
        ref={triggerRef}
        className="inline-flex items-center cursor-pointer hover:bg-[rgba(255,255,255,0.06)] rounded px-1 py-0.5 -ml-1 transition-colors"
        onClick={(e) => {
          e.stopPropagation();
          setOpen((v) => !v);
        }}
        onPointerDown={(e) => e.stopPropagation()}
      >
        {children ?? defaultTrigger}
      </div>

      {open && pos && createPortal(
        <div
          ref={popoverRef}
          className="fixed z-[9999] min-w-[200px] max-h-[220px] overflow-y-auto bg-[#13151f] border border-[rgba(255,255,255,0.12)] rounded-lg shadow-xl py-1"
          style={{ top: pos.top, left: pos.left }}
          onClick={(e) => e.stopPropagation()}
          onPointerDown={(e) => e.stopPropagation()}
        >
          {loading && (
            <div className="flex items-center justify-center py-3">
              <Loader2 className="w-3 h-3 animate-spin text-[rgba(245,247,251,0.5)]" />
            </div>
          )}

          {!loading && members.length === 0 && (
            <div className="px-3 py-2 text-[10px] text-[rgba(245,247,251,0.4)] italic">
              {t('settings.members.noMembers')}
            </div>
          )}

          {!loading && (
            <>
              {/* Unassign option */}
              <button
                onClick={() => handleSelect(null)}
                disabled={saving}
                className="w-full flex items-center gap-2 px-3 py-1.5 text-[11px] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.06)] transition-colors disabled:opacity-50"
              >
                <div className="w-4 h-4 rounded-full flex items-center justify-center bg-[rgba(255,255,255,0.06)]">
                  <User className="w-2.5 h-2.5 text-[rgba(245,247,251,0.4)]" />
                </div>
                <span className="flex-1 text-left">{t('projects.nobody')}</span>
                {!task.assignedUserId && <Check className="w-3 h-3 text-[rgba(245,247,251,0.4)]" />}
                {saving && savingId === null && <Loader2 className="w-3 h-3 animate-spin text-[rgba(245,247,251,0.5)]" />}
              </button>

              {members.length > 0 && (
                <div className="h-px bg-[rgba(255,255,255,0.06)] my-1" />
              )}

              {members.map((m) => {
                const isSelected = m.userId === task.assignedUserId;
                const isSaving = saving && savingId === m.userId;
                return (
                  <button
                    key={m.userId}
                    onClick={() => handleSelect(m.userId)}
                    disabled={saving}
                    className="w-full flex items-center gap-2 px-3 py-1.5 text-[11px] text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.06)] transition-colors disabled:opacity-50"
                  >
                    <div
                      className="w-4 h-4 rounded-full flex items-center justify-center text-[8px] font-bold text-white shrink-0"
                      style={{ backgroundColor: projectColor }}
                    >
                      {initials(m.displayName)}
                    </div>
                    <span className="flex-1 text-left truncate">{m.displayName}</span>
                    {isSelected && !isSaving && <Check className="w-3 h-3 text-[rgba(139,92,246,0.8)]" />}
                    {isSaving && <Loader2 className="w-3 h-3 animate-spin text-[rgba(245,247,251,0.5)]" />}
                  </button>
                );
              })}
            </>
          )}
        </div>,
        document.body,
      )}
    </>
  );
}
