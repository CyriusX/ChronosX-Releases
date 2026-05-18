/**
 * NotificationsBell — header bell icon with unread badge and inline dropdown.
 * Merges AgentNotificationInbox (task notifications) and SmartAlerts (AI alerts).
 * Tabs: Inbox | Alerts (personal) | Team (manager/admin only)
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import { Bell, Check, Loader2, X, Play, BarChart3, Users } from 'lucide-react';
import { useIpc } from '../../hooks/useIpc';
import { usePermissions } from '../../hooks/usePermissions';
import {
  listMyNotifications,
  markNotificationRead,
  markAllNotificationsRead,
  type NotificationItem,
} from '../../services/projectsApi';
import {
  getAlerts,
  getUnreadAlertCount,
  markAlertRead,
  markAllAlertsRead,
  markAlertActed,
  dismissAlert,
  getTeamAlerts,
  type SmartAlertItem,
  type TeamAlertItem,
} from '../../services/alertsApi';

const POLL_INTERVAL_MS = 60_000;

type Tab = 'inbox' | 'alerts' | 'team';

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
    case 'DeadlineToday': return '⏰';
    default: return '🔔';
  }
}

function kindAccent(kind: string): string {
  switch (kind) {
    case 'DeadlineToday': return 'bg-[rgba(251,191,36,0.08)] hover:bg-[rgba(251,191,36,0.14)] border-l-2 border-l-[#fbbf24]';
    default: return 'bg-[rgba(139,92,246,0.04)] hover:bg-[rgba(139,92,246,0.08)]';
  }
}

function alertSeverityIcon(severity: string): string {
  switch (severity) {
    case 'warning': return '⚠️';
    case 'critical': return '🔴';
    default: return '💡';
  }
}

function alertSeverityAccent(severity: string): string {
  switch (severity) {
    case 'warning': return 'bg-[rgba(251,191,36,0.06)] hover:bg-[rgba(251,191,36,0.12)] border-l-2 border-l-[#fbbf24]';
    case 'critical': return 'bg-[rgba(239,68,68,0.06)] hover:bg-[rgba(239,68,68,0.12)] border-l-2 border-l-[#ef4444]';
    default: return 'bg-[rgba(139,92,246,0.04)] hover:bg-[rgba(139,92,246,0.08)]';
  }
}

export function NotificationsBell() {
  const { t } = useTranslation();
  const { subscribeToEvent } = useIpc();
  const { canManageTeam } = usePermissions();

  // Inbox state
  const [inboxItems, setInboxItems] = useState<NotificationItem[]>([]);
  const [inboxUnread, setInboxUnread] = useState(0);

  // Smart alerts state
  const [alerts, setAlerts] = useState<SmartAlertItem[]>([]);
  const [alertUnread, setAlertUnread] = useState(0);

  // Team alerts state
  const [teamAlerts, setTeamAlerts] = useState<TeamAlertItem[]>([]);

  const [activeTab, setActiveTab] = useState<Tab>('inbox');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const totalUnread = inboxUnread + alertUnread;

  const fetchInbox = useCallback(async () => {
    try {
      const res = await listMyNotifications(false, 20);
      setInboxItems(res.notifications ?? []);
      setInboxUnread(res.unreadCount ?? 0);
    } catch { /* non-critical */ }
  }, []);

  const fetchAlerts = useCallback(async () => {
    try {
      const [listRes, countRes] = await Promise.allSettled([
        getAlerts({ unreadOnly: true, pageSize: 20 }),
        getUnreadAlertCount(),
      ]);
      if (listRes.status === 'fulfilled') setAlerts(listRes.value.alerts);
      if (countRes.status === 'fulfilled') setAlertUnread(countRes.value.count);
    } catch { /* non-critical */ }
  }, []);

  const fetchTeamAlerts = useCallback(async () => {
    if (!canManageTeam) return;
    try {
      const res = await getTeamAlerts({ pageSize: 20 });
      setTeamAlerts(res.alerts);
    } catch { /* non-critical */ }
  }, [canManageTeam]);

  const fetchAll = useCallback(async () => {
    await Promise.allSettled([fetchInbox(), fetchAlerts()]);
  }, [fetchInbox, fetchAlerts]);

  useEffect(() => {
    fetchAll();
    pollRef.current = setInterval(fetchAll, POLL_INTERVAL_MS);
    return () => { if (pollRef.current) clearInterval(pollRef.current); };
  }, [fetchAll]);

  useEffect(() => {
    const unsub = subscribeToEvent('notificationReceived', () => fetchInbox());
    return unsub;
  }, [subscribeToEvent, fetchInbox]);

  // Fetch team alerts on tab switch
  useEffect(() => {
    if (open && activeTab === 'team') fetchTeamAlerts();
  }, [open, activeTab, fetchTeamAlerts]);

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  // Inbox actions
  const handleMarkInboxRead = async (id: string) => {
    setBusy(true);
    try { await markNotificationRead(id); await fetchInbox(); } finally { setBusy(false); }
  };

  const handleMarkAllInboxRead = async () => {
    setBusy(true);
    try { await markAllNotificationsRead(); await fetchInbox(); } finally { setBusy(false); }
  };

  // Alert actions
  const handleMarkAlertRead = async (id: string) => {
    setBusy(true);
    try { await markAlertRead(id); await fetchAlerts(); } finally { setBusy(false); }
  };

  const handleMarkAllAlertsRead = async () => {
    setBusy(true);
    try { await markAllAlertsRead(); await fetchAlerts(); } finally { setBusy(false); }
  };

  const handleActOnAlert = async (id: string, actionType: string | null) => {
    setBusy(true);
    try {
      await markAlertActed(id);
      // Dispatch IPC action if applicable
      if (actionType === 'start_pomodoro') {
        try {
          const { getIpcService } = await import('../../services');
          const ipc = getIpcService();
          if (ipc.isConnected) ipc.sendCommand('startFocusMode');
        } catch { /* IPC not available */ }
      }
      await fetchAlerts();
    } finally { setBusy(false); }
  };

  const handleDismissAlert = async (id: string) => {
    setBusy(true);
    try { await dismissAlert(id); await fetchAlerts(); } finally { setBusy(false); }
  };

  const tabs: { key: Tab; label: string; badge: number }[] = [
    { key: 'inbox', label: t('alerts.tabs.inbox', 'Inbox'), badge: inboxUnread },
    { key: 'alerts', label: t('alerts.tabs.alerts', 'Alerts'), badge: alertUnread },
    ...(canManageTeam ? [{ key: 'team' as Tab, label: t('alerts.tabs.team', 'Team'), badge: teamAlerts.length }] : []),
  ];

  return (
    <div ref={wrapperRef} className="relative">
      <motion.button
        onClick={() => setOpen(!open)}
        whileHover={{ scale: 1.08 }}
        whileTap={{ scale: 0.95 }}
        className="relative w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] backdrop-blur-sm flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
        title={totalUnread > 0 ? t('dashboard.unreadCount', { count: totalUnread }) : t('dashboard.notifications')}
      >
        <Bell className={`w-4 h-4 ${totalUnread > 0 ? 'text-[#c4b5fd]' : 'text-[rgba(245,247,251,0.6)]'}`} />
        {totalUnread > 0 && (
          <span className="absolute -top-1 -right-1 min-w-[16px] h-4 px-1 rounded-full bg-[#f87171] flex items-center justify-center animate-pulse">
            <span className="text-[9px] font-bold text-white leading-none">{totalUnread > 9 ? '9+' : totalUnread}</span>
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
            className="absolute top-full right-0 mt-2 w-[400px] max-w-[calc(100vw-2rem)] bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl z-50"
          >
            {/* Header */}
            <div className="flex items-center justify-between px-4 py-3 border-b border-[rgba(255,255,255,0.05)]">
              <span className="text-[12px] font-semibold text-[#f5f7fb]">{t('dashboard.notifications')}</span>
              <div className="flex items-center gap-2">
                {(activeTab === 'inbox' && inboxUnread > 0) && (
                  <button onClick={handleMarkAllInboxRead} disabled={busy} className="flex items-center gap-1 text-[10px] text-[#c4b5fd] hover:text-[#a78bfa] disabled:opacity-40 transition-colors">
                    {busy ? <Loader2 className="w-3 h-3 animate-spin" /> : <Check className="w-3 h-3" />}
                    {t('dashboard.markAll')}
                  </button>
                )}
                {(activeTab === 'alerts' && alertUnread > 0) && (
                  <button onClick={handleMarkAllAlertsRead} disabled={busy} className="flex items-center gap-1 text-[10px] text-[#c4b5fd] hover:text-[#a78bfa] disabled:opacity-40 transition-colors">
                    {busy ? <Loader2 className="w-3 h-3 animate-spin" /> : <Check className="w-3 h-3" />}
                    {t('dashboard.markAll')}
                  </button>
                )}
              </div>
            </div>

            {/* Tabs */}
            <div className="flex border-b border-[rgba(255,255,255,0.05)]">
              {tabs.map((tab) => (
                <button
                  key={tab.key}
                  onClick={() => setActiveTab(tab.key)}
                  className={`flex-1 flex items-center justify-center gap-1.5 px-3 py-2 text-[11px] font-medium transition-colors relative ${
                    activeTab === tab.key ? 'text-[#c4b5fd]' : 'text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.6)]'
                  }`}
                >
                  {tab.key === 'team' && <Users className="w-3 h-3" />}
                  {tab.label}
                  {tab.badge > 0 && (
                    <span className="min-w-[14px] h-3.5 px-0.5 rounded-full bg-[#f87171] flex items-center justify-center">
                      <span className="text-[8px] font-bold text-white leading-none">{tab.badge > 99 ? '99+' : tab.badge}</span>
                    </span>
                  )}
                  {activeTab === tab.key && (
                    <motion.div layoutId="notif-tab" className="absolute bottom-0 left-2 right-2 h-[2px] bg-[#8B5CF6] rounded-full" />
                  )}
                </button>
              ))}
            </div>

            {/* Content */}
            <div className="max-h-[360px] overflow-y-auto">
              <AnimatePresence mode="wait">
                {activeTab === 'inbox' && (
                  <motion.div key="inbox" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
                    {inboxItems.length === 0 ? (
                      <EmptyState />
                    ) : (
                      inboxItems.map((n) => {
                        const isUnread = !n.readAt;
                        return (
                          <button
                            key={n.id}
                            onClick={() => isUnread && handleMarkInboxRead(n.id)}
                            disabled={!isUnread}
                            className={`w-full flex items-start gap-3 px-4 py-3 text-left transition-colors border-b border-[rgba(255,255,255,0.03)] last:border-b-0 ${
                              isUnread ? kindAccent(n.kind) : 'opacity-60'
                            }`}
                          >
                            <span className="text-[14px] flex-shrink-0 mt-0.5">{kindIcon(n.kind)}</span>
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2 mb-0.5">
                                <span className="text-[11px] font-semibold text-[#f5f7fb] truncate">{n.title}</span>
                                {isUnread && <span className="w-1.5 h-1.5 rounded-full bg-[#c4b5fd] flex-shrink-0" />}
                              </div>
                              <p className="text-[10px] text-[rgba(245,247,251,0.55)] truncate">{n.body}</p>
                              <p className="text-[9px] text-[rgba(245,247,251,0.35)] mt-0.5">{formatRelative(n.createdAt)}</p>
                            </div>
                          </button>
                        );
                      })
                    )}
                  </motion.div>
                )}

                {activeTab === 'alerts' && (
                  <motion.div key="alerts" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
                    {alerts.length === 0 ? (
                      <EmptyState />
                    ) : (
                      alerts.map((a) => (
                        <div
                          key={a.id}
                          className={`flex items-start gap-3 px-4 py-3 border-b border-[rgba(255,255,255,0.03)] last:border-b-0 ${alertSeverityAccent(a.severity)}`}
                        >
                          <span className="text-[14px] flex-shrink-0 mt-0.5">{alertSeverityIcon(a.severity)}</span>
                          <div className="flex-1 min-w-0">
                            <p className="text-[11px] text-[#f5f7fb] leading-relaxed">{a.message}</p>
                            <p className="text-[9px] text-[rgba(245,247,251,0.35)] mt-1">{formatRelative(a.createdAt)}</p>
                            <div className="flex items-center gap-2 mt-2">
                              {a.actionType === 'start_pomodoro' && (
                                <button
                                  onClick={() => handleActOnAlert(a.id, a.actionType)}
                                  disabled={busy}
                                  className="flex items-center gap-1 px-2 py-1 rounded-md bg-[rgba(139,92,246,0.15)] text-[#c4b5fd] text-[10px] font-medium hover:bg-[rgba(139,92,246,0.25)] disabled:opacity-40 transition-colors"
                                >
                                  <Play className="w-3 h-3" />
                                  {t('alerts.actions.startPomodoro', 'Iniciar Foco')}
                                </button>
                              )}
                              {a.actionType === 'view_report' && (
                                <button
                                  onClick={() => { handleActOnAlert(a.id, a.actionType); setOpen(false); }}
                                  disabled={busy}
                                  className="flex items-center gap-1 px-2 py-1 rounded-md bg-[rgba(139,92,246,0.15)] text-[#c4b5fd] text-[10px] font-medium hover:bg-[rgba(139,92,246,0.25)] disabled:opacity-40 transition-colors"
                                >
                                  <BarChart3 className="w-3 h-3" />
                                  {t('alerts.actions.viewReport', 'Ver Relatório')}
                                </button>
                              )}
                              <button
                                onClick={() => handleMarkAlertRead(a.id)}
                                disabled={busy}
                                className="flex items-center gap-1 px-2 py-1 rounded-md text-[10px] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.04)] disabled:opacity-40 transition-colors"
                              >
                                <Check className="w-3 h-3" />
                              </button>
                              <button
                                onClick={() => handleDismissAlert(a.id)}
                                disabled={busy}
                                className="flex items-center gap-1 px-2 py-1 rounded-md text-[10px] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.04)] disabled:opacity-40 transition-colors"
                              >
                                <X className="w-3 h-3" />
                              </button>
                            </div>
                          </div>
                        </div>
                      ))
                    )}
                  </motion.div>
                )}

                {activeTab === 'team' && canManageTeam && (
                  <motion.div key="team" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
                    {teamAlerts.length === 0 ? (
                      <EmptyState />
                    ) : (
                      teamAlerts.map((a) => (
                        <div
                          key={a.id}
                          className={`flex items-start gap-3 px-4 py-3 border-b border-[rgba(255,255,255,0.03)] last:border-b-0 ${alertSeverityAccent(a.severity)}`}
                        >
                          <span className="text-[14px] flex-shrink-0 mt-0.5">{alertSeverityIcon(a.severity)}</span>
                          <div className="flex-1 min-w-0">
                            <p className="text-[11px] text-[#f5f7fb] leading-relaxed">{a.message}</p>
                            <p className="text-[9px] text-[rgba(245,247,251,0.35)] mt-1">{formatRelative(a.createdAt)}</p>
                            {a.actionType === 'view_report' && (
                              <button
                                onClick={() => setOpen(false)}
                                className="flex items-center gap-1 mt-2 px-2 py-1 rounded-md bg-[rgba(139,92,246,0.15)] text-[#c4b5fd] text-[10px] font-medium hover:bg-[rgba(139,92,246,0.25)] transition-colors"
                              >
                                <BarChart3 className="w-3 h-3" />
                                {t('alerts.actions.viewReport', 'Ver Relatório')}
                              </button>
                            )}
                          </div>
                        </div>
                      ))
                    )}
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

function EmptyState() {
  const { t } = useTranslation();
  return (
    <div className="text-center py-8">
      <Bell className="w-6 h-6 text-[rgba(245,247,251,0.15)] mx-auto mb-2" />
      <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{t('dashboard.noNotifications')}</p>
    </div>
  );
}
