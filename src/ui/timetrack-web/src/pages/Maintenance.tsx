/**
 * Maintenance — SysAdmin-only page for monitoring agent machine metrics.
 *
 * Displays CPU, Memory, and Disk usage for selected devices.
 * Polls every 10s when a device is selected.
 */

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Monitor, RefreshCw, Cpu, HardDrive, MemoryStick, ShieldAlert, ScrollText, ChevronRight, Power, Play, Square, Zap, Bell, X, Clock, Wifi, Globe, Trash2, User, AlertTriangle, Download } from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { useHealthAlertStore } from '../stores/healthAlertStore';
import { usePermissions } from '../hooks/usePermissions';
import { Navigate, useSearchParams } from 'react-router-dom';
import {
  listOrgDevices,
  getDeviceMetrics,
  getDeviceEvents,
  getDeviceInfo,
  setDeviceDevToolsAccess,
  sendRemoteCommand,
  getCommandHistory,
  clearDeviceEvents,
  clearAllEvents,
  getHealthSummary,
  deleteDevice,
  type DeviceListItem,
  type DeviceMetricsResponse,
  type MetricsHistoryPoint,
  type DeviceEventItem,
  type DeviceInfoResponse,
  type CommandHistoryItem,
  type HealthSummaryResponse,
  type HealthAlertItem,
} from '../services/maintenanceApi';
import { getPlatformHealth, listPlatformEvents, type PlatformEventLogDto, type PlatformHealthDto } from '../services/platformEventsApi';
import { getMaintenanceLogPrefs } from './Settings';
import { LineChart, Line, XAxis, ResponsiveContainer, Tooltip } from 'recharts';

const METRICS_POLL_INTERVAL_MS = 10_000;

function formatLastSeen(isoString: string | null): string {
  if (!isoString) return 'nunca';
  const d = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return 'agora';
  if (diffMin < 60) return `ha ${diffMin}min`;
  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `ha ${diffHours}h`;
  return d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
}

function formatUptime(seconds: number): string {
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (d > 0) return `${d}d ${h}h`;
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}


function TrackingStateLed({ state }: { state: string | null }) {
  if (!state || state === 'stopped' || state === 'idle') {
    return (
      <span className="flex items-center gap-1">
        <span className="inline-block w-1.5 h-1.5 rounded-full bg-[rgba(248,113,113,0.7)]" />
        <span className="text-[8px] text-[rgba(248,113,113,0.7)]">parado</span>
      </span>
    );
  }
  if (state === 'running') {
    return (
      <span className="flex items-center gap-1">
        <span className="inline-block w-1.5 h-1.5 rounded-full bg-[#05df72] animate-pulse" />
        <span className="text-[8px] text-[#05df72]">tracking</span>
      </span>
    );
  }
  if (state === 'paused') {
    return (
      <span className="flex items-center gap-1">
        <span className="inline-block w-1.5 h-1.5 rounded-full bg-[#fbbf24]" />
        <span className="text-[8px] text-[#fbbf24]">pausado</span>
      </span>
    );
  }
  return null;
}

function getBarColor(percent: number): string {
  if (percent < 60) return '#05df72';
  if (percent < 80) return '#fbbf24';
  return '#f87171';
}


interface IssueSpec {
  badge: string;           // short label shown inline
  headline: string;        // one-line summary
  subtext: string;         // secondary detail shown collapsed
  component: string;       // which component is affected
  what: string;            // plain-English explanation
  steps: string[];         // ordered troubleshooting steps
}

function buildIssueSpec(
  isOffline: boolean,
  isUnhealthy: boolean,
  isIpcOnly: boolean,
  hostname: string,
  device: DeviceListItem | null,
  deviceInfo: DeviceInfoResponse | null,
): IssueSpec {
  if (isOffline) {
    const lastSeen = device?.lastSeenAt;
    let since = '';
    if (lastSeen) {
      const mins = Math.floor((Date.now() - new Date(lastSeen).getTime()) / 60000);
      since = mins < 60 ? ` · last seen ${mins}min ago` : ` · last seen ${Math.floor(mins / 60)}h ago`;
    }
    return {
      badge: 'OFFLINE',
      headline: `${hostname} is not responding`,
      subtext: `No heartbeat received${since}`,
      component: 'Windows Service (background)',
      what: 'The TimeTrack Agent service has not reported in over 10 minutes. Activity is NOT being recorded on this machine.',
      steps: [
        'Check that the computer is powered on and connected to the internet.',
        'Open Windows Services (Win + R → services.msc) and look for "TimeTrack Agent".',
        'If the service is Stopped: right-click → Start.',
        'If it is Running but still offline: right-click → Restart.',
        'If the service does not appear: re-run the ChronosX installer to reinstall it.',
      ],
    };
  }

  if (isUnhealthy) {
    const failures = deviceInfo?.consecutiveSyncFailures ?? 0;
    const lastSync = deviceInfo?.lastSuccessfulSyncAt
      ? `last successful sync ${formatLastSeen(deviceInfo.lastSuccessfulSyncAt)}`
      : 'no successful sync recorded';
    return {
      badge: 'UNHEALTHY',
      headline: `${hostname} — agent cannot sync data`,
      subtext: `${failures} consecutive failures · ${lastSync}`,
      component: 'Windows Service (background)',
      what: `The agent is running and recording activity locally, but it has failed to upload data to the server ${failures} times in a row. Recorded data is stored on-device and will sync once the connection is restored — nothing is lost yet.`,
      steps: [
        'Check internet connectivity on this computer.',
        'The agent retries automatically every 60 s — wait 2–3 minutes before acting.',
        'Open Windows Services → Restart "TimeTrack Agent" if failures keep growing.',
        'Check the Event Logs tab below for specific error messages (look for "sync" or "http" errors).',
        'If errors mention "401 Unauthorized": the agent token may have expired — re-install or re-activate the agent.',
      ],
    };
  }

  if (!isIpcOnly) {
    // degraded
    const failures = deviceInfo?.consecutiveSyncFailures ?? 0;
    return {
      badge: 'DEGRADED',
      headline: `${hostname} — intermittent sync issues`,
      subtext: `${failures} recent failure${failures !== 1 ? 's' : ''} · usually self-resolving`,
      component: 'Windows Service (background)',
      what: 'The agent had some recent sync failures. Activity recording is unaffected — data is queued locally. This is often caused by a brief network blip or a server restart.',
      steps: [
        'Wait 2–5 minutes — this usually clears on its own.',
        'Check internet connectivity on this computer if it persists.',
        'If it escalates to UNHEALTHY, check the Event Logs tab for error details.',
      ],
    };
  }

  // IPC only
  return {
    badge: 'UI DISCONNECTED',
    headline: `${hostname} — desktop app not connected`,
    subtext: 'Background service is running · tray app is disconnected',
    component: 'Desktop Application (tray icon)',
    what: 'The background Windows Service is running normally and recording activity. However, the TimeTrack desktop app (tray icon / overlay) is not connected to it. Manual timer controls and the tray UI are unavailable for this user.',
    steps: [
      'Ask the user to restart the TimeTrack desktop application from the Start menu or system tray.',
      'If the tray icon is not visible, run "ChronosX" from the Start menu.',
      'Activity recording by the background service is unaffected — no data is lost.',
    ],
  };
}

function DeviceStatusStripe({ device, deviceInfo }: { device: DeviceListItem | null, deviceInfo: DeviceInfoResponse | null }) {
  const [expanded, setExpanded] = useState(false);

  const hostname = deviceInfo?.hostname ?? device?.hostname ?? 'dispositivo';
  const effectiveHealth = deviceInfo?.healthStatus ?? device?.healthStatus;
  const isOffline = device?.status === 'offline' || effectiveHealth === 'offline';
  const isUnhealthy = !isOffline && effectiveHealth === 'unhealthy';
  const isIpcOnly = !isOffline && !isUnhealthy && deviceInfo?.ipcConnected === false && effectiveHealth !== 'degraded';
  const isDegraded = !isOffline && !isUnhealthy && (effectiveHealth === 'degraded' || deviceInfo?.ipcConnected === false);
  const isCritical = isOffline || isUnhealthy;

  if (!isCritical && !isDegraded) return null;

  const spec = buildIssueSpec(isOffline, isUnhealthy, isIpcOnly, hostname, device, deviceInfo);
  const accent = isCritical ? 'rgba(248,113,113' : '251,191,36';
  const accentSolid = isCritical ? '#f87171' : '#fbbf24';

  return (
    <motion.div
      initial={{ opacity: 0, y: -4 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-xl mb-4 border overflow-hidden"
      style={{
        background: `rgba(${isCritical ? '248,113,113' : '251,191,36'},0.05)`,
        borderColor: `rgba(${accent},0.3)`,
      }}
    >
      {/* Header row — always visible */}
      <button
        onClick={() => setExpanded(e => !e)}
        className="w-full flex items-start gap-3 px-4 py-3 text-left hover:bg-[rgba(255,255,255,0.02)] transition-colors"
      >
        <ShieldAlert className="w-4 h-4 flex-shrink-0 mt-0.5" style={{ color: accentSolid }} />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span
              className="text-[9px] font-bold tracking-wider px-1.5 py-0.5 rounded"
              style={{ color: accentSolid, background: `rgba(${accent},0.15)` }}
            >
              {spec.badge}
            </span>
            <span className="text-[12px] font-semibold" style={{ color: accentSolid }}>
              {spec.headline}
            </span>
          </div>
          <p className="text-[11px] text-[rgba(245,247,251,0.45)] mt-0.5">{spec.subtext}</p>
        </div>
        <div className="flex items-center gap-1 flex-shrink-0 mt-0.5">
          <span className="text-[10px]" style={{ color: `rgba(${accent},0.6)` }}>
            {expanded ? 'ocultar' : 'detalhes'}
          </span>
          <motion.div animate={{ rotate: expanded ? 90 : 0 }} transition={{ duration: 0.2 }}>
            <ChevronRight className="w-3.5 h-3.5" style={{ color: `rgba(${accent},0.6)` }} />
          </motion.div>
        </div>
      </button>

      {/* Expandable details */}
      <AnimatePresence>
        {expanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <div
              className="px-4 pb-4 pt-1 border-t"
              style={{ borderColor: `rgba(${accent},0.15)` }}
            >
              {/* Affected component */}
              <div className="flex items-center gap-2 mb-3">
                <span className="text-[9px] text-[rgba(245,247,251,0.35)] uppercase tracking-wider">Componente afetado</span>
                <span
                  className="text-[9px] font-semibold px-1.5 py-0.5 rounded"
                  style={{ color: accentSolid, background: `rgba(${accent},0.1)` }}
                >
                  {spec.component}
                </span>
              </div>

              {/* What it means */}
              <p className="text-[11px] text-[rgba(245,247,251,0.65)] leading-relaxed mb-3">
                {spec.what}
              </p>

              {/* Troubleshooting steps */}
              <div>
                <p className="text-[9px] text-[rgba(245,247,251,0.35)] uppercase tracking-wider mb-2">O que fazer</p>
                <ol className="space-y-1.5">
                  {spec.steps.map((step, i) => (
                    <li key={i} className="flex items-start gap-2">
                      <span
                        className="flex-shrink-0 w-4 h-4 rounded-full text-[9px] font-bold flex items-center justify-center mt-0.5"
                        style={{ color: accentSolid, background: `rgba(${accent},0.15)` }}
                      >
                        {i + 1}
                      </span>
                      <span className="text-[11px] text-[rgba(245,247,251,0.6)] leading-relaxed">{step}</span>
                    </li>
                  ))}
                </ol>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}

function deviceSortKey(d: DeviceListItem): number {
  const h = d.healthStatus;
  if (d.status === 'offline' || h === 'offline' || h === 'unhealthy') return 0;
  if (h === 'degraded') return 1;
  return 2;
}

function MetricCard({
  icon: Icon,
  label,
  value,
  unit,
  percent,
  subtitle,
  history,
  historyKey,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  unit: string;
  percent: number;
  subtitle?: string;
  history?: MetricsHistoryPoint[];
  historyKey?: keyof MetricsHistoryPoint;
}) {
  const barColor = getBarColor(percent);

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4"
    >
      <div className="flex items-center gap-2 mb-3">
        <div className="w-8 h-8 rounded-lg bg-[rgba(139,92,246,0.1)] border border-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <Icon className="w-4 h-4 text-[#8B5CF6]" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] uppercase tracking-wider">{label}</p>
        </div>
        <span className="text-[20px] font-bold text-[#f5f7fb]">{value}<span className="text-[12px] font-normal text-[rgba(245,247,251,0.4)] ml-0.5">{unit}</span></span>
      </div>

      {/* Progress bar */}
      <div className="h-2 rounded-full bg-[rgba(255,255,255,0.06)] overflow-hidden mb-1">
        <motion.div
          initial={{ width: 0 }}
          animate={{ width: `${Math.min(percent, 100)}%` }}
          transition={{ duration: 0.6, ease: 'easeOut' }}
          className="h-full rounded-full"
          style={{ backgroundColor: barColor }}
        />
      </div>
      <div className="flex justify-between items-center">
        <span className="text-[10px] text-[rgba(245,247,251,0.3)]">{subtitle}</span>
        <span className="text-[10px] font-medium" style={{ color: barColor }}>{percent.toFixed(1)}%</span>
      </div>

      {/* Sparkline */}
      {history && history.length > 1 && historyKey && (
        <div className="mt-3 -mx-1">
          <ResponsiveContainer width="100%" height={40}>
            <LineChart data={history}>
              <Line
                type="monotone"
                dataKey={historyKey}
                stroke="#8B5CF6"
                strokeWidth={1.5}
                dot={false}
              />
              <Tooltip
                contentStyle={{
                  background: '#1a1d2e',
                  border: '1px solid rgba(255,255,255,0.1)',
                  borderRadius: 8,
                  fontSize: 10,
                  color: '#f5f7fb',
                }}
                labelFormatter={(v) => {
                  const d = new Date(v);
                  return d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
                }}
              />
              <XAxis dataKey="sampledAt" hide />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </motion.div>
  );
}

export default function Maintenance() {
  const user = useAuthStore((state) => state.user);
  const { isAdmin } = usePermissions();
  const [searchParams] = useSearchParams();
  const setGlobalAlerts = useHealthAlertStore((state) => state.setAlerts);

  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [devicesLoading, setDevicesLoading] = useState(true);
  const [healthSummary, setHealthSummary] = useState<HealthSummaryResponse | null>(null);
  // Track which device IDs were dismissed rather than a single boolean, so the banner
  // re-appears automatically when new devices enter the alert list or existing ones recover
  // and then break again.
  const [dismissedAlertIds, setDismissedAlertIds] = useState<Set<string>>(new Set());
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [metrics, setMetrics] = useState<DeviceMetricsResponse | null>(null);
  const [metricsLoading, setMetricsLoading] = useState(false);
  const [metricsError, setMetricsError] = useState<string | null>(null);
  const [events, setEvents] = useState<DeviceEventItem[]>([]);
  const [eventFilter, setEventFilter] = useState<string | null>(null);
  const [expandedEventId, setExpandedEventId] = useState<string | null>(null);
  const [deviceInfo, setDeviceInfo] = useState<DeviceInfoResponse | null>(null);
  const [commandHistory, setCommandHistory] = useState<CommandHistoryItem[]>([]);
  const [sendingCommand, setSendingCommand] = useState<string | null>(null);
  const [settingDevTools, setSettingDevTools] = useState(false);
  const [showNotifModal, setShowNotifModal] = useState(false);
  const [notifTitle, setNotifTitle] = useState('');
  const [notifBody, setNotifBody] = useState('');
  const [confirmRestart, setConfirmRestart] = useState(false);
  const [confirmForceUpdate, setConfirmForceUpdate] = useState(false);
  const [deletingDeviceId, setDeletingDeviceId] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [platformHealth, setPlatformHealth] = useState<PlatformHealthDto | null>(null);
  const [platformEvents, setPlatformEvents] = useState<PlatformEventLogDto[]>([]);
  const [platformLoading, setPlatformLoading] = useState(false);
  const [showPlatformModal, setShowPlatformModal] = useState(false);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Admin guard
  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }

  // Fetch devices + health summary
  const fetchDevices = useCallback(async () => {
    if (!user?.orgId) return;
    try {
      const [devData, summary] = await Promise.all([
        listOrgDevices(user.orgId),
        getHealthSummary(user.orgId).catch(() => null),
      ]);
      const sorted = [...devData.devices].sort((a, b) => deviceSortKey(a) - deviceSortKey(b));
      setDevices(sorted);

      // Always derive alerts from device list — reliable even when health-summary fails
      const derivedAlerts: HealthAlertItem[] = sorted
        .filter(d =>
          d.status === 'offline' ||
          d.healthStatus === 'unhealthy' ||
          d.healthStatus === 'degraded' ||
          (d.status === 'active' && d.ipcConnected === false)
        )
        .map(d => ({
          deviceId: d.deviceId,
          hostname: d.hostname,
          userDisplayName: d.userDisplayName,
          issue: d.status === 'offline'
            ? 'offline'
            : (d.healthStatus === 'unhealthy' || d.healthStatus === 'degraded')
              ? d.healthStatus
              : 'ipc_disconnected',
          lastSeenAt: d.lastSeenAt,
          healthStatus: d.status === 'offline' ? 'offline' : d.healthStatus,
        }));
      setGlobalAlerts(derivedAlerts);

      if (summary) setHealthSummary(summary);
      else {
        // Synthesise summary counts from device list so the summary bar still renders
        const onlineCount = sorted.filter(d => d.status === 'active').length;
        const offlineCount = sorted.filter(d => d.status === 'offline').length;
        const degradedCount = sorted.filter(d => d.status !== 'offline' && d.healthStatus === 'degraded').length;
        const unhealthyCount = sorted.filter(d => d.status !== 'offline' && d.healthStatus === 'unhealthy').length;
        setHealthSummary({ totalDevices: sorted.length, onlineCount, offlineCount, degradedCount, unhealthyCount, alerts: derivedAlerts });
      }
    } catch (err) {
      console.error('[Maintenance] Error fetching devices:', err);
    } finally {
      setDevicesLoading(false);
    }
  }, [user?.orgId, setGlobalAlerts]);

  const fetchPlatform = useCallback(async () => {
    setPlatformLoading(true);
    try {
      const [health, events] = await Promise.all([
        getPlatformHealth().catch(() => null),
        listPlatformEvents({ limit: 50 }).catch(() => []),
      ]);

      if (health) setPlatformHealth(health);
      setPlatformEvents(events);
    } catch (err) {
      console.error('[Maintenance] Error fetching platform alerts:', err);
    } finally {
      setPlatformLoading(false);
    }
  }, []);

  // Initial load + poll device list every 30s
  const devicePollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  useEffect(() => {
    fetchDevices();
    fetchPlatform();
    devicePollRef.current = setInterval(fetchDevices, 30_000);
    return () => {
      if (devicePollRef.current) { clearInterval(devicePollRef.current); devicePollRef.current = null; }
    };
  }, [fetchDevices, fetchPlatform]);

  // Prune dismissed IDs when devices recover — ensures the banner re-appears
  // if a previously-dismissed device breaks again after recovering.
  useEffect(() => {
    if (!healthSummary?.alerts) return;
    const activeAlertIds = new Set(healthSummary.alerts.map(a => a.deviceId));
    setDismissedAlertIds(prev => {
      const pruned = new Set([...prev].filter(id => activeAlertIds.has(id)));
      return pruned.size !== prev.size ? pruned : prev;
    });
  }, [healthSummary?.alerts]);

  // Fetch metrics for selected device
  const fetchMetrics = useCallback(async (deviceId: string, isInitial = false) => {
    if (!user?.orgId) return;
    if (isInitial) { setMetricsLoading(true); setMetricsError(null); setMetrics(null); }
    try {
      const data = await getDeviceMetrics(user.orgId, deviceId);
      setMetrics(data);
      setMetricsError(null);
    } catch (err) {
      if (isInitial) {
        setMetrics(null);
        setMetricsError(err instanceof Error ? err.message : 'Erro ao carregar metricas');
      }
    } finally {
      if (isInitial) setMetricsLoading(false);
    }
  }, [user?.orgId]);

  // Fetch events for selected device — respects maintenance log preferences from Settings
  const fetchEvents = useCallback(async (deviceId: string, category?: string | null) => {
    if (!user?.orgId) return;
    try {
      const prefs = getMaintenanceLogPrefs();
      const effectiveCategory = category ?? (prefs.categories.length === 1 ? prefs.categories[0] : undefined);
      const effectiveSeverity = prefs.severities.length === 1 ? prefs.severities[0] : undefined;
      const data = await getDeviceEvents(
        user.orgId, deviceId,
        effectiveCategory,
        effectiveSeverity,
        prefs.limit
      );
      setEvents(data.events);
    } catch {
      // Non-critical — don't block UI
    }
  }, [user?.orgId]);

  // Fetch device info + command history
  const fetchDeviceInfo = useCallback(async (deviceId: string) => {
    if (!user?.orgId) return;
    try {
      const [info, history] = await Promise.all([
        getDeviceInfo(user.orgId, deviceId),
        getCommandHistory(user.orgId, deviceId),
      ]);
      setDeviceInfo(info);
      setCommandHistory(history.commands);
    } catch { /* non-critical */ }
  }, [user?.orgId]);

  // Send remote command
  const handleSendCommand = useCallback(async (commandType: string, payload?: object) => {
    if (!user?.orgId || !selectedDeviceId) return;
    setSendingCommand(commandType);
    try {
      await sendRemoteCommand(user.orgId, selectedDeviceId, commandType, payload);
      // Refresh command history immediately
      fetchDeviceInfo(selectedDeviceId);
    } catch (err) {
      console.error('Failed to send command:', err);
    } finally {
      setSendingCommand(null);
      setConfirmRestart(false);
      setShowNotifModal(false);
      setNotifTitle('');
      setNotifBody('');
    }
  }, [user?.orgId, selectedDeviceId, fetchDeviceInfo]);

  const handleToggleDevTools = useCallback(async () => {
    if (!user?.orgId || !selectedDeviceId || !deviceInfo) return;
    const until = deviceInfo.devToolsEnabledUntilUtc;
    const currentlyEnabled = !!until && new Date(until).getTime() > Date.now();
    const nextEnabled = !currentlyEnabled;

    setSettingDevTools(true);
    try {
      await setDeviceDevToolsAccess(user.orgId, selectedDeviceId, nextEnabled);
      fetchDeviceInfo(selectedDeviceId);
    } catch (err) {
      console.error('Failed to set DevTools access:', err);
    } finally {
      setSettingDevTools(false);
    }
  }, [user?.orgId, selectedDeviceId, deviceInfo, fetchDeviceInfo]);

  // Delete device
  const handleDeleteDevice = useCallback(async (deviceId: string) => {
    if (!user?.orgId) return;
    setDeletingDeviceId(deviceId);
    try {
      await deleteDevice(user.orgId, deviceId);
      if (selectedDeviceId === deviceId) setSelectedDeviceId(null);
      setConfirmDeleteId(null);
      fetchDevices();
    } catch (err) {
      console.error('Failed to delete device:', err);
    } finally {
      setDeletingDeviceId(null);
    }
  }, [user?.orgId, selectedDeviceId, fetchDevices]);

  // Poll metrics on device selection
  useEffect(() => {
    if (selectedDeviceId) {
      fetchMetrics(selectedDeviceId, true);
      fetchDeviceInfo(selectedDeviceId);
      pollRef.current = setInterval(() => {
        fetchMetrics(selectedDeviceId);
        fetchDeviceInfo(selectedDeviceId);
      }, METRICS_POLL_INTERVAL_MS);
    } else {
      setMetrics(null);
      setMetricsError(null);
      setEvents([]);
      setDeviceInfo(null);
      setCommandHistory([]);
    }
    return () => {
      if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; }
    };
  }, [selectedDeviceId, fetchMetrics, fetchDeviceInfo]);

  // Poll events separately (so filter changes don't reload metrics)
  const eventPollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  useEffect(() => {
    if (selectedDeviceId) {
      fetchEvents(selectedDeviceId, eventFilter);
      eventPollRef.current = setInterval(() => fetchEvents(selectedDeviceId, eventFilter), METRICS_POLL_INTERVAL_MS);
    } else {
      setEvents([]);
    }
    return () => {
      if (eventPollRef.current) { clearInterval(eventPollRef.current); eventPollRef.current = null; }
    };
  }, [selectedDeviceId, fetchEvents, eventFilter]);

  // Auto-select device from URL param (?device=deviceId — set by notification bell click)
  useEffect(() => {
    const deviceParam = searchParams.get('device');
    if (deviceParam) setSelectedDeviceId(deviceParam);
  }, [searchParams]);

  const selectedDevice = useMemo(
    () => devices.find(d => d.deviceId === selectedDeviceId) ?? null,
    [devices, selectedDeviceId]
  );

  const memPercent = metrics && metrics.memoryTotalMb > 0
    ? (metrics.memoryUsedMb / metrics.memoryTotalMb) * 100 : 0;
  const diskPercent = metrics && metrics.diskTotalGb > 0
    ? (metrics.diskUsedGb / metrics.diskTotalGb) * 100 : 0;

  return (
    <div className="flex h-screen bg-transparent overflow-hidden pt-[52px] md:pt-0">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header */}
        <div className="px-4 lg:px-5 pt-4 pb-2 flex-shrink-0">
          <header className="flex items-center justify-between">
            <div className="relative">
              <h1 className="text-[16px] sm:text-[20px] font-semibold text-[#f5f7fb] pb-2">Manutencao</h1>
              <motion.div
                className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3)]"
                layoutId="web-maintenance-tab"
              />
              <p className="text-[11px] sm:text-[12px] text-[rgba(245,247,251,0.4)] mt-1">
                Monitoramento de maquinas · {user?.orgName}
              </p>
            </div>
            <div className="flex items-center gap-2">
              <motion.button
                onClick={() => setShowPlatformModal(true)}
                whileHover={{ scale: 1.03 }} whileTap={{ scale: 0.97 }}
                className="h-9 px-3 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center gap-2 hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                <Globe className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.7)] font-medium">Platform</span>
                <span
                  className={`text-[10px] px-2 py-0.5 rounded-full border ${
                    platformHealth?.status === 'healthy'
                      ? 'text-[#05df72] border-[rgba(5,223,114,0.3)] bg-[rgba(5,223,114,0.08)]'
                      : 'text-[#f87171] border-[rgba(248,113,113,0.3)] bg-[rgba(248,113,113,0.08)]'
                  }`}
                >
                  {platformHealth?.status ?? 'unknown'}
                </span>
              </motion.button>

              <motion.button
                onClick={() => { fetchDevices(); fetchPlatform(); setDismissedAlertIds(new Set()); if (selectedDeviceId) fetchMetrics(selectedDeviceId, true); }}
                whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
                className="w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                <RefreshCw className={`w-4 h-4 text-[rgba(245,247,251,0.6)] ${(metricsLoading || platformLoading) ? 'animate-spin' : ''}`} />
              </motion.button>
            </div>
          </header>
        </div>

        {/* Health Summary Bar */}
        {healthSummary && (
          <div className="px-4 lg:px-5 pb-3 flex-shrink-0">
            <div className="grid grid-cols-3 gap-2">
              {[
                { label: 'Online', count: healthSummary.onlineCount, color: '#05df72' },
                { label: 'Offline', count: healthSummary.offlineCount, color: 'rgba(248,113,113,0.8)' },
                { label: 'Degradado', count: healthSummary.degradedCount + healthSummary.unhealthyCount, color: '#fbbf24' },
              ].map(({ label, count, color }) => (
                <div key={label} className="bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] rounded-xl px-3 py-2 flex items-center gap-2">
                  <span className="w-2 h-2 rounded-full flex-shrink-0" style={{ backgroundColor: color }} />
                  <span className="text-[10px] text-[rgba(245,247,251,0.4)] flex-1 truncate">{label}</span>
                  <span className="text-[15px] font-semibold" style={{ color }}>{count}</span>
                </div>
              ))}
            </div>

            {/* Alert banner — visible when any alert device hasn't been dismissed */}
            {healthSummary.alerts.length > 0 && healthSummary.alerts.some(a => !dismissedAlertIds.has(a.deviceId)) && (
              <div className="mt-2 flex items-center gap-2 px-3 py-2 rounded-xl bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)]">
                <ShieldAlert className="w-4 h-4 text-[rgba(248,113,113,0.8)] flex-shrink-0" />
                <span className="text-[11px] text-[rgba(248,113,113,0.9)] flex-1">
                  {healthSummary.alerts.length === 1
                    ? `1 dispositivo precisa de atencao · ${healthSummary.alerts[0].hostname}`
                    : `${healthSummary.alerts.length} dispositivos precisam de atencao`}
                </span>
                <button
                  onClick={() => setDismissedAlertIds(new Set(healthSummary.alerts.map(a => a.deviceId)))}
                  className="text-[rgba(248,113,113,0.5)] hover:text-[rgba(248,113,113,0.9)] transition-colors flex-shrink-0"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </div>
            )}
          </div>
        )}

        {/* Two-column body: device card list (left) + detail panel (right) */}
        <div className="flex-1 flex flex-col md:flex-row min-h-0 overflow-y-auto md:overflow-hidden">

          {/* ── Left: device card list ─────────────────────────────────────── */}
          <aside className="w-full md:w-[260px] md:flex-shrink-0 border-b md:border-b-0 md:border-r border-[rgba(255,255,255,0.04)] overflow-y-auto max-h-[240px] md:max-h-none">
            {devicesLoading ? (
              <div className="flex items-center justify-center gap-2 py-8">
                <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                <span className="text-[11px] text-[rgba(245,247,251,0.4)]">Carregando...</span>
              </div>
            ) : devices.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
                <Monitor className="w-8 h-8 text-[rgba(245,247,251,0.15)] mb-2" />
                <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Nenhum dispositivo</p>
              </div>
            ) : (
              <div className="p-2 space-y-1">
                {devices.map((device) => {
                  const isSelected = device.deviceId === selectedDeviceId;
                  const isConfirming = confirmDeleteId === device.deviceId;
                  const isDeleting = deletingDeviceId === device.deviceId;
                  const isOnline = device.status === 'active';
                  const hasIssue = device.healthStatus === 'unhealthy' || device.healthStatus === 'degraded' || (!isOnline && device.status === 'offline') || (isOnline && device.ipcConnected === false);
                  const isCritical = !isOnline || device.healthStatus === 'unhealthy';

                  return (
                    <motion.div
                      key={device.deviceId}
                      layout
                      className={`group relative rounded-xl border transition-all cursor-pointer ${
                        isSelected
                          ? 'bg-[rgba(139,92,246,0.1)] border-[rgba(139,92,246,0.3)]'
                          : 'bg-[rgba(255,255,255,0.02)] border-[rgba(255,255,255,0.05)] hover:bg-[rgba(255,255,255,0.05)] hover:border-[rgba(255,255,255,0.09)]'
                      }`}
                      onClick={() => { setSelectedDeviceId(device.deviceId); setConfirmDeleteId(null); }}
                    >
                      {/* Selected left bar */}
                      {isSelected && (
                        <div className="absolute left-0 top-3 bottom-3 w-0.5 rounded-full bg-[#8B5CF6]" />
                      )}

                      <div className="px-3 pt-3 pb-2.5">
                        {/* Row 1: user name + status LED */}
                        <div className="flex items-start justify-between gap-2 mb-1">
                          <div className="flex items-center gap-1.5 min-w-0">
                            <User className="w-3 h-3 text-[rgba(139,92,246,0.6)] flex-shrink-0" />
                            <span className={`text-[12px] font-semibold truncate ${isSelected ? 'text-[#c4b5fd]' : 'text-[rgba(245,247,251,0.9)]'}`}>
                              {device.userDisplayName || device.hostname}
                            </span>
                          </div>
                          <div className="flex items-center gap-1.5 flex-shrink-0 mt-0.5">
                            {hasIssue && (
                              <AlertTriangle
                                className={`w-3.5 h-3.5 flex-shrink-0 ${isCritical ? 'text-[#f87171]' : 'text-[#fbbf24]'}`}
                              />
                            )}
                            <span
                              className={`w-2 h-2 rounded-full flex-shrink-0 ${isOnline ? 'bg-[#05df72]' : 'bg-[rgba(248,113,113,0.6)]'}`}
                              title={isOnline ? 'Online' : 'Offline'}
                            />
                          </div>
                        </div>

                        {/* Row 2: hostname + version badge */}
                        <div className="flex items-center gap-1.5 mb-2">
                          <Monitor className="w-3 h-3 text-[rgba(245,247,251,0.25)] flex-shrink-0" />
                          <span className="text-[10px] text-[rgba(245,247,251,0.45)] truncate flex-1">
                            {device.hostname}
                          </span>
                          <span className="text-[9px] font-mono text-[rgba(139,92,246,0.6)] bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)] px-1.5 py-0.5 rounded flex-shrink-0">
                            v{device.agentVersion}
                          </span>
                        </div>

                        {/* Row 3: tracking state + last seen */}
                        <div className="flex items-center justify-between gap-2">
                          <TrackingStateLed state={device.trackingState} />
                          <span className="text-[9px] text-[rgba(245,247,251,0.3)] flex-shrink-0">
                            {formatLastSeen(device.lastSeenAt)}
                          </span>
                        </div>

                        {/* Row 4 (conditional): connection status pill */}
                        {hasIssue && (
                          <div className="mt-2 pt-2 border-t border-[rgba(255,255,255,0.05)]">
                            <span className={`text-[9px] font-semibold px-1.5 py-0.5 rounded ${
                              !isOnline
                                ? 'text-[#f87171] bg-[rgba(248,113,113,0.1)]'
                                : device.healthStatus === 'unhealthy'
                                  ? 'text-[#f87171] bg-[rgba(248,113,113,0.1)]'
                                  : device.ipcConnected === false
                                    ? 'text-[#a78bfa] bg-[rgba(167,139,250,0.1)]'
                                    : 'text-[#fbbf24] bg-[rgba(251,191,36,0.1)]'
                            }`}>
                              {!isOnline ? 'OFFLINE' : device.healthStatus === 'unhealthy' ? 'UNHEALTHY' : device.ipcConnected === false ? 'UI DESCONECTADA' : 'DEGRADADO'}
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Delete action */}
                      <div className="px-3 pb-2.5">
                        {isConfirming ? (
                          <div className="flex items-center gap-1.5" onClick={(e) => e.stopPropagation()}>
                            <button
                              onClick={() => handleDeleteDevice(device.deviceId)}
                              disabled={isDeleting}
                              className="flex-1 py-1 rounded-lg text-[9px] font-semibold bg-[rgba(248,113,113,0.15)] text-[#f87171] border border-[rgba(248,113,113,0.3)] hover:bg-[rgba(248,113,113,0.25)] transition-colors text-center"
                            >
                              {isDeleting ? '...' : 'Confirmar remoção'}
                            </button>
                            <button
                              onClick={() => setConfirmDeleteId(null)}
                              className="px-2 py-1 rounded-lg text-[9px] text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)]"
                            >
                              ✕
                            </button>
                          </div>
                        ) : (
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(device.deviceId); }}
                            className={`flex items-center gap-1 text-[9px] text-[rgba(248,113,113,0.5)] hover:text-[#f87171] transition-opacity ${
                              !isOnline ? 'opacity-70' : 'opacity-0 group-hover:opacity-70'
                            } hover:!opacity-100`}
                            title="Remover dispositivo (historico preservado)"
                          >
                            <Trash2 className="w-3 h-3" />
                            Remover
                          </button>
                        )}
                      </div>
                    </motion.div>
                  );
                })}
              </div>
            )}
          </aside>

          {/* ── Right: device detail panel ─────────────────────────────────── */}
          <div className="flex-1 md:overflow-y-auto md:min-h-0 px-4 lg:px-5 pb-4 pt-2 md:pt-0">
          {/* Empty state */}
          {!selectedDeviceId && (
            <div className="flex items-center justify-center py-16">
              <div className="text-center">
                <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)] flex items-center justify-center">
                  <Monitor className="w-7 h-7 text-[rgba(139,92,246,0.5)]" />
                </div>
                <p className="text-[15px] font-medium text-[rgba(245,247,251,0.6)]">Selecione um usuario</p>
                <p className="text-[12px] text-[rgba(245,247,251,0.3)] mt-1 max-w-[280px] mx-auto">
                  Clique em um dispositivo na lista ao lado para monitorar em tempo real
                </p>
              </div>
            </div>
          )}

          {/* Loading */}
          {selectedDeviceId && metricsLoading && (
            <div className="flex items-center justify-center py-12">
              <div className="flex items-center gap-3">
                <div className="w-5 h-5 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                <span className="text-[13px] text-[rgba(245,247,251,0.5)]">Carregando metricas...</span>
              </div>
            </div>
          )}

          {/* Error */}
          {selectedDeviceId && metricsError && !metricsLoading && (
            <div className="flex items-center justify-center py-8">
              <div className="text-center">
                <p className="text-[13px] text-[rgba(248,113,113,0.9)]">Erro ao carregar metricas</p>
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-1">{metricsError}</p>
                <button
                  onClick={() => selectedDeviceId && fetchMetrics(selectedDeviceId, true)}
                  className="mt-3 px-4 py-1.5 text-[11px] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)] rounded-lg hover:bg-[rgba(139,92,246,0.1)] transition-colors"
                >
                  Tentar novamente
                </button>
              </div>
            </div>
          )}

          {/* Device Status Stripe — shown when device has issues */}
          {selectedDeviceId && !metricsLoading && (
            <DeviceStatusStripe device={selectedDevice} deviceInfo={deviceInfo} />
          )}

          {/* Device Info Card */}
          {selectedDeviceId && !metricsLoading && deviceInfo && (
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 mb-4">
              {[
                { icon: Globe, label: 'OS', value: deviceInfo.osVersion?.replace('Microsoft ', '') || 'N/A' },
                { icon: Monitor, label: 'Versao', value: `v${deviceInfo.agentVersion}` },
                { icon: Wifi, label: 'IP', value: deviceInfo.ipAddress || 'N/A' },
                { icon: Clock, label: 'Uptime', value: deviceInfo.uptimeSeconds ? formatUptime(deviceInfo.uptimeSeconds) : 'N/A' },
                { icon: RefreshCw, label: 'Heartbeat', value: formatLastSeen(deviceInfo.lastHeartbeatAt) },
                { icon: Play, label: 'Tracking', value: deviceInfo.trackingState || 'N/A' },
              ].map((item) => (
                <div key={item.label} className="bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl px-3 py-2">
                  <div className="flex items-center gap-1.5 mb-1">
                    <item.icon className="w-3 h-3 text-[rgba(139,92,246,0.6)]" />
                    <span className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase tracking-wider">{item.label}</span>
                  </div>
                  <p className="text-[11px] text-[rgba(245,247,251,0.8)] font-medium truncate">{item.value}</p>
                </div>
              ))}
            </div>
          )}

          {/* Metrics */}
          {selectedDeviceId && !metricsLoading && !metricsError && metrics && (
            <>
              {/* Polling indicator */}
              <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)] mb-4">
                <div className="w-1.5 h-1.5 rounded-full bg-[#8B5CF6] animate-pulse" />
                <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                  {metrics.lastUpdatedAt
                    ? `Ultima atualizacao: ${formatLastSeen(metrics.lastUpdatedAt)}`
                    : 'Sem dados recentes'}
                </span>
                <span className="text-[10px] text-[rgba(245,247,251,0.25)] hidden sm:inline">· Atualiza a cada 10s</span>
              </div>

              {/* Metric cards grid */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <MetricCard
                  icon={Cpu}
                  label="CPU"
                  value={metrics.cpuPercent.toFixed(1)}
                  unit="%"
                  percent={metrics.cpuPercent}
                  subtitle="Uso do processador"
                  history={metrics.recentHistory}
                  historyKey="cpuPercent"
                />
                <MetricCard
                  icon={MemoryStick}
                  label="Memoria"
                  value={`${(metrics.memoryUsedMb / 1024).toFixed(1)}`}
                  unit={`/ ${(metrics.memoryTotalMb / 1024).toFixed(1)} GB`}
                  percent={memPercent}
                  subtitle={`${metrics.memoryUsedMb.toLocaleString()} MB utilizados`}
                  history={metrics.recentHistory}
                  historyKey="memoryUsedMb"
                />
                <MetricCard
                  icon={HardDrive}
                  label="Disco"
                  value={metrics.diskUsedGb.toFixed(1)}
                  unit={`/ ${metrics.diskTotalGb.toFixed(1)} GB`}
                  percent={diskPercent}
                  subtitle={`${(metrics.diskTotalGb - metrics.diskUsedGb).toFixed(1)} GB livres`}
                />
              </div>

              {/* No data state */}
              {!metrics.lastUpdatedAt && (
                <div className="mt-6 flex items-center justify-center py-8">
                  <div className="text-center">
                    <ShieldAlert className="w-8 h-8 mx-auto text-[rgba(245,247,251,0.15)] mb-2" />
                    <p className="text-[13px] text-[rgba(245,247,251,0.5)]">Sem metricas disponíveis</p>
                    <p className="text-[11px] text-[rgba(245,247,251,0.3)] mt-1">
                      O agent deste dispositivo ainda nao enviou metricas de maquina
                    </p>
                  </div>
                </div>
              )}
            </>
          )}

          {/* Remote Commands */}
          {selectedDeviceId && !metricsLoading && (
            <div className="mt-6">
              <div className="flex items-center gap-2 mb-3">
                <Zap className="w-4 h-4 text-[#8B5CF6]" />
                <h2 className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Comandos Remotos</h2>
              </div>

              {deviceInfo && (
                <div className="mb-3 flex items-center justify-between gap-3 bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] rounded-xl px-3 py-2">
                  <div className="min-w-0">
                    <p className="text-[11px] font-semibold text-[rgba(245,247,251,0.9)]">DevTools</p>
                    {deviceInfo.devToolsEnabledUntilUtc && new Date(deviceInfo.devToolsEnabledUntilUtc).getTime() > Date.now() ? (
                      <p className="text-[10px] text-[rgba(245,247,251,0.45)] truncate">
                        Habilitado ate {new Date(deviceInfo.devToolsEnabledUntilUtc).toLocaleString('pt-BR')}
                      </p>
                    ) : (
                      <p className="text-[10px] text-[rgba(245,247,251,0.45)]">Desabilitado</p>
                    )}
                  </div>
                  <button
                    onClick={handleToggleDevTools}
                    disabled={settingDevTools || sendingCommand !== null}
                    className="flex items-center gap-2 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(139,92,246,0.35)] text-[#c4b5fd] bg-[rgba(139,92,246,0.08)] hover:bg-[rgba(139,92,246,0.14)] transition-colors disabled:opacity-40 flex-shrink-0"
                  >
                    {settingDevTools ? <div className="w-3 h-3 border-2 border-current border-t-transparent rounded-full animate-spin" /> : null}
                    {deviceInfo.devToolsEnabledUntilUtc && new Date(deviceInfo.devToolsEnabledUntilUtc).getTime() > Date.now() ? 'Desabilitar' : 'Habilitar'}
                  </button>
                </div>
              )}

              <div className="flex flex-wrap gap-2 mb-4">
                {/* Stop Tracking */}
                <button
                  onClick={() => handleSendCommand('stop_tracking')}
                  disabled={sendingCommand !== null}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(248,113,113,0.3)] text-[#f87171] bg-[rgba(248,113,113,0.06)] hover:bg-[rgba(248,113,113,0.12)] transition-colors disabled:opacity-40"
                >
                  {sendingCommand === 'stop_tracking' ? <div className="w-3 h-3 border-2 border-current border-t-transparent rounded-full animate-spin" /> : <Square className="w-3 h-3" />}
                  Parar Tracking
                </button>

                {/* Resume Tracking */}
                <button
                  onClick={() => handleSendCommand('resume_tracking')}
                  disabled={sendingCommand !== null}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(5,223,114,0.3)] text-[#05df72] bg-[rgba(5,223,114,0.06)] hover:bg-[rgba(5,223,114,0.12)] transition-colors disabled:opacity-40"
                >
                  {sendingCommand === 'resume_tracking' ? <div className="w-3 h-3 border-2 border-current border-t-transparent rounded-full animate-spin" /> : <Play className="w-3 h-3" />}
                  Retomar Tracking
                </button>

                {/* Force Sync */}
                <button
                  onClick={() => handleSendCommand('force_sync')}
                  disabled={sendingCommand !== null}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(139,92,246,0.3)] text-[#8B5CF6] bg-[rgba(139,92,246,0.06)] hover:bg-[rgba(139,92,246,0.12)] transition-colors disabled:opacity-40"
                >
                  {sendingCommand === 'force_sync' ? <div className="w-3 h-3 border-2 border-current border-t-transparent rounded-full animate-spin" /> : <RefreshCw className="w-3 h-3" />}
                  Forcar Sync
                </button>

                {/* Send Notification */}
                <button
                  onClick={() => setShowNotifModal(true)}
                  disabled={sendingCommand !== null}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(56,189,248,0.3)] text-[#38bdf8] bg-[rgba(56,189,248,0.06)] hover:bg-[rgba(56,189,248,0.12)] transition-colors disabled:opacity-40"
                >
                  <Bell className="w-3 h-3" />
                  Notificar Usuario
                </button>

                {/* Force Update */}
                {!confirmForceUpdate ? (
                  <button
                    onClick={() => setConfirmForceUpdate(true)}
                    disabled={sendingCommand !== null}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(34,211,238,0.3)] text-[#22D3EE] bg-[rgba(34,211,238,0.06)] hover:bg-[rgba(34,211,238,0.12)] transition-colors disabled:opacity-40"
                  >
                    <Download className="w-3 h-3" />
                    Forcar Atualizacao
                  </button>
                ) : (
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handleSendCommand('force_update')}
                      disabled={sendingCommand !== null}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium bg-[rgba(34,211,238,0.2)] text-[#22D3EE] border border-[rgba(34,211,238,0.4)] hover:bg-[rgba(34,211,238,0.3)] transition-colors"
                    >
                      {sendingCommand === 'force_update' ? <div className="w-3 h-3 border-2 border-current border-t-transparent rounded-full animate-spin" /> : <Download className="w-3 h-3" />}
                      Confirmar
                    </button>
                    <button
                      onClick={() => setConfirmForceUpdate(false)}
                      className="px-2 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
                    >
                      Cancelar
                    </button>
                  </div>
                )}

                {/* Restart Agent */}
                {!confirmRestart ? (
                  <button
                    onClick={() => setConfirmRestart(true)}
                    disabled={sendingCommand !== null}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium border border-[rgba(248,113,113,0.3)] text-[#f87171] bg-[rgba(248,113,113,0.06)] hover:bg-[rgba(248,113,113,0.12)] transition-colors disabled:opacity-40"
                  >
                    <Power className="w-3 h-3" />
                    Reiniciar Agent
                  </button>
                ) : (
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handleSendCommand('restart')}
                      disabled={sendingCommand !== null}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium bg-[rgba(248,113,113,0.2)] text-[#f87171] border border-[rgba(248,113,113,0.4)] hover:bg-[rgba(248,113,113,0.3)] transition-colors"
                    >
                      {sendingCommand === 'restart' ? <div className="w-3 h-3 border-2 border-current border-t-transparent rounded-full animate-spin" /> : <Power className="w-3 h-3" />}
                      Confirmar
                    </button>
                    <button
                      onClick={() => setConfirmRestart(false)}
                      className="px-2 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
                    >
                      Cancelar
                    </button>
                  </div>
                )}
              </div>

              {/* Command History */}
              {commandHistory.length > 0 && (
                <div className="bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-hidden mb-4">
                  <div className="px-4 py-2 border-b border-[rgba(255,255,255,0.04)]">
                    <span className="text-[10px] text-[rgba(245,247,251,0.4)] uppercase tracking-wider">Historico de Comandos</span>
                  </div>
                  <div className="divide-y divide-[rgba(255,255,255,0.04)] max-h-[200px] overflow-y-auto">
                    {commandHistory.slice(0, 10).map((cmd) => {
                      const statusStyle = cmd.status === 'completed' ? 'bg-[rgba(5,223,114,0.12)] text-[#05df72]'
                        : cmd.status === 'failed' ? 'bg-[rgba(248,113,113,0.12)] text-[#f87171]'
                        : cmd.status === 'expired' ? 'bg-[rgba(245,247,251,0.08)] text-[rgba(245,247,251,0.4)]'
                        : 'bg-[rgba(251,191,36,0.12)] text-[#fbbf24] animate-pulse';
                      return (
                        <div key={cmd.id} className="flex items-center gap-3 px-4 py-2">
                          <span className="text-[10px] font-mono text-[rgba(245,247,251,0.6)]">{cmd.commandType}</span>
                          <span className={`text-[9px] px-1.5 py-0.5 rounded font-medium ${statusStyle}`}>{cmd.status}</span>
                          <span className="text-[9px] text-[rgba(245,247,251,0.3)] ml-auto">{formatLastSeen(cmd.createdAt)}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Notification Modal */}
          <AnimatePresence>
            {showNotifModal && (
              <>
                <motion.div
                  initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
                  className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm"
                  onClick={() => setShowNotifModal(false)}
                />
                <motion.div
                  initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} exit={{ opacity: 0, scale: 0.95 }}
                  className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] max-w-[90vw] bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl p-5"
                >
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="text-[14px] font-medium text-[#f5f7fb]">Enviar Notificacao</h3>
                    <button onClick={() => setShowNotifModal(false)} className="p-1 rounded-lg hover:bg-[rgba(255,255,255,0.06)]">
                      <X className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
                    </button>
                  </div>
                  <input
                    type="text"
                    placeholder="Titulo"
                    value={notifTitle}
                    onChange={(e) => setNotifTitle(e.target.value)}
                    maxLength={100}
                    className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
                  />
                  <textarea
                    placeholder="Mensagem"
                    value={notifBody}
                    onChange={(e) => setNotifBody(e.target.value)}
                    maxLength={500}
                    rows={3}
                    className="w-full px-3 py-2 mb-4 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] resize-none"
                  />
                  <div className="flex justify-end gap-2">
                    <button onClick={() => setShowNotifModal(false)} className="px-4 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)]">
                      Cancelar
                    </button>
                    <button
                      onClick={() => handleSendCommand('send_notification', { title: notifTitle, body: notifBody })}
                      disabled={!notifTitle.trim() || sendingCommand !== null}
                      className="px-4 py-1.5 rounded-lg text-[11px] font-medium bg-[rgba(139,92,246,0.2)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)] hover:bg-[rgba(139,92,246,0.3)] disabled:opacity-40 transition-colors"
                    >
                      {sendingCommand === 'send_notification' ? 'Enviando...' : 'Enviar'}
                    </button>
                  </div>
                </motion.div>
              </>
            )}
          </AnimatePresence>

          {/* Event Log */}
          {selectedDeviceId && !metricsLoading && (
            <div className="mt-6">
              <div className="flex flex-wrap items-center gap-2 mb-3">
                <ScrollText className="w-4 h-4 text-[#8B5CF6]" />
                <h2 className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Event Log</h2>
                <span className="text-[10px] text-[rgba(245,247,251,0.3)]">({events.length})</span>
                <div className="ml-auto flex items-center gap-2">
                  <ClearLogsButton
                    label="Limpar usuario"
                    onClear={async () => {
                      if (!user?.orgId || !selectedDeviceId) return;
                      await clearDeviceEvents(user.orgId, selectedDeviceId);
                      setEvents([]);
                    }}
                  />
                  <ClearLogsButton
                    label="Limpar todos"
                    variant="danger"
                    onClear={async () => {
                      if (!user?.orgId) return;
                      await clearAllEvents(user.orgId);
                      setEvents([]);
                    }}
                  />
                </div>
              </div>

              {/* Category filter chips */}
              <div className="flex flex-wrap gap-2 mb-3">
                {[
                  { value: null, label: 'Todos' },
                  { value: 'user_action', label: 'Acoes' },
                  { value: 'system', label: 'Sistema' },
                  { value: 'error', label: 'Erros' },
                ].map((f) => (
                  <button
                    key={f.value ?? 'all'}
                    onClick={() => setEventFilter(f.value)}
                    className={`px-2.5 py-1 rounded-lg text-[10px] font-medium transition-colors ${
                      eventFilter === f.value
                        ? 'bg-[rgba(139,92,246,0.2)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)]'
                        : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.06)] hover:bg-[rgba(255,255,255,0.08)]'
                    }`}
                  >
                    {f.label}
                  </button>
                ))}
              </div>

              {/* Event list */}
              <div className="bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-hidden">
                {events.length === 0 ? (
                  <div className="py-8 text-center">
                    <ScrollText className="w-6 h-6 mx-auto text-[rgba(245,247,251,0.15)] mb-2" />
                    <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Nenhum evento registrado</p>
                  </div>
                ) : (
                  <div className="divide-y divide-[rgba(255,255,255,0.04)] max-h-[400px] overflow-y-auto">
                    {events.map((event) => {
                      const style = getEventStyle(event.eventType, event.severity);
                      const isExpanded = expandedEventId === event.id;
                      return (
                        <button
                          key={event.id}
                          onClick={() => setExpandedEventId(isExpanded ? null : event.id)}
                          className="w-full flex items-start gap-3 px-4 py-2.5 text-left hover:bg-[rgba(255,255,255,0.03)] transition-colors"
                        >
                          <div className={`w-2 h-2 rounded-full mt-1.5 flex-shrink-0 ${style.dot}`} />
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className={`text-[10px] font-mono px-1.5 py-0.5 rounded ${style.badge}`}>
                                {event.eventType}
                              </span>
                              <span className={`text-[9px] font-medium ${style.category}`}>
                                {event.category === 'user_action' ? 'Acao' : event.category === 'error' ? 'Erro' : 'Sistema'}
                              </span>
                              <span className="text-[9px] text-[rgba(245,247,251,0.3)]">
                                {formatLastSeen(event.timestamp)}
                              </span>
                            </div>
                            <p className="text-[11px] text-[rgba(245,247,251,0.7)] mt-0.5 truncate">
                              {event.message}
                            </p>
                            {isExpanded && event.metadataJson && (
                              <motion.pre
                                initial={{ opacity: 0, height: 0 }}
                                animate={{ opacity: 1, height: 'auto' }}
                                className="mt-2 p-2 rounded-lg bg-[rgba(0,0,0,0.3)] text-[9px] text-[rgba(245,247,251,0.5)] font-mono overflow-x-auto"
                              >
                                {JSON.stringify(JSON.parse(event.metadataJson), null, 2)}
                              </motion.pre>
                            )}
                          </div>
                          {event.metadataJson && (
                            <motion.div animate={{ rotate: isExpanded ? 90 : 0 }} className="mt-1 flex-shrink-0">
                              <ChevronRight className="w-3 h-3 text-[rgba(245,247,251,0.3)]" />
                            </motion.div>
                          )}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          )}
          </div>{/* end right detail panel */}
        </div>{/* end two-column body */}

        {/* Platform Alerts Modal */}
        <AnimatePresence>
          {showPlatformModal && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 bg-[rgba(0,0,0,0.6)] z-50 flex items-center justify-center p-4"
              onClick={() => setShowPlatformModal(false)}
            >
              <motion.div
                initial={{ y: 20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                exit={{ y: 20, opacity: 0 }}
                transition={{ type: 'spring', stiffness: 300, damping: 25 }}
                className="w-full max-w-2xl bg-gradient-to-br from-[rgba(26,29,46,0.98)] to-[rgba(17,19,28,0.98)] border border-[rgba(255,255,255,0.08)] rounded-2xl shadow-2xl overflow-hidden"
                onClick={(e) => e.stopPropagation()}
              >
                <div className="flex items-center justify-between px-5 py-4 border-b border-[rgba(255,255,255,0.06)]">
                  <div>
                    <h3 className="text-[14px] font-semibold text-[#f5f7fb]">Platform Alerts</h3>
                    <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
                      API-level health + critical transitions
                    </p>
                  </div>
                  <button
                    onClick={() => setShowPlatformModal(false)}
                    className="text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.8)] transition-colors"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                <div className="p-5 space-y-4 max-h-[70vh] overflow-auto">
                  <div className="bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4">
                    <div className="flex items-center justify-between">
                      <div className="text-[12px] text-[rgba(245,247,251,0.7)] font-medium">Platform health</div>
                      <button
                        onClick={fetchPlatform}
                        disabled={platformLoading}
                        className="text-[12px] text-[#8B5CF6] hover:underline disabled:opacity-50"
                      >
                        {platformLoading ? 'Loading…' : 'Refresh'}
                      </button>
                    </div>
                    <div className="mt-2 flex items-center gap-2">
                      <span className="text-[13px] text-[rgba(245,247,251,0.85)]">Status:</span>
                      <span className={`text-[13px] font-semibold ${
                        platformHealth?.status === 'healthy' ? 'text-[#05df72]' : 'text-[#f87171]'
                      }`}>
                        {platformHealth?.status ?? 'unknown'}
                      </span>
                    </div>
                    <div className="mt-2 text-[11px] text-[rgba(245,247,251,0.45)]">
                      Last changed: {platformHealth?.lastChangedAtUtc ? new Date(platformHealth.lastChangedAtUtc).toLocaleString() : '—'}
                    </div>
                  </div>

                  <div className="space-y-2">
                    <div className="text-[12px] text-[rgba(245,247,251,0.7)] font-medium">Recent platform events</div>
                    {platformEvents.length === 0 ? (
                      <div className="text-[12px] text-[rgba(245,247,251,0.4)]">No events.</div>
                    ) : (
                      platformEvents.map((e) => (
                        <div
                          key={e.id}
                          className="bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4"
                        >
                          <div className="flex items-center justify-between gap-3">
                            <div className="min-w-0">
                              <div className="text-[12px] text-[rgba(245,247,251,0.85)] font-semibold truncate">
                                {e.eventType} · {e.severity}
                              </div>
                              <div className="text-[12px] text-[rgba(245,247,251,0.65)] mt-1">
                                {e.message}
                              </div>
                            </div>
                            <div className="text-[11px] text-[rgba(245,247,251,0.35)] flex-shrink-0">
                              {new Date(e.timestampUtc).toLocaleString()}
                            </div>
                          </div>
                          {e.metadataJson && (
                            <pre className="mt-3 text-[11px] text-[rgba(245,247,251,0.55)] bg-[rgba(0,0,0,0.25)] border border-[rgba(255,255,255,0.06)] rounded-lg p-3 overflow-auto">
{e.metadataJson}
                            </pre>
                          )}
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </motion.div>
            </motion.div>
          )}
        </AnimatePresence>
      </main>
    </div>
  );
}

// ── Clear logs button ────────────────────────────────────────────────────────

function ClearLogsButton({
  label,
  onClear,
  variant = 'default',
}: {
  label: string;
  onClear: () => Promise<void>;
  variant?: 'default' | 'danger';
}) {
  const [confirm, setConfirm] = useState(false);
  const [clearing, setClearing] = useState(false);

  const handleClick = async () => {
    if (!confirm) { setConfirm(true); setTimeout(() => setConfirm(false), 3000); return; }
    setClearing(true);
    try { await onClear(); } finally { setClearing(false); setConfirm(false); }
  };

  return (
    <button
      onClick={handleClick}
      disabled={clearing}
      className={`px-2.5 py-1 rounded-lg text-[10px] font-medium border transition-colors ${
        confirm
          ? 'bg-[rgba(239,68,68,0.15)] border-[rgba(239,68,68,0.4)] text-[#f87171]'
          : variant === 'danger'
            ? 'bg-[rgba(239,68,68,0.06)] border-[rgba(239,68,68,0.15)] text-[rgba(248,113,113,0.6)] hover:text-[#f87171] hover:border-[rgba(239,68,68,0.3)]'
            : 'bg-[rgba(255,255,255,0.04)] border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)]'
      }`}
    >
      {clearing ? 'Limpando...' : confirm ? 'Confirmar?' : label}
    </button>
  );
}

// Red = needs attention / something bad happened
// Yellow = noteworthy, keep an eye on it
// Green = good / routine / resolved
type AttentionLevel = 'red' | 'yellow' | 'green';

const ATTENTION_MAP: Record<string, AttentionLevel> = {
  // Red — bad, needs attention
  'tracking.stopped': 'red',
  'tracking.paused': 'red',
  'sync.failed': 'red',
  'sync.critical': 'red',
  'backend.unreachable': 'red',
  'worker.crash': 'red',
  'token.refresh_failed': 'red',
  'device.activation_failed': 'red',
  'ipc.connection_lost': 'red',

  // Yellow — noteworthy, monitor
  'sync.partial': 'yellow',
  'health.changed': 'yellow',
  'outbox.stuck_reset': 'yellow',
  'outbox.manual_reset': 'yellow',
  'idle.detected': 'yellow',
  'sync.retry': 'yellow',
  'focus.stopped': 'yellow',
  'focus.break_skipped': 'yellow',
  'agent.stopped': 'yellow',

  // Green — good, routine, resolved
  'tracking.started': 'green',
  'tracking.resumed': 'green',
  'agent.started': 'green',
  'sync.completed': 'green',
  'backend.restored': 'green',
  'device.activated': 'green',
  'token.refreshed': 'green',
  'worker.recovered': 'green',
  'ipc.reconnected': 'green',
  'focus.started': 'green',
  'focus.paused': 'green',
  'activity.resumed': 'green',
  'settings.updated': 'green',
  'settings.app_category_changed': 'green',
  'sync.manual_triggered': 'green',
};

const ATTENTION_STYLES = {
  red: {
    dot: 'bg-[#f87171]',
    badge: 'bg-[rgba(248,113,113,0.12)] text-[#f87171] border border-[rgba(248,113,113,0.2)]',
    category: 'text-[#f87171]',
  },
  yellow: {
    dot: 'bg-[#fbbf24]',
    badge: 'bg-[rgba(251,191,36,0.12)] text-[#fbbf24] border border-[rgba(251,191,36,0.2)]',
    category: 'text-[#fbbf24]',
  },
  green: {
    dot: 'bg-[#05df72]',
    badge: 'bg-[rgba(5,223,114,0.12)] text-[#05df72] border border-[rgba(5,223,114,0.2)]',
    category: 'text-[#05df72]',
  },
};

function getEventStyle(eventType: string, severity: string) {
  // Critical severity always pulses red regardless of event type
  if (severity === 'critical') {
    return { ...ATTENTION_STYLES.red, dot: 'bg-[#f87171] animate-pulse' };
  }
  const level = ATTENTION_MAP[eventType] ?? (severity === 'error' ? 'red' : severity === 'warning' ? 'yellow' : 'green');
  return ATTENTION_STYLES[level];
}
