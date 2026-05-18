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
import { Users, Building2, ArrowLeft, ShieldAlert, Globe } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { WebSidebar } from '../components/WebSidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useAuthStore } from '../stores/authStore';
import { useLanguage } from '../hooks/useLanguage';
import { getOrgPolicy, updateOrgPolicy } from '../services/policyApi';
import { clearAllEvents } from '../services/maintenanceApi';
import { createPlatformApiKey, listPlatformApiKeys, revokePlatformApiKey, type PlatformApiKeyDto } from '../services/platformApiKeysApi';
import { MembersSection } from '@desktop/components/settings/MembersSection';
import { InviteLinkSection } from '../components/settings/InviteLinkSection';
import { OrganizationSection } from '@desktop/components/settings/OrganizationSection';
import { SkeletonShimmer } from '@desktop/components/ui/SkeletonShimmer';
import { SPRING } from '@desktop/lib/animation';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest, AppLanguage } from '@desktop/types/settings';

type WebSettingsSection = 'team' | 'organization' | 'maintenance' | 'preferences';

const SECTIONS: { id: WebSettingsSection; label: string; icon: typeof Users }[] = [
  { id: 'team', label: 'settings.members.title', icon: Users },
  { id: 'organization', label: 'settings.organization.title', icon: Building2 },
  { id: 'maintenance', label: 'maintenance.title', icon: ShieldAlert },
  { id: 'preferences', label: 'maintenance.preferences', icon: Globe },
];

// ── Maintenance log preferences stored in localStorage ──────────────────────
export const MAINTENANCE_PREFS_KEY = 'maintenance_log_prefs';

export interface MaintenanceLogPrefs {
  categories: string[];   // [] = all
  severities: string[];   // [] = all
  limit: number;
}

export function getMaintenanceLogPrefs(): MaintenanceLogPrefs {
  try {
    const raw = localStorage.getItem(MAINTENANCE_PREFS_KEY);
    if (raw) return JSON.parse(raw) as MaintenanceLogPrefs;
  } catch { /* ignore */ }
  return { categories: [], severities: [], limit: 100 };
}

function saveMaintenanceLogPrefs(prefs: MaintenanceLogPrefs) {
  localStorage.setItem(MAINTENANCE_PREFS_KEY, JSON.stringify(prefs));
}

export default function Settings() {
  const navigate = useNavigate();
  const { t } = useTranslation();
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
      setPolicyError(t('maintenance.policyError'));
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
        return (
          <>
            <MembersSection />
            <InviteLinkSection />
          </>
        );
      case 'maintenance':
        return <MaintenanceSection />;
      case 'preferences':
        return <PreferencesSection />;
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
              <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('maintenance.orgTitle')}</h2>
              <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
                {t('maintenance.orgSubtitle')}
              </p>
            </div>
            <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,107,107,0.2)] rounded-2xl p-6">
              <p className="text-[14px] text-[#ff6b6b]">{policyError}</p>
              <button onClick={loadPolicy} className="mt-3 text-[12px] text-[#8B5CF6] hover:underline">
                {t('common.retry')}
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
            {t('maintenance.management')}
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
              <span className="relative z-10 text-[13px] font-medium">{t(section.label)}</span>
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
              <span>{t(section.label)}</span>
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
              <h1 className="text-[20px] font-semibold text-[#f5f7fb]">{t('maintenance.headerTitle')}</h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                {t('maintenance.headerSubtitle')}
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

// ── Maintenance log preferences section ──────────────────────────────────────

const ALL_CATEGORIES = [
  { id: 'system', labelKey: 'maintenance.categorySystem' },
  { id: 'error', labelKey: 'maintenance.categoryErrors' },
  { id: 'user_action', labelKey: 'maintenance.categoryUserActions' },
  { id: 'network', labelKey: 'maintenance.categoryNetwork' },
  { id: 'performance', labelKey: 'maintenance.categoryPerformance' },
];

const ALL_SEVERITIES = [
  { id: 'info', labelKey: 'maintenance.severityInfo', color: 'text-[#4ade80]' },
  { id: 'warning', labelKey: 'maintenance.severityWarning', color: 'text-[#fbbf24]' },
  { id: 'error', labelKey: 'maintenance.severityError', color: 'text-[#f87171]' },
  { id: 'critical', labelKey: 'maintenance.severityCritical', color: 'text-[#ef4444]' },
];

const LIMIT_OPTIONS = [25, 50, 100, 200, 500];

function ToggleSwitch({ on, onClick, disabled }: { on: boolean; onClick: () => void; disabled?: boolean }) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`w-10 h-5 rounded-full transition-colors relative ${
        disabled ? 'opacity-40 cursor-not-allowed' : ''
      } ${on ? 'bg-[#8B5CF6]' : 'bg-[rgba(255,255,255,0.1)]'}`}
    >
      <span className={`absolute top-0.5 w-4 h-4 rounded-full bg-white shadow transition-all ${
        on ? 'left-5' : 'left-0.5'
      }`} />
    </button>
  );
}

function MaintenanceSection() {
  const { t } = useTranslation();
  const orgId = useAuthStore(s => s.user?.orgId);
  const isPlatformAdmin = useAuthStore(s => s.user?.isPlatformAdmin);
  const [prefs, setPrefs] = useState<MaintenanceLogPrefs>(getMaintenanceLogPrefs);
  const [saved, setSaved] = useState(false);
  const [clearing, setClearing] = useState(false);
  const [clearConfirm, setClearConfirm] = useState(false);
  const [clearResult, setClearResult] = useState<'success' | 'error' | null>(null);

  const [apiKeys, setApiKeys] = useState<PlatformApiKeyDto[]>([]);
  const [keysLoading, setKeysLoading] = useState(false);
  const [keysError, setKeysError] = useState<string | null>(null);
  const [newKeyLabel, setNewKeyLabel] = useState('');
  const [creatingKey, setCreatingKey] = useState(false);
  const [createdToken, setCreatedToken] = useState<string | null>(null);

  const getMcpEndpoint = () => {
    // Mirror apiClient.ts resolution
    let apiUrl: string | undefined;
    if (typeof window !== 'undefined' && window.__APP_CONFIG__?.VITE_API_URL) {
      apiUrl = window.__APP_CONFIG__.VITE_API_URL;
    }
    if (!apiUrl) {
      apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';
    }

    try {
      const url = new URL(apiUrl);
      url.pathname = url.pathname.replace(/\/+$/, '').replace(/\/api\/v\d+$/, '');
      const originAndPath = `${url.origin}${url.pathname}`.replace(/\/+$/, '');
      return `${originAndPath}/mcp`;
    } catch {
      const trimmed = apiUrl.replace(/\/+$/, '').replace(/\/api\/v\d+$/, '');
      return `${trimmed}/mcp`;
    }
  };

  const loadKeys = async () => {
    if (!isPlatformAdmin) return;
    setKeysLoading(true);
    setKeysError(null);
    try {
      const keys = await listPlatformApiKeys();
      setApiKeys(keys);
    } catch (e) {
      console.error('[Settings] Error listing platform api keys:', e);
      setKeysError('Failed to load keys');
    } finally {
      setKeysLoading(false);
    }
  };

  useEffect(() => {
    loadKeys();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isPlatformAdmin]);

  const handleCreateKey = async () => {
    const label = newKeyLabel.trim();
    if (!label) return;
    setCreatingKey(true);
    setKeysError(null);
    try {
      const created = await createPlatformApiKey(label);
      setCreatedToken(created.token);
      setNewKeyLabel('');
      await loadKeys();
    } catch (e) {
      console.error('[Settings] Error creating platform api key:', e);
      setKeysError('Failed to create key');
    } finally {
      setCreatingKey(false);
    }
  };

  const handleRevokeKey = async (keyId: string) => {
    if (!confirm('Revoke this key? It will stop working immediately.')) return;
    setKeysError(null);
    try {
      await revokePlatformApiKey(keyId);
      await loadKeys();
    } catch (e) {
      console.error('[Settings] Error revoking platform api key:', e);
      setKeysError('Failed to revoke key');
    }
  };

  const allCatsSelected = prefs.categories.length === 0;
  const allSevsSelected = prefs.severities.length === 0;

  const toggleSelectAll = (list: 'categories' | 'severities') => {
    const allIds = list === 'categories'
      ? ALL_CATEGORIES.map(c => c.id)
      : ALL_SEVERITIES.map(s => s.id);
    const isAll = list === 'categories' ? allCatsSelected : allSevsSelected;
    // Select All ON → switch to explicit all so individual toggles activate
    // Select All OFF → clear to [] (show all, implicit)
    setPrefs(prev => ({ ...prev, [list]: isAll ? allIds : [] }));
    setSaved(false);
  };

  const toggle = (list: 'categories' | 'severities', id: string) => {
    setPrefs(prev => {
      const current = prev[list];
      const next = current.includes(id) ? current.filter(x => x !== id) : [...current, id];
      return { ...prev, [list]: next };
    });
    setSaved(false);
  };

  const handleSave = () => {
    saveMaintenanceLogPrefs(prefs);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  const handleClearEvents = async () => {
    if (!orgId) return;
    setClearing(true);
    setClearResult(null);
    try {
      await clearAllEvents(orgId);
      setClearResult('success');
      setClearConfirm(false);
    } catch {
      setClearResult('error');
    } finally {
      setClearing(false);
      setTimeout(() => setClearResult(null), 3000);
    }
  };

  const sectionCard = 'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5 space-y-4';
  const rowBase = 'flex items-center justify-between py-2 border-b border-[rgba(255,255,255,0.04)] last:border-0';

  return (
    <div className="space-y-6 max-w-2xl">
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('maintenance.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('maintenance.subtitle')}
        </p>
      </div>

      {/* MCP / Ops integration (SysAdmin only) */}
      {isPlatformAdmin && (
        <div className={sectionCard}>
          <div>
            <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">Ops MCP</p>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
              Read-only MCP endpoint for Maintenance/Ops integrations.
            </p>
          </div>

          <div className="space-y-2">
            <div className="text-[12px] text-[rgba(245,247,251,0.7)]">Endpoint</div>
            <div className="flex items-center gap-2">
              <code className="flex-1 text-[12px] text-[rgba(245,247,251,0.85)] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] px-3 py-2 rounded-lg overflow-x-auto">
                {getMcpEndpoint()}
              </code>
              <button
                onClick={() => navigator.clipboard.writeText(getMcpEndpoint())}
                className="px-3 py-2 rounded-lg text-[12px] font-medium border border-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.7)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
              >
                Copy
              </button>
            </div>
          </div>

          {createdToken && (
            <div className="space-y-2">
              <div className="text-[12px] text-[rgba(245,247,251,0.7)]">New key (copy now — shown once)</div>
              <div className="flex items-center gap-2">
                <code className="flex-1 text-[12px] text-[rgba(245,247,251,0.85)] bg-[rgba(139,92,246,0.12)] border border-[rgba(139,92,246,0.25)] px-3 py-2 rounded-lg overflow-x-auto">
                  {createdToken}
                </code>
                <button
                  onClick={() => navigator.clipboard.writeText(createdToken)}
                  className="px-3 py-2 rounded-lg text-[12px] font-medium bg-[#8B5CF6] hover:bg-[#7c3aed] text-white transition-colors"
                >
                  Copy
                </button>
                <button
                  onClick={() => setCreatedToken(null)}
                  className="px-3 py-2 rounded-lg text-[12px] font-medium border border-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.85)] transition-colors"
                >
                  Hide
                </button>
              </div>
            </div>
          )}

          <div className="space-y-3">
            <div className="flex items-end gap-2">
              <div className="flex-1">
                <div className="text-[12px] text-[rgba(245,247,251,0.7)]">Create key</div>
                <input
                  value={newKeyLabel}
                  onChange={(e) => setNewKeyLabel(e.target.value)}
                  placeholder="Label (e.g. 'Ops bot')"
                  className="mt-1 w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[rgba(245,247,251,0.85)] placeholder:text-[rgba(245,247,251,0.3)] outline-none focus:border-[rgba(139,92,246,0.5)]"
                />
              </div>
              <button
                onClick={handleCreateKey}
                disabled={creatingKey || !newKeyLabel.trim()}
                className="px-4 py-2 rounded-lg text-[12px] font-semibold bg-[#8B5CF6] hover:bg-[#7c3aed] text-white transition-colors disabled:opacity-50 disabled:hover:bg-[#8B5CF6]"
              >
                {creatingKey ? 'Creating…' : 'Create'}
              </button>
            </div>

            {keysError && (
              <div className="text-[12px] text-[#f87171]">{keysError}</div>
            )}

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <div className="text-[12px] text-[rgba(245,247,251,0.7)]">Keys</div>
                <button
                  onClick={loadKeys}
                  disabled={keysLoading}
                  className="text-[12px] text-[#8B5CF6] hover:underline disabled:opacity-50"
                >
                  {keysLoading ? 'Loading…' : 'Refresh'}
                </button>
              </div>

              <div className="space-y-2">
                {apiKeys.length === 0 && !keysLoading && (
                  <div className="text-[12px] text-[rgba(245,247,251,0.4)]">No keys yet.</div>
                )}

                {apiKeys.map((k) => (
                  <div
                    key={k.id}
                    className="flex items-center justify-between gap-3 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]"
                  >
                    <div className="min-w-0">
                      <div className="text-[12px] text-[rgba(245,247,251,0.85)] font-medium truncate">
                        {k.label}
                      </div>
                      <div className="text-[11px] text-[rgba(245,247,251,0.4)]">
                        Created: {new Date(k.createdAtUtc).toLocaleString()} · Last used: {k.lastUsedAtUtc ? new Date(k.lastUsedAtUtc).toLocaleString() : '—'}
                      </div>
                      {k.revokedAtUtc && (
                        <div className="text-[11px] text-[#f87171]">
                          Revoked: {new Date(k.revokedAtUtc).toLocaleString()}
                        </div>
                      )}
                    </div>
                    <button
                      onClick={() => handleRevokeKey(k.id)}
                      disabled={!!k.revokedAtUtc}
                      className="px-3 py-1.5 rounded-lg text-[12px] font-medium border border-[rgba(248,113,113,0.3)] bg-[rgba(248,113,113,0.08)] text-[#f87171] hover:bg-[rgba(248,113,113,0.15)] transition-colors disabled:opacity-40 disabled:hover:bg-[rgba(248,113,113,0.08)]"
                    >
                      Revoke
                    </button>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Categories */}
      <div className={sectionCard}>
        <div>
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">{t('maintenance.categories')}</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
            {t('maintenance.categoriesDesc')}
          </p>
        </div>
        <div className={rowBase}>
          <span className="text-[12px] font-semibold text-[rgba(245,247,251,0.9)]">{t('maintenance.selectAll')}</span>
          <ToggleSwitch on={allCatsSelected} onClick={() => toggleSelectAll('categories')} />
        </div>
        {ALL_CATEGORIES.map(cat => (
          <div key={cat.id} className={rowBase}>
            <span className={`text-[12px] ${allCatsSelected ? 'text-[rgba(245,247,251,0.35)]' : 'text-[rgba(245,247,251,0.7)]'}`}>{t(cat.labelKey)}</span>
            <ToggleSwitch
              on={allCatsSelected || prefs.categories.includes(cat.id)}
              onClick={() => toggle('categories', cat.id)}
              disabled={allCatsSelected}
            />
          </div>
        ))}
      </div>

      {/* Severities */}
      <div className={sectionCard}>
        <div>
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">{t('maintenance.severityTitle')}</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
            {t('maintenance.severityDesc')}
          </p>
        </div>
        <div className={rowBase}>
          <span className="text-[12px] font-semibold text-[rgba(245,247,251,0.9)]">{t('maintenance.selectAll')}</span>
          <ToggleSwitch on={allSevsSelected} onClick={() => toggleSelectAll('severities')} />
        </div>
        {ALL_SEVERITIES.map(sev => (
          <div key={sev.id} className={rowBase}>
            <span className={`text-[12px] font-medium ${allSevsSelected ? 'opacity-40' : ''} ${sev.color}`}>{t(sev.labelKey)}</span>
            <ToggleSwitch
              on={allSevsSelected || prefs.severities.includes(sev.id)}
              onClick={() => toggle('severities', sev.id)}
              disabled={allSevsSelected}
            />
          </div>
        ))}
      </div>

      {/* Limit */}
      <div className={sectionCard}>
        <div>
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">{t('maintenance.maxEvents')}</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">{t('maintenance.maxEventsDesc')}</p>
        </div>
        <div className="flex gap-2 flex-wrap pt-1">
          {LIMIT_OPTIONS.map(n => (
            <button
              key={n}
              onClick={() => { setPrefs(p => ({ ...p, limit: n })); setSaved(false); }}
              className={`px-4 py-1.5 rounded-lg text-[12px] font-medium border transition-colors ${
                prefs.limit === n
                  ? 'bg-[rgba(139,92,246,0.2)] border-[rgba(139,92,246,0.4)] text-[#a78bfa]'
                  : 'bg-[rgba(255,255,255,0.04)] border-[rgba(255,255,255,0.08)] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
              }`}
            >
              {n}
            </button>
          ))}
        </div>
      </div>

      {/* Clear event logs */}
      <div className={sectionCard}>
        <div>
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">{t('maintenance.clearLogs')}</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
            {t('maintenance.clearLogsDesc')}
          </p>
        </div>
        {!clearConfirm ? (
          <button
            onClick={() => setClearConfirm(true)}
            className="px-4 py-2 rounded-lg text-[12px] font-medium border border-[rgba(248,113,113,0.3)] bg-[rgba(248,113,113,0.08)] text-[#f87171] hover:bg-[rgba(248,113,113,0.15)] transition-colors"
          >
            {t('maintenance.clearAll')}
          </button>
        ) : (
          <div className="flex items-center gap-3">
            <span className="text-[12px] text-[rgba(245,247,251,0.6)]">{t('maintenance.clearConfirm')}</span>
            <button
              onClick={handleClearEvents}
              disabled={clearing}
              className="px-4 py-1.5 rounded-lg text-[12px] font-medium bg-[#f87171] hover:bg-[#ef4444] text-white transition-colors disabled:opacity-50"
            >
              {clearing ? t('maintenance.clearing') : t('common.confirm')}
            </button>
            <button
              onClick={() => setClearConfirm(false)}
              className="px-4 py-1.5 rounded-lg text-[12px] font-medium border border-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] transition-colors"
            >
              {t('common.cancel')}
            </button>
          </div>
        )}
        {clearResult === 'success' && (
          <p className="text-[12px] text-[#4ade80]">{t('maintenance.logsRemoved')}</p>
        )}
        {clearResult === 'error' && (
          <p className="text-[12px] text-[#f87171]">{t('maintenance.clearError')}</p>
        )}
      </div>

      <button
        onClick={handleSave}
        className={`px-6 py-2.5 rounded-xl text-[13px] font-semibold transition-colors ${
          saved
            ? 'bg-[rgba(5,223,114,0.15)] border border-[rgba(5,223,114,0.3)] text-[#05df72]'
            : 'bg-[#8B5CF6] hover:bg-[#7c3aed] text-white'
        }`}
      >
        {saved ? t('common.saved') : t('maintenance.savePreferences')}
      </button>
    </div>
  );
}

// ── Preferences section (language picker) ──────────────────────────────────

const LANGUAGE_OPTIONS: { code: AppLanguage; label: string }[] = [
  { code: 'pt-BR', label: 'settings.general.ptBR' },
  { code: 'en-US', label: 'settings.general.enUS' },
  { code: 'fr-FR', label: 'settings.general.frFR' },
  { code: 'es-ES', label: 'settings.general.esES' },
];

function PreferencesSection() {
  const { t } = useTranslation();
  const { language, changeLanguage } = useLanguage();

  return (
    <div className="space-y-6 max-w-2xl">
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('maintenance.preferences')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.general.subtitle')}
        </p>
      </div>

      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
            <Globe className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.general.language')}</h3>
        </div>

        <div className="grid grid-cols-2 gap-2">
          {LANGUAGE_OPTIONS.map((opt) => (
            <button
              key={opt.code}
              onClick={() => changeLanguage(opt.code)}
              className={`py-2 px-4 rounded-lg text-[13px] font-medium transition-all ${
                language === opt.code
                  ? 'bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] text-white'
                  : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
              }`}
            >
              {t(opt.label)}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
