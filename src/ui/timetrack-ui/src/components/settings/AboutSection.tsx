import { Info, Download, ExternalLink, HardDrive } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAgentStatus } from '../../hooks/useAgentStatus';

/**
 * AboutSection - Aba Sobre/Diagnóstico
 */
export function AboutSection() {
  const { t } = useTranslation();
  const { status } = useAgentStatus();

  const handleExportLogs = async () => {
    // TODO: Implement export logs via IPC
    console.log('[Settings] Export logs requested');
  };

  const handleOpenStatus = () => {
    window.location.href = '/status';
  };

  const agentVersion = status.version !== '-' ? status.version : '—';
  const desktopHostVersion = status.desktopHostVersion ?? '—';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.about.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.about.subtitle')}
        </p>
      </div>

      {/* Version Info Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center">
            <Info className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.about.versions')}</h3>
        </div>

        <div className="space-y-3">
          <div className="flex items-center justify-between py-2 border-b border-[rgba(255,255,255,0.04)]">
            <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('settings.about.agentService')}</span>
            <span className="text-[13px] font-medium text-[#f5f7fb]">{agentVersion}</span>
          </div>
          <div className="flex items-center justify-between py-2 border-b border-[rgba(255,255,255,0.04)]">
            <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('settings.about.desktopHost')}</span>
            <span className="text-[13px] font-medium text-[#f5f7fb]">{desktopHostVersion}</span>
          </div>
          <div className="flex items-center justify-between py-2">
            <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{t('settings.about.deviceId')}</span>
            <code className="text-[12px] font-mono text-[#8B5CF6] bg-[rgba(139,92,246,0.1)] px-2 py-0.5 rounded">
              {t('settings.about.viewAgentStatus')}
            </code>
          </div>
        </div>
      </div>

      {/* Diagnostics Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
            <HardDrive className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.about.diagnostics')}</h3>
        </div>

        <div className="space-y-3">
          {/* Export Logs Button */}
          <button
            onClick={handleExportLogs}
            className="w-full flex items-center justify-between py-3 px-4 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <div className="flex items-center gap-3">
              <Download className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              <span className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('settings.about.exportLogs')}</span>
            </div>
            <span className="text-[11px] text-[rgba(245,247,251,0.4)]">.zip</span>
          </button>

          {/* Status Page Link */}
          <button
            onClick={handleOpenStatus}
            className="w-full flex items-center justify-between py-3 px-4 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <div className="flex items-center gap-3">
              <ExternalLink className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              <span className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('settings.about.viewStatus')}</span>
            </div>
            <span className="text-[11px] text-[rgba(245,247,251,0.4)]">/status</span>
          </button>
        </div>
      </div>
    </div>
  );
}
