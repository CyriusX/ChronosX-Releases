import { Download, ExternalLink, HardDrive, CheckCircle, AlertCircle, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAgentStatus } from '../../hooks/useAgentStatus';
import { useUpdate } from '../../hooks/useUpdate';

/**
 * MaintenanceSection — System updates and diagnostics
 */
export function MaintenanceSection() {
  const { t } = useTranslation();
  const { status } = useAgentStatus();
  const { updateInfo, progress, error, isChecking, isUpdating, checkForUpdates, startUpdate } = useUpdate();

  const agentVersion = status.version !== '-' ? status.version : '—';

  const handleExportLogs = async () => {
    // TODO: Implement export logs via IPC
    console.log('[Settings] Export logs requested');
  };

  const handleOpenStatus = () => {
    window.location.href = '/status';
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.maintenance.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.maintenance.subtitle')}
        </p>
      </div>

      {/* Update Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center">
            <Download className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.maintenance.update.title')}</h3>
        </div>

        {/* Current version */}
        <div className="flex items-center justify-between py-2 mb-4 border-b border-[rgba(255,255,255,0.04)]">
          <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('settings.maintenance.update.currentVersion')}</span>
          <span className="text-[13px] font-medium text-[#f5f7fb]">{agentVersion}</span>
        </div>

        {/* Check button (idle state) */}
        {!updateInfo && !isChecking && progress.stage === 'idle' && (
          <button
            onClick={checkForUpdates}
            className="w-full py-2.5 px-4 rounded-xl bg-[rgba(139,92,246,0.15)] border border-[rgba(139,92,246,0.3)] text-[13px] font-medium text-[#c4b5fd] hover:bg-[rgba(139,92,246,0.25)] transition-colors"
          >
            {t('settings.maintenance.update.checkButton')}
          </button>
        )}

        {/* Checking */}
        {isChecking && (
          <div className="flex items-center gap-3 py-3">
            <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
            <span className="text-[13px] text-[rgba(245,247,251,0.7)]">{t('settings.maintenance.update.checking')}</span>
          </div>
        )}

        {/* No update available */}
        {updateInfo && !updateInfo.hasUpdate && (
          <div className="flex items-center gap-3 py-3">
            <CheckCircle className="w-4 h-4 text-[#22c55e]" />
            <span className="text-[13px] text-[rgba(245,247,251,0.7)]">{t('settings.maintenance.update.upToDate')}</span>
          </div>
        )}

        {/* Update available */}
        {updateInfo && updateInfo.hasUpdate && !isUpdating && progress.stage !== 'complete' && (
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-[#f59e0b]" />
              <span className="text-[13px] font-medium text-[#f5f7fb]">{t('settings.maintenance.update.available')}</span>
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between py-1.5">
                <span className="text-[12px] text-[rgba(245,247,251,0.5)]">{t('settings.maintenance.update.newVersion')}</span>
                <span className="text-[12px] font-medium text-[#22D3EE]">{updateInfo.latestVersion}</span>
              </div>
              <div className="flex items-center justify-between py-1.5">
                <span className="text-[12px] text-[rgba(245,247,251,0.5)]">{t('settings.maintenance.update.fileSize')}</span>
                <span className="text-[12px] font-medium text-[rgba(245,247,251,0.8)]">{formatBytes(updateInfo.fileSizeBytes)}</span>
              </div>
            </div>

            {updateInfo.releaseNotes && (
              <div className="bg-[rgba(255,255,255,0.03)] rounded-xl p-3 border border-[rgba(255,255,255,0.04)]">
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-1">{t('settings.maintenance.update.releaseNotes')}</p>
                <p className="text-[12px] text-[rgba(245,247,251,0.7)] whitespace-pre-wrap">{updateInfo.releaseNotes}</p>
              </div>
            )}

            {error && (
              <p className="text-[12px] text-[#ff6b6b]">{error}</p>
            )}

            <button
              onClick={startUpdate}
              className="w-full py-2.5 px-4 rounded-xl bg-gradient-to-r from-[#8B5CF6] to-[#6D28D9] text-[13px] font-medium text-white hover:opacity-90 transition-opacity"
            >
              {t('settings.maintenance.update.installButton')}
            </button>
          </div>
        )}

        {/* Downloading / Installing progress */}
        {isUpdating && (
          <div className="space-y-3">
            <div className="flex items-center gap-3">
              <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
              <span className="text-[13px] text-[rgba(245,247,251,0.7)]">
                {progress.stage === 'downloading'
                  ? t('settings.maintenance.update.downloading')
                  : t('settings.maintenance.update.installing')}
              </span>
            </div>
            <div className="w-full h-2 bg-[rgba(255,255,255,0.06)] rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full transition-all duration-300"
                style={{ width: `${Math.max(progress.percentage, 2)}%` }}
              />
            </div>
            <div className="flex justify-between">
              <span className="text-[11px] text-[rgba(245,247,251,0.4)]">
                {progress.bytesDownloaded && progress.bytesTotal
                  ? `${formatBytes(progress.bytesDownloaded)} / ${formatBytes(progress.bytesTotal)}`
                  : progress.message}
              </span>
              <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{progress.percentage}%</span>
            </div>
          </div>
        )}

        {/* Complete */}
        {progress.stage === 'complete' && (
          <div className="flex items-center gap-3 py-3">
            <CheckCircle className="w-4 h-4 text-[#22c55e]" />
            <span className="text-[13px] text-[#22c55e]">{t('settings.maintenance.update.complete')}</span>
          </div>
        )}

        {/* Failed */}
        {progress.stage === 'failed' && !isUpdating && (
          <div className="space-y-3">
            <div className="flex items-center gap-3 py-2">
              <AlertCircle className="w-4 h-4 text-[#ff6b6b]" />
              <span className="text-[13px] text-[#ff6b6b]">{t('settings.maintenance.update.failed')}</span>
            </div>
            {error && (
              <p className="text-[12px] text-[rgba(245,247,251,0.5)] bg-[rgba(255,107,107,0.1)] rounded-lg p-3">{error}</p>
            )}
            <button
              onClick={checkForUpdates}
              className="w-full py-2.5 px-4 rounded-xl bg-[rgba(139,92,246,0.15)] border border-[rgba(139,92,246,0.3)] text-[13px] font-medium text-[#c4b5fd] hover:bg-[rgba(139,92,246,0.25)] transition-colors"
            >
              {t('settings.maintenance.update.checkButton')}
            </button>
          </div>
        )}
      </div>

      {/* Diagnostics Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
            <HardDrive className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.maintenance.diagnostics.title')}</h3>
        </div>

        <div className="space-y-3">
          {/* Export Logs */}
          <button
            onClick={handleExportLogs}
            className="w-full flex items-center justify-between py-3 px-4 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <div className="flex items-center gap-3">
              <Download className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              <span className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('settings.maintenance.diagnostics.exportLogs')}</span>
            </div>
            <span className="text-[11px] text-[rgba(245,247,251,0.4)]">.zip</span>
          </button>

          {/* Status Page */}
          <button
            onClick={handleOpenStatus}
            className="w-full flex items-center justify-between py-3 px-4 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <div className="flex items-center gap-3">
              <ExternalLink className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              <span className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('settings.maintenance.diagnostics.viewStatus')}</span>
            </div>
            <span className="text-[11px] text-[rgba(245,247,251,0.4)]">/status</span>
          </button>
        </div>
      </div>
    </div>
  );
}
