import { useState } from 'react';
import { EyeOff, Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useHiddenAppsStore } from '../../stores/hiddenAppsStore';

export function TimelineSection() {
  const { t } = useTranslation();
  const { hiddenApps, hideApp, showApp } = useHiddenAppsStore();
  const [newApp, setNewApp] = useState('');

  const handleAdd = () => {
    const trimmed = newApp.trim();
    if (trimmed && !hiddenApps.includes(trimmed.toLowerCase())) {
      hideApp(trimmed);
      setNewApp('');
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') handleAdd();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.timeline.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.timeline.subtitle')}
        </p>
      </div>

      {/* Hidden Apps Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center">
            <EyeOff className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">Apps ocultos</h3>
        </div>

        <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-4">
          Apps nesta lista não aparecem na timeline de atividades nem no heatmap de produtividade.
          Use o nome do processo (ex: TimeTrack.DesktopHost, msedge).
        </p>

        {/* Add new app */}
        <div className="flex gap-2 mb-4">
          <input
            type="text"
            value={newApp}
            onChange={(e) => setNewApp(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Nome do processo (ex: Spotify)"
            className="flex-1 px-3 py-2 rounded-lg text-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.5)]"
          />
          <button
            onClick={handleAdd}
            disabled={!newApp.trim()}
            className="px-3 py-2 rounded-lg text-[12px] font-medium bg-[rgba(139,92,246,0.2)] border border-[rgba(139,92,246,0.5)] text-[#8B5CF6] hover:bg-[rgba(139,92,246,0.3)] disabled:opacity-30 disabled:cursor-not-allowed transition-all"
          >
            <Plus className="w-4 h-4" />
          </button>
        </div>

        {/* List of hidden apps */}
        <div className="space-y-1">
          {hiddenApps.length === 0 ? (
            <p className="text-[11px] text-[rgba(245,247,251,0.3)] text-center py-4">
              Nenhum app oculto
            </p>
          ) : (
            hiddenApps.map((app) => (
              <div
                key={app}
                className="flex items-center justify-between px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)]"
              >
                <span className="text-[12px] text-[rgba(245,247,251,0.7)]">{app}</span>
                <button
                  onClick={() => showApp(app)}
                  className="p-1 rounded-md text-[rgba(245,247,251,0.3)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.1)] transition-colors"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
