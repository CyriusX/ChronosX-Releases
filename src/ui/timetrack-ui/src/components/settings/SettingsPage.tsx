import { useState, useEffect } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { SettingsTabs, type SettingsTab } from './SettingsTabs';
import { PreferencesSection } from './PreferencesSection';
import { AboutSection } from './AboutSection';
import { MembersSection } from './MembersSection';
import { PolicyCards } from './PolicyCards';
import { AppCategoriesSection } from './AppCategoriesSection';
import { useIpc } from '../../hooks/useIpc';
import { useNotifications } from '../../stores/uiStore';
import { usePermissions } from '../../hooks/usePermissions';
import { useAuthStore, selectAccessToken } from '../../stores/authStore';
import { getOrgPolicy, updateOrgPolicy } from '../../services/policyApi';
import type { LocalSettings, UpdateLocalSettingsRequest, OrgPolicyResponse, UpdateOrgPolicyRequest } from '../../types/settings';

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
 * - DIP: Usa hooks para comunicação
 * - OCP: Extensível para novas abas
 *
 * Composition:
 * - Composição de SettingsTabs + Sections + PolicyCards
 */
export function SettingsPage() {
  const navigate = useNavigate();
  const { sendQuery, sendCommand } = useIpc();
  const { notify } = useNotifications();
  const { canManageTeam, canViewOrgPolicies, canEditOrgPolicies } = usePermissions();
  const accessToken = useAuthStore(selectAccessToken);
  const user = useAuthStore((state) => state.user);

  const [activeTab, setActiveTab] = useState<SettingsTab>('preferences');
  const [settings, setSettings] = useState<LocalSettings | null>(null);
  const [policy, setPolicy] = useState<OrgPolicyResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [policyError, setPolicyError] = useState<string | null>(null);

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

      // Fetch org policies from backend API
      if (accessToken && user?.orgId) {
        try {
          const policyResponse = await getOrgPolicy(accessToken, user.orgId);
          setPolicy(policyResponse);
          setPolicyError(null);
        } catch (error) {
          console.error('[Settings] Error fetching policies:', error);
          setPolicyError('Não foi possível carregar as políticas da organização');
          // Keep policy as null to show error state
        }
      }
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

  const handleUpdatePolicy = async (request: UpdateOrgPolicyRequest) => {
    if (!accessToken || !user?.orgId) {
      notify.error('Erro ao atualizar políticas');
      return;
    }

    try {
      const updatedPolicy = await updateOrgPolicy(accessToken, user.orgId, request);
      setPolicy(updatedPolicy);
      notify.success('Políticas atualizadas');
    } catch (error) {
      console.error('[Settings] Error updating policy:', error);
      notify.error('Erro ao atualizar políticas');
      throw error; // Re-throw to let the component handle it
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
          showAppsTab={canViewOrgPolicies}
        />
      </div>

      {/* Content Area */}
      <div className="space-y-8">
        {/* Tab Content */}
        {activeTab === 'preferences' && settings && (
          <PreferencesSection settings={settings} onUpdate={handleUpdateSettings} />
        )}
        {activeTab === 'members' && canManageTeam && <MembersSection />}
        {activeTab === 'apps' && canViewOrgPolicies && user && user.orgId && (
          <AppCategoriesSection accessToken={accessToken} orgId={user.orgId as string} />
        )}
        {activeTab === 'about' && <AboutSection />}

        {/* Policy Cards - Apenas Admin/Gestor */}
        {canViewOrgPolicies && (
          policy ? (
            <PolicyCards
              policy={policy}
              onUpdate={handleUpdatePolicy}
              canEdit={canEditOrgPolicies}
            />
          ) : policyError ? (
            <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,107,107,0.2)] rounded-2xl p-6">
              <p className="text-[14px] text-[#ff6b6b]">{policyError}</p>
              <button
                onClick={loadSettings}
                className="mt-3 text-[12px] text-[#4ad9ff] hover:underline"
              >
                Tentar novamente
              </button>
            </div>
          ) : null
        )}
      </div>
    </main>
  );
}
