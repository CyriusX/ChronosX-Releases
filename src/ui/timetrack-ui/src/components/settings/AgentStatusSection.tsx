/**
 * AgentStatusSection - Seção de Status do Agent em Settings
 *
 * Exibe saúde em tempo real do Agent com visual rico e animações.
 * Sem controles manuais - apenas visualização.
 *
 * Features:
 * - Health indicator animado com pulse
 * - Status info (versão, uptime, último sync)
 * - Pending items com indicadores visuais
 * - Recent errors (when houver)
 * - Animações fluidas com Framer Motion
 */

import { motion, AnimatePresence } from 'motion/react';
import {
  Activity,
  Clock,
  AlertTriangle,
  AlertCircle,
  RefreshCw,
  Zap,
  Server,
  CheckCircle2,
  Pause,
  StopCircle,
  TrendingUp,
  Wifi,
  WifiOff,
} from 'lucide-react';
import { useAgentStatus } from '../../hooks/useAgentStatus';
import { HEALTH_CONFIG } from '../../types/agentStatus';
import type { AgentStatusUI, ErrorItem } from '../../types/agentStatus';

import { SPRING } from '../../lib/animation';

// ============================================================================
// HEALTH INDICATOR COMPONENT
// ============================================================================

interface HealthIndicatorProps {
  health: AgentStatusUI['health'];
  indicator: AgentStatusUI['healthIndicator'];
  state: AgentStatusUI['state'];
}

function HealthIndicator({ health, indicator, state }: HealthIndicatorProps) {
  const config = HEALTH_CONFIG[health];

  const StateIcon = () => {
    if (state === 'running') {
      return (
        <motion.div
          animate={{ rotate: 360 }}
          transition={{ duration: 2, repeat: Infinity, ease: 'linear' }}
        >
          <Activity className="w-6 h-6" />
        </motion.div>
      );
    }
    if (state === 'paused') {
      return <Pause className="w-6 h-6" />;
    }
    if (state === 'stopped') {
      return <StopCircle className="w-6 h-6" />;
    }
    return <Activity className="w-6 h-6" />;
  };

  const gradients: Record<AgentStatusUI['health'], string> = {
    healthy: 'from-[rgba(74,222,128,0.25)] via-[rgba(74,222,128,0.15)] to-[rgba(74,222,128,0.05)]',
    degraded: 'from-[rgba(251,191,36,0.25)] via-[rgba(251,191,36,0.15)] to-[rgba(251,191,36,0.05)]',
    unhealthy: 'from-[rgba(248,113,113,0.25)] via-[rgba(248,113,113,0.15)] to-[rgba(248,113,113,0.05)]',
  };

  const pulseColors: Record<AgentStatusUI['health'], string> = {
    healthy: 'bg-[#4ade96]',
    degraded: 'bg-[#fbbf24]',
    unhealthy: 'bg-[#f87171]',
  };

  const glowColors: Record<AgentStatusUI['health'], string> = {
    healthy: 'shadow-[0_0_30px_rgba(74,222,128,0.3)]',
    degraded: 'shadow-[0_0_30px_rgba(251,191,36,0.3)]',
    unhealthy: 'shadow-[0_0_30px_rgba(248,113,113,0.3)]',
  };

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.4 }}
      className={`relative overflow-hidden rounded-2xl p-6 bg-gradient-to-br ${gradients[health]} ${glowColors[health]} border border-[rgba(255,255,255,0.05)]`}
    >
      {/* Animated background pulse */}
      <motion.div
        className={`absolute inset-0 ${pulseColors[health]} opacity-10`}
        animate={{
          opacity: [0.05, 0.15, 0.05],
        }}
        transition={{
          duration: 2,
          repeat: Infinity,
          ease: 'easeInOut',
        }}
      />

      <div className="relative flex items-center justify-between">
        <div className="flex items-center gap-5">
          {/* Main icon with pulse ring */}
          <div className="relative">
            <motion.div
              className={`absolute inset-0 rounded-xl ${pulseColors[health]} opacity-30`}
              animate={{
                scale: [1, 1.3, 1],
                opacity: [0.3, 0.1, 0.3],
              }}
              transition={{
                duration: 2,
                repeat: Infinity,
                ease: 'easeInOut',
              }}
            />
            <motion.div
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ type: 'spring', ...SPRING.bouncy }}
              className={`relative w-14 h-14 rounded-xl flex items-center justify-center ${config.color} bg-opacity-20 backdrop-blur-sm`}
            >
              <StateIcon />
            </motion.div>
          </div>

          <div>
            <motion.p
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              className={`text-[18px] font-semibold ${config.color}`}
            >
              {indicator.label}
            </motion.p>
            <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-1">
              Status do Agent
            </p>
          </div>
        </div>

        {/* Live indicator */}
        <div className="flex items-center gap-3">
          <motion.div
            className="flex items-center gap-2 px-3 py-1.5 rounded-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.1)]"
            whileHover={{ scale: 1.05 }}
          >
            <motion.span
              className={`w-2.5 h-2.5 rounded-full ${pulseColors[health]}`}
              animate={{
                scale: [1, 1.2, 1],
                opacity: [1, 0.7, 1],
              }}
              transition={{
                duration: 1.5,
                repeat: Infinity,
                ease: 'easeInOut',
              }}
            />
            <span className="text-[11px] font-medium text-[rgba(245,247,251,0.6)]">
              Em tempo real
            </span>
          </motion.div>
        </div>
      </div>
    </motion.div>
  );
}

// ============================================================================
// STATUS INFO CARD COMPONENT
// ============================================================================

interface StatusInfoCardProps {
  icon: React.ReactNode;
  label: string;
  value: string;
  subtext?: string;
  iconColor?: string;
  trend?: 'up' | 'down' | 'neutral';
}

function StatusInfoCard({
  icon,
  label,
  value,
  subtext,
  iconColor = 'text-[#4ad9ff]',
}: StatusInfoCardProps) {
  return (
    <motion.div
      whileHover={{ scale: 1.02, backgroundColor: 'rgba(255,255,255,0.04)' }}
      transition={{ duration: 0.2 }}
      className="flex items-center gap-3 py-3 px-4 rounded-xl bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)]"
    >
      <div
        className={`w-9 h-9 rounded-lg bg-gradient-to-br from-[rgba(255,255,255,0.1)] to-[rgba(255,255,255,0.05)] flex items-center justify-center ${iconColor}`}
      >
        {icon}
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-[11px] text-[rgba(245,247,251,0.5)]">{label}</p>
        <p className="text-[14px] font-medium text-[#f5f7fb] truncate">{value}</p>
      </div>
      {subtext && (
        <p className="text-[11px] text-[rgba(245,247,251,0.3)]">{subtext}</p>
      )}
    </motion.div>
  );
}

// ============================================================================
// SYNC STATUS BADGE
// ============================================================================

interface SyncStatusBadgeProps {
  status: AgentStatusUI['syncStatus'];
  lastSyncRelative?: string;
  pendingItems: number;
}

function SyncStatusBadge({
  status,
  lastSyncRelative,
  pendingItems,
}: SyncStatusBadgeProps) {
  const configMap = {
    synced: {
      color: 'text-[#4ade96]',
      bg: 'bg-[rgba(74,222,128,0.15)]',
      border: 'border-[rgba(74,222,128,0.3)]',
      label: 'Sincronizado',
      icon: CheckCircle2,
    },
    syncing: {
      color: 'text-[#4ad9ff]',
      bg: 'bg-[rgba(74,217,255,0.15)]',
      border: 'border-[rgba(74,217,255,0.3)]',
      label: 'Sincronizando...',
      icon: RefreshCw,
    },
    pending: {
      color: 'text-[#fbbf24]',
      bg: 'bg-[rgba(251,191,36,0.15)]',
      border: 'border-[rgba(251,191,36,0.3)]',
      label: 'Pendente',
      icon: Clock,
    },
    failed: {
      color: 'text-[#f87171]',
      bg: 'bg-[rgba(248,113,113,0.15)]',
      border: 'border-[rgba(248,113,113,0.3)]',
      label: 'Falhou',
      icon: AlertCircle,
    },
  };
  const config = configMap[status] || configMap.pending;
  const Icon = config.icon;

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <motion.span
        initial={{ opacity: 0, scale: 0.9 }}
        animate={{ opacity: 1, scale: 1 }}
        className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-medium ${config.bg} ${config.color} border ${config.border}`}
      >
        {status === 'syncing' ? (
          <motion.div
            animate={{ rotate: 360 }}
            transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
          >
            <Icon className="w-3.5 h-3.5" />
          </motion.div>
        ) : (
          <Icon className="w-3.5 h-3.5" />
        )}
        {config.label}
      </motion.span>
      {status === 'synced' && lastSyncRelative && (
        <span className="text-[11px] text-[rgba(245,247,251,0.4)]">
          {lastSyncRelative}
        </span>
      )}
      {pendingItems > 0 && (
        <motion.span
          initial={{ opacity: 0, scale: 0.8 }}
          animate={{ opacity: 1, scale: 1 }}
          className="flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-medium bg-[rgba(251,191,36,0.15)] text-[#fbbf24] border border-[rgba(251,191,36,0.2)]"
        >
          <Clock className="w-3 h-3" />
          {pendingItems} pendente{pendingItems > 1 ? 's' : ''}
        </motion.span>
      )}
    </div>
  );
}

// ============================================================================
// ERROR ITEM CARD COMPONENT
// ============================================================================

interface ErrorItemCardProps {
  message: string;
  timestamp: string;
  type: string;
  index: number;
}

function ErrorItemCard({ message, timestamp, type, index }: ErrorItemCardProps) {
  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.1 }}
      className="py-2.5 px-3.5 rounded-xl bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.2)] hover:bg-[rgba(248,113,113,0.15)] transition-colors"
    >
      <div className="flex items-start gap-3">
        <motion.div
          animate={{ scale: [1, 1.1, 1] }}
          transition={{ duration: 2, repeat: Infinity }}
        >
          <AlertCircle className="w-4 h-4 text-[#f87171] mt-0.5 flex-shrink-0" />
        </motion.div>
        <div className="flex-1 min-w-0">
          <p className="text-[12px] text-[#f87171] font-medium">{type}</p>
          <p className="text-[12px] text-[rgba(248,113,113,0.8)] truncate mt-0.5">
            {message}
          </p>
        </div>
        <span className="text-[10px] text-[rgba(245,247,251,0.3)] flex-shrink-0 font-mono">
          {new Date(timestamp).toLocaleTimeString('pt-BR', {
            hour: '2-digit',
            minute: '2-digit',
          })}
        </span>
      </div>
    </motion.div>
  );
}

// ============================================================================
// CONNECTION STATUS INDICATOR
// ============================================================================

interface ConnectionStatusProps {
  isConnected: boolean;
}

function ConnectionStatus({ isConnected }: ConnectionStatusProps) {
  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className={`flex items-center gap-2 px-3 py-2 rounded-lg ${
        isConnected
          ? 'bg-[rgba(74,222,128,0.1)] border border-[rgba(74,222,128,0.2)]'
          : 'bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.2)]'
      }`}
    >
      {isConnected ? (
        <>
          <Wifi className="w-4 h-4 text-[#4ade96]" />
          <span className="text-[11px] text-[#4ade96] font-medium">Conectado</span>
        </>
      ) : (
        <>
          <WifiOff className="w-4 h-4 text-[#f87171]" />
          <span className="text-[11px] text-[#f87171] font-medium">Desconectado</span>
        </>
      )}
    </motion.div>
  );
}

// ============================================================================
// MAIN COMPONENT
// ============================================================================

export function AgentStatusSection() {
  const { status, isLoading, isAgentOnline } = useAgentStatus();

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div>
          <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Status do Agent</h2>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
            Monitoramento em tempo real do serviço de tracking
          </p>
        </div>
        <div className="animate-pulse space-y-4">
          <div className="h-32 bg-[rgba(255,255,255,0.05)] rounded-2xl" />
          <div className="h-24 bg-[rgba(255,255,255,0.05)] rounded-2xl" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center justify-between"
      >
        <div>
          <h2 className="text-[20px] font-semibold text-[#f5f7fb]">
            Status do Agent
          </h2>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
            Monitoramento em tempo real do serviço de tracking
          </p>
        </div>
        <ConnectionStatus isConnected={isAgentOnline} />
      </motion.div>

      {/* Health Indicator */}
      <HealthIndicator
        health={status.health}
        indicator={status.healthIndicator}
        state={status.state}
      />

      {/* Status Info Grid */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.1 }}
        className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5"
      >
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center">
            <Server className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">
            Informações do Sistema
          </h3>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <StatusInfoCard
            icon={
              status.state === 'running' ? (
                <motion.div
                  animate={{ scale: [1, 1.1, 1] }}
                  transition={{ duration: 1.5, repeat: Infinity }}
                >
                  <Zap className="w-4 h-4" />
                </motion.div>
              ) : (
                <Activity className="w-4 h-4" />
              )
            }
            label="Estado"
            value={
              status.state === 'running'
                ? 'Executando'
                : status.state === 'paused'
                  ? 'Pausado'
                  : status.state === 'idle'
                    ? 'Inativo'
                    : 'Parado'
            }
            iconColor={
              status.state === 'running'
                ? 'text-[#4ade96]'
                : status.state === 'paused'
                  ? 'text-[#fbbf24]'
                  : 'text-[#f87171]'
            }
          />
          <StatusInfoCard
            icon={<Clock className="w-4 h-4" />}
            label="Uptime"
            value={formatUptime(status.uptime)}
            iconColor="text-[#a78bfa]"
          />
          <StatusInfoCard
            icon={
              status.syncStatus === 'synced' ? (
                <CheckCircle2 className="w-4 h-4" />
              ) : (
                <RefreshCw className="w-4 h-4" />
              )
            }
            label="Última Sincronização"
            value={status.lastSyncRelative || 'Nunca'}
            iconColor={
              status.syncStatus === 'synced'
                ? 'text-[#4ade96]'
                : 'text-[#4ad9ff]'
            }
          />
          <StatusInfoCard
            icon={<TrendingUp className="w-4 h-4" />}
            label="Versão"
            value={status.version}
            iconColor="text-[#4ad9ff]"
          />
        </div>
      </motion.div>

      {/* Sync Status */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.2 }}
        className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5"
      >
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#a78bfa] to-[#7c3aed] flex items-center justify-center">
              <RefreshCw className="w-4 h-4 text-white" />
            </div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">
              Sincronização
            </h3>
          </div>
          <SyncStatusBadge
            status={status.syncStatus}
            lastSyncRelative={status.lastSyncRelative}
            pendingItems={status.pendingItems}
          />
        </div>

        <AnimatePresence mode="wait">
          {status.pendingItems > 0 && (
            <motion.div
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              className="mt-3 p-3 rounded-xl bg-[rgba(251,191,36,0.1)] border border-[rgba(251,191,36,0.2)]"
            >
              <div className="flex items-center gap-2">
                <motion.div
                  animate={{ scale: [1, 1.1, 1] }}
                  transition={{ duration: 1.5, repeat: Infinity }}
                >
                  <AlertTriangle className="w-4 h-4 text-[#fbbf24]" />
                </motion.div>
                <span className="text-[12px] text-[#fbbf24]">
                  {status.pendingItems} item{status.pendingItems > 1 ? 's' : ''}{' '}
                  aguardando sincronização
                </span>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        <AnimatePresence mode="wait">
          {status.failedItems > 0 && (
            <motion.div
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              className="mt-3 p-3 rounded-xl bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.2)]"
            >
              <div className="flex items-center gap-2">
                <motion.div
                  animate={{ rotate: [0, 10, -10, 0] }}
                  transition={{ duration: 0.5, repeat: Infinity, repeatDelay: 2 }}
                >
                  <AlertCircle className="w-4 h-4 text-[#f87171]" />
                </motion.div>
                <span className="text-[12px] text-[#f87171]">
                  {status.failedItems} item{status.failedItems > 1 ? 's' : ''}{' '}
                  falhou ao sincronizar
                </span>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>

      {/* Recent Errors */}
      <AnimatePresence mode="wait">
        {status.recentErrors.length > 0 && (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
            transition={{ delay: 0.3 }}
            className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(248,113,113,0.2)] rounded-2xl p-5"
          >
            <div className="flex items-center gap-3 mb-4">
              <motion.div
                animate={{ scale: [1, 1.05, 1] }}
                transition={{ duration: 2, repeat: Infinity }}
                className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#f87171] to-[#dc2626] flex items-center justify-center"
              >
                <AlertCircle className="w-4 h-4 text-white" />
              </motion.div>
              <h3 className="text-[14px] font-medium text-[#f87171]">
                Erros Recentes
              </h3>
              <span className="ml-auto px-2 py-0.5 rounded-md bg-[rgba(248,113,113,0.2)] text-[11px] text-[#f87171] font-mono">
                {status.recentErrors.length}
              </span>
            </div>

            <div className="space-y-2">
              {status.recentErrors.map((error: ErrorItem, index: number) => (
                <ErrorItemCard
                  key={error.id}
                  message={error.message}
                  timestamp={error.timestamp}
                  type={error.type}
                  index={index}
                />
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// Helper function to format uptime
function formatUptime(seconds: number): string {
  if (seconds < 60) {
    return `${seconds}s`;
  }
  const mins = Math.floor(seconds / 60);
  if (mins < 60) {
    return `${mins}min`;
  }
  const hours = Math.floor(mins / 60);
  const remainingMins = mins % 60;
  if (hours < 24) {
    return `${hours}h ${remainingMins}min`;
  }
  const days = Math.floor(hours / 24);
  const remainingHours = hours % 24;
  return `${days}d ${remainingHours}h`;
}
