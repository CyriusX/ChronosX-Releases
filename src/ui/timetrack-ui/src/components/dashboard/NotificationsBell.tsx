/**
 * NotificationsBell — header bell icon with unread badge and inline dropdown.
 * Polls the backend inbox every 60s and shows recent notifications.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Bell, Check, Loader2 } from 'lucide-react';
import {
  listMyNotifications,
  markNotificationRead,
  markAllNotificationsRead,
  type NotificationItem,
} from '../../services/projectsApi';

const POLL_INTERVAL_MS = 60_000;

function formatRelative(iso: string): string {
  const then = new Date(iso).getTime();
  const diffSec = Math.floor((Date.now() - then) / 1000);
  if (diffSec < 60) return 'agora';
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}min`;
  if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h`;
  return `${Math.floor(diffSec / 86400)}d`;
}

function kindIcon(kind: string): string {
  switch (kind) {
    case 'TaskAssigned': return '📋';
    case 'TaskUnassigned': return '❌';
    case 'TaskUpdated': return '✏️';
    case 'ProjectMembershipChanged': return '👥';
    default: return '🔔';
  }
}

export function NotificationsBell() {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchNotifications = useCallback(async () => {
    try {
      const res = await listMyNotifications(false, 20);
      setItems(res.notifications ?? []);
      setUnreadCount(res.unreadCount ?? 0);
    } catch (err) {
      // Non-critical — keep previous state
    }
  }, []);

  useEffect(() => {
    fetchNotifications();
    pollRef.current = setInterval(fetchNotifications, POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; }
    };
  }, [fetchNotifications]);

  // Close dropdown on outside click
  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  const handleMarkRead = async (id: string) => {
    setBusy(true);
    try {
      await markNotificationRead(id);
      await fetchNotifications();
    } finally {
      setBusy(false);
    }
  };

  const handleMarkAllRead = async () => {
    setBusy(true);
    try {
      await markAllNotificationsRead();
      await fetchNotifications();
    } finally {
      setBusy(false);
    }
  };

  return (
    <div ref={wrapperRef} className="relative">
      <motion.button
        onClick={() => setOpen(!open)}
        whileHover={{ scale: 1.08 }}
        whileTap={{ scale: 0.95 }}
        className="relative w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] backdrop-blur-sm flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
        title={unreadCount > 0 ? `${unreadCount} não lidas` : 'Notificações'}
      >
        <Bell
          className={`w-4 h-4 ${unreadCount > 0 ? 'text-[#c4b5fd]' : 'text-[rgba(245,247,251,0.6)]'}`}
        />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -right-1 min-w-[16px] h-4 px-1 rounded-full bg-[#f87171] flex items-center justify-center animate-pulse">
            <span className="text-[9px] font-bold text-white leading-none">
              {unreadCount > 9 ? '9+' : unreadCount}
            </span>
          </span>
        )}
      </motion.button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: -8, scale: 0.95 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -8, scale: 0.95 }}
            transition={{ duration: 0.15 }}
            className="absolute top-full right-0 mt-2 w-[360px] max-w-[calc(100vw-2rem)] bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl z-50"
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-[rgba(255,255,255,0.05)]">
              <span className="text-[12px] font-semibold text-[#f5f7fb]">Notificações</span>
              {unreadCount > 0 && (
                <button
                  onClick={handleMarkAllRead}
                  disabled={busy}
                  className="flex items-center gap-1 text-[10px] text-[#c4b5fd] hover:text-[#a78bfa] disabled:opacity-40 transition-colors"
                >
                  {busy ? <Loader2 className="w-3 h-3 animate-spin" /> : <Check className="w-3 h-3" />}
                  Marcar todas
                </button>
              )}
            </div>

            <div className="max-h-[320px] overflow-y-auto">
              {items.length === 0 ? (
                <div className="text-center py-8">
                  <Bell className="w-6 h-6 text-[rgba(245,247,251,0.15)] mx-auto mb-2" />
                  <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Nenhuma notificação</p>
                </div>
              ) : (
                items.map((n) => {
                  const isUnread = !n.readAt;
                  return (
                    <button
                      key={n.id}
                      onClick={() => isUnread && handleMarkRead(n.id)}
                      disabled={!isUnread}
                      className={`w-full flex items-start gap-3 px-4 py-3 text-left transition-colors border-b border-[rgba(255,255,255,0.03)] last:border-b-0 ${
                        isUnread ? 'bg-[rgba(139,92,246,0.04)] hover:bg-[rgba(139,92,246,0.08)]' : 'opacity-60'
                      }`}
                    >
                      <span className="text-[14px] flex-shrink-0 mt-0.5">{kindIcon(n.kind)}</span>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-0.5">
                          <span className="text-[11px] font-semibold text-[#f5f7fb] truncate">{n.title}</span>
                          {isUnread && (
                            <span className="w-1.5 h-1.5 rounded-full bg-[#c4b5fd] flex-shrink-0" />
                          )}
                        </div>
                        <p className="text-[10px] text-[rgba(245,247,251,0.55)] truncate">{n.body}</p>
                        <p className="text-[9px] text-[rgba(245,247,251,0.35)] mt-0.5">{formatRelative(n.createdAt)}</p>
                      </div>
                    </button>
                  );
                })
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
