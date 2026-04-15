import { useEffect, useState, useCallback } from 'react';
import { Zap, RefreshCw, Unplug, AlertTriangle, CheckCircle2, ExternalLink, Loader2, Eye, EyeOff, ChevronDown, ChevronRight, XCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import linearLogo from '../../assets/LinearLogo.png';
import {
  listMyIntegrations,
  connectLinear,
  disconnectLinear,
  syncLinear,
  getLinearSyncHistory,
  initiateLinearOAuth,
  type UserIntegration,
  type LinearSyncResult,
  type LinearSyncHistoryEntry,
} from '../../services/integrationsApi';
import { useNotifications } from '../../stores/uiStore';

/**
 * IntegrationsSection — Settings → Integrações tab.
 *
 * Shows one card per provider (just Linear for v1). States:
 *  - Disconnected: paste-API-key form with step-by-step guide
 *  - Connected: show connected account, last sync, sync + disconnect actions
 *  - ErrorUnauthorized: red banner with reconnect button
 */
export function IntegrationsSection() {
  const { t } = useTranslation();
  const { notify } = useNotifications();

  const [loading, setLoading] = useState(true);
  const [linear, setLinear] = useState<UserIntegration | null>(null);

  const loadIntegrations = useCallback(async () => {
    setLoading(true);
    try {
      const res = await listMyIntegrations();
      const l = res.integrations?.find((i) => i.provider === 'Linear') ?? null;
      setLinear(l);
    } catch (err) {
      console.error('[IntegrationsSection] failed to load', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadIntegrations();
  }, [loadIntegrations]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.integrations.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.integrations.subtitle')}
        </p>
      </div>

      {loading ? (
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-6">
          <div className="flex items-center gap-2 text-[12px] text-[rgba(245,247,251,0.5)]">
            <Loader2 className="w-3.5 h-3.5 animate-spin" />
            {t('settings.integrations.loading')}
          </div>
        </div>
      ) : (
        <LinearCard integration={linear} onChange={loadIntegrations} notify={notify} />
      )}
    </div>
  );
}

// ── Linear card ───────────────────────────────────────────────────────────

function LinearCard({
  integration,
  onChange,
  notify,
}: {
  integration: UserIntegration | null;
  onChange: () => void | Promise<void>;
  notify: { success: (msg: string) => void; error: (msg: string) => void; info: (msg: string) => void };
}) {
  const { t } = useTranslation();
  const [mode, setMode] = useState<'view' | 'connect'>('view');
  const [apiKey, setApiKey] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [lastSyncResult, setLastSyncResult] = useState<LinearSyncResult | null>(null);
  const [oauthPolling, setOauthPolling] = useState(false);

  // Sync history (lazy-loaded when the panel is expanded for the first time)
  const [historyOpen, setHistoryOpen] = useState(false);
  const [history, setHistory] = useState<LinearSyncHistoryEntry[] | null>(null);
  const [historyLoading, setHistoryLoading] = useState(false);

  const isConnected = integration?.status === 'Active';
  const isUnauthorized = integration?.status === 'ErrorUnauthorized';

  // Force "connect" mode when unauthorized so the user can paste a fresh key
  useEffect(() => {
    if (!integration) setMode('connect');
    else if (isUnauthorized) setMode('connect');
    else setMode('view');
  }, [integration, isUnauthorized]);

  const handleConnect = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiKey.trim()) {
      setFormError(t('settings.integrations.linearKeyPrompt'));
      return;
    }
    setBusy(true);
    setFormError(null);
    try {
      await connectLinear(apiKey.trim());
      setApiKey('');
      notify.success(t('settings.integrations.connectSuccess'));
      await onChange();
    } catch (err: unknown) {
      const message = extractMessage(err) ?? t('settings.integrations.connectFail');
      setFormError(message);
    } finally {
      setBusy(false);
    }
  };

  const handleOAuthConnect = async () => {
    setBusy(true);
    setFormError(null);
    setOauthPolling(true);
    try {
      const { authorizeUrl } = await initiateLinearOAuth();
      window.open(authorizeUrl, '_blank');

      const pollInterval = 2000;
      const maxDuration = 60000;
      const startTime = Date.now();

      const poll = async (): Promise<void> => {
        if (Date.now() - startTime > maxDuration) {
          setOauthPolling(false);
          setBusy(false);
          setFormError(t('settings.integrations.oauthTimeout'));
          return;
        }

        try {
          const res = await listMyIntegrations();
          const linearIntegration = res.integrations?.find((i) => i.provider === 'Linear') ?? null;
          if (linearIntegration && linearIntegration.status === 'Active') {
            setOauthPolling(false);
            setBusy(false);
            notify.success(t('settings.integrations.connectOAuthSuccess'));
            await onChange();
            return;
          }
        } catch {
          // ignore poll errors, keep trying
        }

        await new Promise((r) => setTimeout(r, pollInterval));
        return poll();
      };

      await poll();
    } catch (err: unknown) {
      setOauthPolling(false);
      setBusy(false);
      setFormError(extractMessage(err) ?? t('settings.integrations.oauthError'));
    }
  };

  const handleSync = async () => {
    setBusy(true);
    setFormError(null);
    try {
      const res = await syncLinear();
      setLastSyncResult(res);
      const total = res.tasksCreated + res.tasksUpdated;
      notify.success(
        total > 0
          ? t('settings.integrations.syncSuccess', { created: res.tasksCreated, updated: res.tasksUpdated })
          : t('settings.integrations.syncNoChanges'),
      );
      await onChange();
      if (historyOpen) await loadHistory();
    } catch (err: unknown) {
      const message = extractMessage(err) ?? t('settings.integrations.syncFail');
      notify.error(message);
    } finally {
      setBusy(false);
    }
  };

  const loadHistory = async () => {
    setHistoryLoading(true);
    try {
      const res = await getLinearSyncHistory();
      setHistory(res.entries ?? []);
    } catch (err) {
      console.error('[IntegrationsSection] failed to load sync history', err);
      setHistory([]);
    } finally {
      setHistoryLoading(false);
    }
  };

  const toggleHistory = () => {
    const next = !historyOpen;
    setHistoryOpen(next);
    if (next && history === null) {
      loadHistory();
    }
  };

  const handleDisconnect = async () => {
    if (!confirm(t('settings.integrations.disconnectConfirm'))) return;
    setBusy(true);
    try {
      await disconnectLinear();
      setLastSyncResult(null);
      notify.info(t('settings.integrations.disconnected'));
      await onChange();
    } catch (err: unknown) {
      notify.error(extractMessage(err) ?? t('settings.integrations.disconnectFail'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      {/* Header */}
      <div className="flex items-start gap-3 mb-4">
        <div className="w-10 h-10 rounded-lg bg-white flex items-center justify-center flex-shrink-0">
          <img src={linearLogo} alt="Linear" className="w-6 h-6 object-contain" />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <h3 className="text-[14px] font-semibold text-[#f5f7fb]">Linear</h3>
            {isConnected && (
              <span className="flex items-center gap-1 text-[10px] text-[#05df72]">
                <CheckCircle2 className="w-3 h-3" />
                {t('settings.integrations.connected')}
              </span>
            )}
            {isUnauthorized && (
              <span className="flex items-center gap-1 text-[10px] text-[#f87171]">
                <AlertTriangle className="w-3 h-3" />
                {t('settings.integrations.reconnectNeeded')}
              </span>
            )}
          </div>
          <p className="text-[11px] text-[rgba(245,247,251,0.5)] mt-0.5">
            {t('settings.integrations.linearDescription')}
          </p>
        </div>
      </div>

      {/* Unauthorized banner */}
      {isUnauthorized && (
        <div className="mb-4 flex items-start gap-2 px-3 py-2 rounded-lg bg-[rgba(239,68,68,0.08)] border border-[rgba(239,68,68,0.25)]">
          <AlertTriangle className="w-4 h-4 text-[#f87171] flex-shrink-0 mt-0.5" />
          <div className="text-[11px] text-[#f87171]">
            {t('settings.integrations.unauthorizedBanner')}
          </div>
        </div>
      )}

      {/* Connected view */}
      {mode === 'view' && isConnected && integration && (
        <div className="space-y-3">
          <div className="flex items-center justify-between py-2 border-b border-[rgba(255,255,255,0.04)]">
            <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('settings.integrations.account')}</span>
            <span className="text-[11px] text-[#f5f7fb] truncate max-w-[200px]">
              {integration.externalUserName ?? integration.externalUserId}
            </span>
          </div>
          {integration.externalUserEmail && (
            <div className="flex items-center justify-between py-2 border-b border-[rgba(255,255,255,0.04)]">
              <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('settings.profile.email')}</span>
              <span className="text-[11px] text-[#f5f7fb] truncate max-w-[200px]">{integration.externalUserEmail}</span>
            </div>
          )}
          <div className="flex items-center justify-between py-2 border-b border-[rgba(255,255,255,0.04)]">
            <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('settings.integrations.connectedAt')}</span>
            <span className="text-[11px] text-[#f5f7fb]">{formatDateTime(integration.connectedAt)}</span>
          </div>
          <div className="flex items-center justify-between py-2">
            <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('settings.integrations.lastSync')}</span>
            <span className="text-[11px] text-[#f5f7fb]">
              {integration.lastSyncAt ? formatRelative(integration.lastSyncAt, t) : '—'}
            </span>
          </div>

          {lastSyncResult && (
            <div className="px-3 py-2 rounded-lg bg-[rgba(5,223,114,0.05)] border border-[rgba(5,223,114,0.15)] text-[10px] text-[rgba(245,247,251,0.65)]">
              {t('settings.integrations.lastResult', {
                projects: lastSyncResult.projectsCreated,
                tasks: lastSyncResult.tasksCreated,
                updated: lastSyncResult.tasksUpdated,
                ms: lastSyncResult.durationMs,
              })}
            </div>
          )}

          <div className="flex items-center gap-2 pt-2">
            <button
              onClick={handleSync}
              disabled={busy}
              className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-[rgba(139,92,246,0.15)] hover:bg-[rgba(139,92,246,0.25)] text-[11px] font-medium text-[#c4b5fd] disabled:opacity-40 transition-colors"
            >
              {busy ? <Loader2 className="w-3 h-3 animate-spin" /> : <RefreshCw className="w-3 h-3" />}
              {t('settings.integrations.syncNow')}
            </button>
            <button
              onClick={handleDisconnect}
              disabled={busy}
              className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-[rgba(255,255,255,0.04)] hover:bg-[rgba(239,68,68,0.12)] text-[11px] text-[rgba(245,247,251,0.6)] hover:text-[#f87171] disabled:opacity-40 transition-colors"
            >
              <Unplug className="w-3 h-3" />
              {t('settings.integrations.disconnect')}
            </button>
          </div>

          {/* Sync history — expandable */}
          <div className="pt-3 mt-3 border-t border-[rgba(255,255,255,0.04)]">
            <button
              type="button"
              onClick={toggleHistory}
              className="w-full flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.45)] hover:text-[rgba(245,247,251,0.75)] transition-colors"
            >
              {historyOpen ? <ChevronDown className="w-3 h-3" /> : <ChevronRight className="w-3 h-3" />}
              {t('settings.integrations.syncHistory')}
            </button>
            {historyOpen && (
              <div className="mt-2">
                {historyLoading ? (
                  <div className="flex items-center gap-2 text-[10px] text-[rgba(245,247,251,0.5)] py-2">
                    <Loader2 className="w-3 h-3 animate-spin" />
                    {t('settings.integrations.historyLoading')}
                  </div>
                ) : history && history.length > 0 ? (
                  <ul className="space-y-1.5 max-h-[220px] overflow-y-auto pr-1">
                    {history.map((h) => (
                      <SyncHistoryRow key={h.id} entry={h} />
                    ))}
                  </ul>
                ) : (
                  <p className="text-[10px] italic text-[rgba(245,247,251,0.35)] py-2">
                    {t('settings.integrations.noHistory')}
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Connect form */}
      {mode === 'connect' && (
        <form onSubmit={handleConnect} className="space-y-3">
          {/* OAuth connect */}
          <div className="space-y-2">
            <button
              type="button"
              onClick={handleOAuthConnect}
              disabled={busy || oauthPolling}
              className="w-full flex items-center justify-center gap-2 px-3 py-2.5 rounded-md bg-gradient-to-r from-[#5e6ad2] to-[#a78bfa] hover:from-[#4f5bc4] hover:to-[#9678f0] text-[12px] font-medium text-white disabled:opacity-40 transition-colors"
            >
              {oauthPolling ? (
                <>
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  {t('settings.integrations.connecting')}
                </>
              ) : (
                <>
                  <Zap className="w-3.5 h-3.5" />
                  {t('settings.integrations.oauthButton')}
                </>
              )}
            </button>
            {oauthPolling && (
              <p className="text-[10px] text-[rgba(245,247,251,0.45)] text-center">
                {t('settings.integrations.oauthWaiting')}
              </p>
            )}
          </div>

          <div className="flex items-center gap-3">
            <div className="flex-1 h-px bg-[rgba(255,255,255,0.06)]" />
            <span className="text-[10px] text-[rgba(245,247,251,0.35)]">{t('settings.integrations.orApiKey')}</span>
            <div className="flex-1 h-px bg-[rgba(255,255,255,0.06)]" />
          </div>

          <div className="text-[11px] text-[rgba(245,247,251,0.65)] space-y-1.5">
            <p className="font-medium text-[rgba(245,247,251,0.85)]">{t('settings.integrations.howToGetKey')}</p>
            <ol className="list-decimal list-inside space-y-0.5 pl-1">
              <li>Linear → Settings → Account → Security & Access</li>
              <li>"Personal API keys" → <span className="font-mono text-[10px]">TimeTrack</span></li>
              <li>{t('settings.integrations.linearKeyPrompt')}</li>
            </ol>
            <a
              href="https://linear.app/settings/account/security"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-[10px] text-[#c4b5fd] hover:text-[#a78bfa] mt-1"
            >
              {t('settings.integrations.openLinearSettings')}
              <ExternalLink className="w-2.5 h-2.5" />
            </a>
          </div>

          <div className="relative">
            <input
              type={showKey ? 'text' : 'password'}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="lin_api_…"
              disabled={busy}
              className="w-full px-3 py-2 pr-10 rounded-md bg-[rgba(11,13,20,0.6)] border border-[rgba(255,255,255,0.08)] text-[12px] text-[#f5f7fb] placeholder:text-[rgba(245,247,251,0.25)] font-mono focus:outline-none focus:border-[rgba(139,92,246,0.4)] disabled:opacity-50"
              autoComplete="off"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => setShowKey((v) => !v)}
              className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-[rgba(245,247,251,0.5)] hover:text-[#f5f7fb]"
              aria-label={showKey ? t('settings.integrations.hideKey') : t('settings.integrations.showKey')}
            >
              {showKey ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
            </button>
          </div>

          {formError && (
            <div className="flex items-start gap-2 px-2.5 py-2 rounded-md bg-[rgba(239,68,68,0.08)] border border-[rgba(239,68,68,0.2)]">
              <AlertTriangle className="w-3.5 h-3.5 text-[#f87171] flex-shrink-0 mt-0.5" />
              <span className="text-[11px] text-[#f87171]">{formError}</span>
            </div>
          )}

          <div className="flex items-center gap-2 pt-1">
            <button
              type="submit"
              disabled={busy || !apiKey.trim()}
              className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-gradient-to-r from-[#8B5CF6] to-[#a78bfa] hover:from-[#7c3aed] hover:to-[#8B5CF6] text-[11px] font-medium text-white disabled:opacity-40 transition-colors"
            >
              {busy ? <Loader2 className="w-3 h-3 animate-spin" /> : <Zap className="w-3 h-3" />}
              {isUnauthorized || isConnected ? t('settings.integrations.reconnect') : t('settings.integrations.connect')}
            </button>
            {isConnected && (
              <button
                type="button"
                onClick={() => {
                  setMode('view');
                  setFormError(null);
                  setApiKey('');
                }}
                disabled={busy}
                className="px-3 py-1.5 rounded-md bg-[rgba(255,255,255,0.04)] hover:bg-[rgba(255,255,255,0.08)] text-[11px] text-[rgba(245,247,251,0.6)] disabled:opacity-40"
              >
                {t('settings.integrations.cancel')}
              </button>
            )}
          </div>
        </form>
      )}
    </div>
  );
}

// ── helpers ────────────────────────────────────────────────────────────────

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatRelative(iso: string, t: (key: string, opts?: Record<string, unknown>) => string): string {
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (diff < 60) return t('settings.integrations.relativeNow');
  if (diff < 3600) return t('settings.integrations.relativeMin', { n: Math.floor(diff / 60) });
  if (diff < 86400) return t('settings.integrations.relativeHour', { n: Math.floor(diff / 3600) });
  return t('settings.integrations.relativeDay', { n: Math.floor(diff / 86400) });
}

function SyncHistoryRow({ entry }: { entry: LinearSyncHistoryEntry }) {
  const { t } = useTranslation();
  const iconColor = entry.success ? '#05df72' : '#f87171';
  const Icon = entry.success ? CheckCircle2 : XCircle;

  return (
    <li className="px-2.5 py-1.5 rounded-md bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)]">
      <div className="flex items-start gap-2">
        <Icon className="w-3 h-3 mt-0.5 flex-shrink-0" style={{ color: iconColor }} />
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <span className="text-[10px] text-[rgba(245,247,251,0.7)]">{formatDateTime(entry.startedAt)}</span>
            <span className="text-[9px] text-[rgba(245,247,251,0.4)]">{entry.durationMs}ms</span>
          </div>
          {entry.success ? (
            <div className="text-[9px] text-[rgba(245,247,251,0.55)] mt-0.5">
              {t('settings.integrations.historySuccess', {
                projects: entry.projectsCreated,
                tasks: entry.tasksCreated,
                updated: entry.tasksUpdated,
              })}
              {entry.tasksSoftDeleted > 0 && t('settings.integrations.historyDeletedSuffix', { count: entry.tasksSoftDeleted })}
            </div>
          ) : (
            <div className="text-[9px] text-[#f87171] mt-0.5 break-words">{entry.errorMessage ?? t('settings.integrations.historyFailed')}</div>
          )}
        </div>
      </div>
    </li>
  );
}

function extractMessage(err: unknown): string | null {
  if (err && typeof err === 'object') {
    const e = err as { message?: unknown; body?: { errors?: Record<string, string[]>; message?: unknown } };
    if (e.body?.errors) {
      const first = Object.values(e.body.errors).find((v) => Array.isArray(v) && v.length);
      if (first && first.length) return first[0];
    }
    if (typeof e.body?.message === 'string') return e.body.message;
    if (typeof e.message === 'string') return e.message;
  }
  return null;
}
