import { useState, useEffect } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { SettingsTabs, type SettingsTab } from './SettingsTabs';
import { PreferencesSection } from './PreferencesSection';
import { AboutSection } from './AboutSection';
import { MembersSection } from './MembersSection';
import { PolicyCards } from './PolicyCards';
import { useIpc } from '../../hooks/useIpc';
import { useNotifications } from '../../stores/uiStore';
import { usePermissions } from '../../hooks/usePermissions';
import type { LocalSettings, OrgPolicies, UpdateLocalSettingsRequest } from '../../types/settings';

/**
 * SettingsPage - Página de configurações
 *
 * Layout:
 * - Sidebar à esquerda (reutilizada do Dashboard)
 * - Área principal com:
 *   - Header com título e botão voltar
 *   - Tabs (Preferências | Membros* | Sobre)
 *   - Conteúdo da aba
 *   - Grid de PolicyCards (políticas da organização)
 *
 * * Membros visível apenas para Admin/Gestor
 *
 * SOLID:
 * - SRP: Apenas orquestração da UI de settings
 * - DIP: Usa useIpc hook para comunicação
 * - OCP: Extensível para novas abas
 *
 * Composition:
 * - Composição de SettingsTabs + Sections + PolicyCards
 */
export function SettingsPage() {
  const navigate = useNavigate();
  const { sendQuery, sendCommand } = useIpc();
  const { notify } = useNotifications();
  const { canManageTeam, canViewOrgPolicies } = usePermissions();
  const [activeTab, setActiveTab] = useState<SettingsTab>('preferences');
  const [settings, setSettings] = useState<LocalSettings | null>(null);
  const [policies, setPolicies] = useState<OrgPolicies | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Load settings on mount
  useEffect(() => {
    loadSettings();
  }, []);

  const loadSettings = async () => {
    setIsLoading(true);
    try {
      // Query local settings
      const settingsResult = await sendQuery('getSettings');
      if (settingsResult.success && settingsResult.data) {
        setSettings(settingsResult.data as LocalSettings);
      }

      // TODO: Query org policies from backend when available
      // For now, use mock data
      setPolicies({
        workHours: {
          startHour: 9,
          endHour: 18,
          workDays: [1, 2, 3, 4, 5],
          timezone: 'America/Sao_Paulo',
        },
        idleThresholdSeconds: 180,
        focusMode: {
          enabled: false,
          mode: null,
        },
      });
    } catch (error) {
      console.error('[Settings] Error loading settings:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleUpdateSettings = async (updates: UpdateLocalSettingsRequest) => {
    if (!settings) return;

    // Optimistic update
    setSettings((prev) =>
      prev
        ? {
            ...prev,
            ...updates,
            updatedAt: new Date().toISOString(),
          }
        : null
    );

    try {
      const result = await sendCommand('updateSettings', updates);
      if (!result.success) {
        // Revert on error
        console.error('[Settings] Failed to update:', result.error);
        notify.error('Erro ao salvar configurações');
        await loadSettings(); // Reload from server
      } else {
        notify.success('Configurações salvas');
      }
    } catch (error) {
      console.error('[Settings] Error updating settings:', error);
      notify.error('Erro ao salvar configurações');
      await loadSettings();
    }
  };

  const handleGoBack = () => {
    navigate(-1);
  };

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-2 border-[#4ad9ff] border-t-transparent rounded-full animate-spin" />
          <span className="text-[13px] text-[rgba(245,247,251,0.5)]">Carregando...</span>
        </div>
      </div>
    );
  }

  return (
    <main className="flex-1 overflow-auto p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-4">
          <button
            onClick={handleGoBack}
            className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
          </button>
          <div>
            <h1 className="text-[20px] font-semibold text-[#f5f7fb]">Configurações</h1>
            <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
              Gerencie suas preferências pessoais
            </p>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="mb-6">
        <SettingsTabs
          activeTab={activeTab}
          onTabChange={setActiveTab}
          showMembersTab={canManageTeam}
        />
      </div>

      {/* Content Area */}
      <div className="space-y-8">
        {/* Tab Content */}
        {activeTab === 'preferences' && settings && (
          <PreferencesSection settings={settings} onUpdate={handleUpdateSettings} />
        )}
        {activeTab === 'members' && canManageTeam && <MembersSection />}
        {activeTab === 'about' && <AboutSection />}

        {/* Policy Cards - Apenas Admin/Gestor */}
        {policies && canViewOrgPolicies && <PolicyCards policies={policies} />}
      </div>
    </main>
  );
}
