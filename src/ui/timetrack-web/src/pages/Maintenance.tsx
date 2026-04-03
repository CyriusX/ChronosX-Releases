/**
 * Maintenance — Admin-only page for monitoring agent machine metrics.
 *
 * Displays CPU, Memory, and Disk usage for selected devices.
 * Polls every 10s when a device is selected.
 */

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Monitor, ChevronDown, RefreshCw, Cpu, HardDrive, MemoryStick, ShieldAlert } from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { usePermissions } from '../hooks/usePermissions';
import { Navigate } from 'react-router-dom';
import {
  listOrgDevices,
  getDeviceMetrics,
  type DeviceListItem,
  type DeviceMetricsResponse,
  type MetricsHistoryPoint,
} from '../services/maintenanceApi';
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

function getStatusColor(status: string): string {
  switch (status) {
    case 'active': return 'bg-[#05df72]';
    case 'offline': return 'bg-[rgba(245,247,251,0.3)]';
    default: return 'bg-[rgba(248,113,113,0.6)]';
  }
}

function getBarColor(percent: number): string {
  if (percent < 60) return '#05df72';
  if (percent < 80) return '#fbbf24';
  return '#f87171';
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

  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [devicesLoading, setDevicesLoading] = useState(true);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [metrics, setMetrics] = useState<DeviceMetricsResponse | null>(null);
  const [metricsLoading, setMetricsLoading] = useState(false);
  const [metricsError, setMetricsError] = useState<string | null>(null);
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Admin guard
  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }

  // Close dropdown on outside click
  useEffect(() => {
    if (!showDropdown) return;
    const handler = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [showDropdown]);

  // Fetch devices
  const fetchDevices = useCallback(async () => {
    if (!user?.orgId) return;
    try {
      const data = await listOrgDevices(user.orgId);
      setDevices(data.devices);
    } catch (err) {
      console.error('[Maintenance] Error fetching devices:', err);
    } finally {
      setDevicesLoading(false);
    }
  }, [user?.orgId]);

  useEffect(() => {
    fetchDevices();
  }, [fetchDevices]);

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

  // Poll metrics
  useEffect(() => {
    if (selectedDeviceId) {
      fetchMetrics(selectedDeviceId, true);
      pollRef.current = setInterval(() => fetchMetrics(selectedDeviceId), METRICS_POLL_INTERVAL_MS);
    } else {
      setMetrics(null);
      setMetricsError(null);
    }
    return () => {
      if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; }
    };
  }, [selectedDeviceId, fetchMetrics]);

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
            <motion.button
              onClick={() => { fetchDevices(); if (selectedDeviceId) fetchMetrics(selectedDeviceId, true); }}
              whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
              className="w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <RefreshCw className={`w-4 h-4 text-[rgba(245,247,251,0.6)] ${metricsLoading ? 'animate-spin' : ''}`} />
            </motion.button>
          </header>
        </div>

        {/* Device Selector */}
        <div className="px-4 lg:px-5 pb-3 flex-shrink-0" ref={dropdownRef}>
          <div className="relative">
            <motion.button
              onClick={() => setShowDropdown(!showDropdown)}
              className="w-full flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl hover:border-[rgba(255,255,255,0.1)] transition-colors"
              whileHover={{ scale: 1.003 }} whileTap={{ scale: 0.997 }}
            >
              {selectedDevice ? (
                <>
                  <div className="w-7 h-7 rounded-lg bg-[rgba(139,92,246,0.1)] border border-[rgba(139,92,246,0.15)] flex items-center justify-center flex-shrink-0">
                    <Monitor className="w-3.5 h-3.5 text-[#8B5CF6]" />
                  </div>
                  <div className="flex-1 min-w-0 text-left">
                    <p className="text-[13px] font-medium text-[rgba(245,247,251,0.9)] truncate">
                      {selectedDevice.userDisplayName || selectedDevice.hostname}
                    </p>
                    <p className="text-[10px] text-[rgba(245,247,251,0.4)]">
                      {selectedDevice.hostname} · v{selectedDevice.agentVersion}
                      <span className={`inline-block w-1.5 h-1.5 rounded-full ml-2 mr-1 ${getStatusColor(selectedDevice.status)}`} />
                      {selectedDevice.status === 'active' ? 'Online' : 'Offline'}
                    </p>
                  </div>
                </>
              ) : (
                <>
                  <Monitor className="w-4 h-4 text-[#8B5CF6] flex-shrink-0" />
                  <span className="text-[13px] text-[rgba(245,247,251,0.6)]">Selecionar dispositivo</span>
                </>
              )}
              <motion.div animate={{ rotate: showDropdown ? 180 : 0 }} transition={{ duration: 0.2 }} className="ml-auto">
                <ChevronDown className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              </motion.div>
            </motion.button>

            <AnimatePresence>
              {showDropdown && (
                <motion.div
                  initial={{ opacity: 0, y: -8, scale: 0.95 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  exit={{ opacity: 0, y: -8, scale: 0.95 }}
                  transition={{ duration: 0.15 }}
                  className="absolute top-full left-0 right-0 mt-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl z-30 max-h-[300px] overflow-y-auto"
                >
                  {devicesLoading ? (
                    <div className="flex items-center justify-center gap-2 py-4">
                      <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                      <span className="text-[11px] text-[rgba(245,247,251,0.4)]">Carregando...</span>
                    </div>
                  ) : devices.length === 0 ? (
                    <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-4">Nenhum dispositivo encontrado</p>
                  ) : (
                    devices.map((device) => {
                      const isSelected = device.deviceId === selectedDeviceId;
                      return (
                        <button
                          key={device.deviceId}
                          onClick={() => { setSelectedDeviceId(device.deviceId); setShowDropdown(false); }}
                          className={`w-full flex items-center gap-3 px-4 py-2.5 hover:bg-[rgba(255,255,255,0.06)] transition-colors ${
                            isSelected ? 'bg-[rgba(139,92,246,0.08)] border-l-2 border-l-[#8B5CF6]' : ''
                          }`}
                        >
                          <div className="w-6 h-6 rounded-md bg-[rgba(139,92,246,0.08)] flex items-center justify-center flex-shrink-0">
                            <Monitor className="w-3 h-3 text-[rgba(139,92,246,0.6)]" />
                          </div>
                          <div className="flex-1 min-w-0 text-left">
                            <p className={`text-[11px] font-medium truncate ${isSelected ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.9)]'}`}>
                              {device.userDisplayName || device.hostname}
                            </p>
                            <p className="text-[9px] text-[rgba(245,247,251,0.4)]">
                              {device.hostname} · v{device.agentVersion} · Visto {formatLastSeen(device.lastSeenAt)}
                            </p>
                          </div>
                          <div className={`w-1.5 h-1.5 rounded-full flex-shrink-0 ${getStatusColor(device.status)}`} />
                        </button>
                      );
                    })
                  )}
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto min-h-0 px-4 lg:px-5 pb-4">
          {/* Empty state */}
          {!selectedDeviceId && (
            <div className="flex items-center justify-center py-16">
              <div className="text-center">
                <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)] flex items-center justify-center">
                  <Monitor className="w-7 h-7 text-[rgba(139,92,246,0.5)]" />
                </div>
                <p className="text-[15px] font-medium text-[rgba(245,247,251,0.6)]">Selecione um dispositivo</p>
                <p className="text-[12px] text-[rgba(245,247,251,0.3)] mt-1 max-w-[280px] mx-auto">
                  Escolha um dispositivo acima para monitorar CPU, memoria e disco em tempo real
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
        </div>
      </main>
    </div>
  );
}
