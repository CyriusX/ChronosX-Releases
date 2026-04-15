import { useState, useEffect, useRef } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import { SkeletonShimmer } from '../ui/SkeletonShimmer';
import { SettingsSidebar } from './SettingsSidebar';
import { ProfileSection } from './ProfileSection';
import { GeneralSection } from './GeneralSection';
import { NotificationsSection } from './NotificationsSection';
import { FocusTimerSection } from './FocusTimerSection';
import { AboutSection } from './AboutSection';
import { MembersSection } from './MembersSection';
import { OrganizationSection } from './OrganizationSection';
import { AgentStatusSection } from './AgentStatusSection';
import { TimelineSection } from './TimelineSection';
import { IntegrationsSection } from './IntegrationsSection';
import { MaintenanceSection } from './MaintenanceSection';
import { useIpc } from '../../hooks/useIpc';
import { useNotifications } from '../../stores/uiStore';
import { usePermissions } from '../../hooks/usePermissions';
import { useAuthStore } from '../../stores/authStore';
import { getOrgPolicy, updateOrgPolicy } from '../../services/policyApi';
import type { LocalSettings, UpdateLocalSettingsRequest, OrgPolicyResponse, UpdateOrgPolicyRequest } from '../../types/settings';
import type { SettingsSection } from '../../types/settingsNav';

/**
 * SettingsPage - Página de configurações
 *
 * Layout:
 * - Settings sidebar à esquerda (navegação de seções)
 * - Área de conteúdo scrollável à direita
 *
 * Seções:
 * - Pessoal: Perfil, Geral, Notificações, Foco & Timer
 * - Gestão (Admin/Gestor): Equipe, Organização
 * - Sistema: Sobre
 */
export function SettingsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { sendQuery, sendCommand } = useIpc();
  const { notify } = useNotifications();
  const { canManageTeam, canViewOrgPolicies, canEditOrgPolicies } = usePermissions();
  const user = useAuthStore((state) => state.user);
  const contentRef = useRef<HTMLDivElement>(null);

  const [activeSection, setActiveSection] = useState<SettingsSection>('profile');
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
      if (user?.orgId) {
        try {
          const policyResponse = await getOrgPolicy(user.orgId);
          setPolicy(policyResponse);
          setPolicyError(null);
        } catch (error) {
          console.error('[Settings] Error fetching policies:', error);
          setPolicyError(t('settings.policyLoadFailed'));
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
        console.error('[Settings] Failed to update:', result.error);
        notify.error(t('settings.saveFailed'));
        await loadSettings();
      } else {
        notify.success(t('settings.saveSuccess'));
      }
    } catch (error) {
      console.error('[Settings] Error updating settings:', error);
      notify.error(t('settings.saveFailed'));
      await loadSettings();
    }
  };

  const handleUpdatePolicy = async (request: UpdateOrgPolicyRequest) => {
    if (!user?.orgId) {
      notify.error(t('settings.policyUpdateFailed'));
      return;
    }

    try {
      const updatedPolicy = await updateOrgPolicy(user.orgId, request);
      setPolicy(updatedPolicy);
      notify.success(t('settings.policiesUpdated'));
    } catch (error) {
      console.error('[Settings] Error updating policy:', error);
      notify.error(t('settings.policyUpdateFailed'));
      throw error;
    }
  };

  const handleGoBack = () => {
    navigate(-1);
  };

  const handleSectionChange = (section: SettingsSection) => {
    setActiveSection(section);
    contentRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const renderSection = () => {
    switch (activeSection) {
      case 'profile':
        return <ProfileSection />;
      case 'general':
        return settings ? (
          <GeneralSection settings={settings} onUpdate={handleUpdateSettings} />
        ) : null;
      case 'notifications':
        return settings ? (
          <NotificationsSection settings={settings} onUpdate={handleUpdateSettings} />
        ) : null;
      case 'timeline':
        return <TimelineSection />;
      case 'focus-timer':
        return <FocusTimerSection />;
      case 'integrations':
        return <IntegrationsSection />;
      case 'team':
        return canManageTeam ? <MembersSection /> : null;
      case 'organization':
        return canViewOrgPolicies ? (
          policy ? (
            <OrganizationSection
              policy={policy}
              onUpdate={handleUpdatePolicy}
              canEdit={canEditOrgPolicies}
              orgId={user?.orgId as string}
            />
          ) : policyError ? (
            <div className="space-y-6">
              <div>
                <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.organization.title')}</h2>
                <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
                  {t('settings.organization.subtitle')}
                </p>
              </div>
              <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,107,107,0.2)] rounded-2xl p-6">
                <p className="text-[14px] text-[#ff6b6b]">{policyError}</p>
                <button
                  onClick={loadSettings}
                  className="mt-3 text-[12px] text-[#8B5CF6] hover:underline"
                >
                  {t('common.retry')}
                </button>
              </div>
            </div>
          ) : null
        ) : null;
      case 'about':
        return <AboutSection />;
      case 'maintenance':
        return <MaintenanceSection />;
      case 'agent-status':
        return <AgentStatusSection />;
      default:
        return null;
    }
  };

  if (isLoading) {
    return (
      <div className="flex-1 flex">
        {/* Sidebar skeleton */}
        <div className="hidden md:flex flex-col w-[220px] flex-shrink-0 border-r border-[rgba(255,255,255,0.04)] p-4 gap-3">
          <SkeletonShimmer width={80} height={12} />
          {Array.from({ length: 4 }).map((_, i) => (
            <SkeletonShimmer key={i} height={36} rounded="rounded-lg" />
          ))}
          <div className="mt-2" />
          <SkeletonShimmer width={80} height={12} />
          {Array.from({ length: 2 }).map((_, i) => (
            <SkeletonShimmer key={`m${i}`} height={36} rounded="rounded-lg" />
          ))}
        </div>
        {/* Content skeleton */}
        <div className="flex-1 p-6 space-y-6">
          <SkeletonShimmer width={200} height={24} />
          <div className="space-y-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <SkeletonShimmer key={i} height={80} rounded="rounded-xl" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  return (
    <main className="flex-1 flex flex-col md:flex-row overflow-hidden">
      {/* Settings Sidebar */}
      <SettingsSidebar
        activeSection={activeSection}
        onSectionChange={handleSectionChange}
        canManageTeam={canManageTeam}
        canViewOrgPolicies={canViewOrgPolicies}
      />

      {/* Content Area */}
      <div ref={contentRef} className="flex-1 overflow-auto p-6">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3 }}
          className="flex items-center justify-between mb-6"
        >
          <div className="flex items-center gap-4">
            <motion.button
              whileHover={{ scale: 1.08 }}
              whileTap={{ scale: 0.95 }}
              onClick={handleGoBack}
              className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
            </motion.button>
            <div>
              <h1 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.title')}</h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                {t('settings.subtitle')}
              </p>
            </div>
          </div>
        </motion.div>

        {/* Section Content */}
        <AnimatePresence mode="wait">
          <motion.div
            key={activeSection}
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -8 }}
            transition={{ duration: 0.2 }}
          >
            {renderSection()}
          </motion.div>
        </AnimatePresence>
      </div>
    </main>
  );
}
