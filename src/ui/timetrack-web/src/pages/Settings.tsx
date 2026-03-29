/**
 * Settings Page — Web Admin Portal
 *
 * Only shows organization management sections:
 * - Members (invite, list, activate/deactivate, change role)
 * - Organization Policies (work hours, idle threshold, retention, app exclusions, focus mode)
 * - App Categories
 *
 * No: Profile, General (local), Notifications (desktop-only), Focus Timer (personal), About
 */

import { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Users, Building2, ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { WebSidebar } from '../components/WebSidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useAuthStore } from '../stores/authStore';
import { getOrgPolicy, updateOrgPolicy } from '../services/policyApi';
import { MembersSection } from '@desktop/components/settings/MembersSection';
import { OrganizationSection } from '@desktop/components/settings/OrganizationSection';
import { SkeletonShimmer } from '@desktop/components/ui/SkeletonShimmer';
import { SPRING } from '@desktop/lib/animation';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest } from '@desktop/types/settings';

type WebSettingsSection = 'team' | 'organization';

const SECTIONS: { id: WebSettingsSection; label: string; icon: typeof Users }[] = [
  { id: 'team', label: 'Equipe', icon: Users },
  { id: 'organization', label: 'Organizacao', icon: Building2 },
];

export default function Settings() {
  const navigate = useNavigate();
  const { canEditOrgPolicies } = usePermissions();
  const user = useAuthStore((state) => state.user);
  const contentRef = useRef<HTMLDivElement>(null);

  const [activeSection, setActiveSection] = useState<WebSettingsSection>('team');
  const [policy, setPolicy] = useState<OrgPolicyResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [policyError, setPolicyError] = useState<string | null>(null);

  useEffect(() => {
    loadPolicy();
  }, []);

  const loadPolicy = async () => {
    setIsLoading(true);
    try {
      if (user?.orgId) {
        const policyResponse = await getOrgPolicy(user.orgId);
        setPolicy(policyResponse);
        setPolicyError(null);
      }
    } catch (error) {
      console.error('[Settings] Error fetching policies:', error);
      setPolicyError('Nao foi possivel carregar as politicas da organizacao');
    } finally {
      setIsLoading(false);
    }
  };

  const handleUpdatePolicy = async (request: UpdateOrgPolicyRequest) => {
    if (!user?.orgId) return;

    try {
      const updatedPolicy = await updateOrgPolicy(user.orgId, request);
      setPolicy(updatedPolicy);
    } catch (error) {
      console.error('[Settings] Error updating policy:', error);
      throw error;
    }
  };

  const handleSectionChange = (section: WebSettingsSection) => {
    setActiveSection(section);
    contentRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const renderSection = () => {
    switch (activeSection) {
      case 'team':
        return <MembersSection />;
      case 'organization':
        return policy ? (
          <OrganizationSection
            policy={policy}
            onUpdate={handleUpdatePolicy}
            canEdit={canEditOrgPolicies}
            orgId={user?.orgId as string}
          />
        ) : policyError ? (
          <div className="space-y-6">
            <div>
              <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Organizacao</h2>
              <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
                Politicas e configuracoes da organizacao
              </p>
            </div>
            <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,107,107,0.2)] rounded-2xl p-6">
              <p className="text-[14px] text-[#ff6b6b]">{policyError}</p>
              <button onClick={loadPolicy} className="mt-3 text-[12px] text-[#8B5CF6] hover:underline">
                Tentar novamente
              </button>
            </div>
          </div>
        ) : null;
      default:
        return null;
    }
  };

  return (
    <div className="flex h-screen bg-[#0b0d14]">
      <WebSidebar />

      <main className="flex-1 flex flex-col md:flex-row overflow-hidden">
        {/* Settings Sidebar */}
        <nav className="hidden md:flex flex-col w-[220px] flex-shrink-0 border-r border-[rgba(255,255,255,0.04)] bg-gradient-to-b from-[rgba(11,13,20,0.3)] to-[rgba(17,19,28,0.3)] overflow-y-auto py-4 px-3 gap-1">
          <p className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.3)] px-3 pt-3 pb-1 font-medium">
            Gestao
          </p>
          {SECTIONS.map((section) => (
            <motion.button
              key={section.id}
              onClick={() => handleSectionChange(section.id)}
              whileHover={{ x: activeSection === section.id ? 0 : 3 }}
              transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
              className={`w-full flex items-center gap-3 px-3 py-[10px] rounded-[10px] transition-colors relative ${
                activeSection === section.id
                  ? 'text-[#f5f7fb]'
                  : 'text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)]'
              }`}
            >
              {activeSection === section.id && (
                <motion.div
                  layoutId="web-settings-nav-active"
                  className="absolute inset-0 bg-[#1c1f2e] rounded-[10px]"
                  transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
                />
              )}
              <section.icon className="w-4 h-4 relative z-10" />
              <span className="relative z-10 text-[13px] font-medium">{section.label}</span>
            </motion.button>
          ))}
        </nav>

        {/* Mobile nav */}
        <div className="flex md:hidden overflow-x-auto gap-1 px-4 py-3 border-b border-[rgba(255,255,255,0.04)] bg-[rgba(26,29,46,0.4)]">
          {SECTIONS.map((section) => (
            <button
              key={section.id}
              onClick={() => handleSectionChange(section.id)}
              className={`relative flex items-center gap-2 px-4 py-2 rounded-lg text-[13px] font-medium whitespace-nowrap transition-colors ${
                activeSection === section.id
                  ? 'text-[#f5f7fb] bg-[rgba(139,92,246,0.15)]'
                  : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
              }`}
            >
              <section.icon className="w-4 h-4" />
              <span>{section.label}</span>
            </button>
          ))}
        </div>

        {/* Content */}
        <div ref={contentRef} className="flex-1 overflow-auto p-6">
          <motion.div
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
            className="flex items-center gap-4 mb-6"
          >
            <motion.button
              whileHover={{ scale: 1.08 }}
              whileTap={{ scale: 0.95 }}
              onClick={() => navigate(-1)}
              className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
            </motion.button>
            <div>
              <h1 className="text-[20px] font-semibold text-[#f5f7fb]">Configuracoes</h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                Gerencie a equipe e politicas da organizacao
              </p>
            </div>
          </motion.div>

          {isLoading ? (
            <div className="space-y-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <SkeletonShimmer key={i} height={80} rounded="rounded-xl" />
              ))}
            </div>
          ) : (
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
          )}
        </div>
      </main>
    </div>
  );
}
