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
import { Users, Building2, ArrowLeft, ShieldAlert } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { WebSidebar } from '../components/WebSidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useAuthStore } from '../stores/authStore';
import { getOrgPolicy, updateOrgPolicy } from '../services/policyApi';
import { clearAllEvents } from '../services/maintenanceApi';
import { MembersSection } from '@desktop/components/settings/MembersSection';
import { OrganizationSection } from '@desktop/components/settings/OrganizationSection';
import { SkeletonShimmer } from '@desktop/components/ui/SkeletonShimmer';
import { SPRING } from '@desktop/lib/animation';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest } from '@desktop/types/settings';

type WebSettingsSection = 'team' | 'organization' | 'maintenance';

const SECTIONS: { id: WebSettingsSection; label: string; icon: typeof Users }[] = [
  { id: 'team', label: 'Equipe', icon: Users },
  { id: 'organization', label: 'Organizacao', icon: Building2 },
  { id: 'maintenance', label: 'Manutencao', icon: ShieldAlert },
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
      case 'maintenance':
        return <MaintenanceSection />;
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

// ── Maintenance log preferences section ──────────────────────────────────────

const ALL_CATEGORIES = [
  { id: 'system', label: 'Sistema' },
  { id: 'error', label: 'Erros' },
  { id: 'user_action', label: 'Acoes do usuario' },
  { id: 'network', label: 'Rede' },
  { id: 'performance', label: 'Performance' },
];

const ALL_SEVERITIES = [
  { id: 'info', label: 'Info', color: 'text-[#4ade80]' },
  { id: 'warning', label: 'Aviso', color: 'text-[#fbbf24]' },
  { id: 'error', label: 'Erro', color: 'text-[#f87171]' },
  { id: 'critical', label: 'Critico', color: 'text-[#ef4444]' },
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
  const orgId = useAuthStore(s => s.user?.orgId);
  const [prefs, setPrefs] = useState<MaintenanceLogPrefs>(getMaintenanceLogPrefs);
  const [saved, setSaved] = useState(false);
  const [clearing, setClearing] = useState(false);
  const [clearConfirm, setClearConfirm] = useState(false);
  const [clearResult, setClearResult] = useState<'success' | 'error' | null>(null);

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
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Manutencao</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          Controle o nivel de detalhe dos logs exibidos na pagina de manutencao.
        </p>
      </div>

      {/* Categories */}
      <div className={sectionCard}>
        <div>
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">Categorias de eventos</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
            Selecione quais categorias mostrar.
          </p>
        </div>
        <div className={rowBase}>
          <span className="text-[12px] font-semibold text-[rgba(245,247,251,0.9)]">Selecionar tudo</span>
          <ToggleSwitch on={allCatsSelected} onClick={() => toggleSelectAll('categories')} />
        </div>
        {ALL_CATEGORIES.map(cat => (
          <div key={cat.id} className={rowBase}>
            <span className={`text-[12px] ${allCatsSelected ? 'text-[rgba(245,247,251,0.35)]' : 'text-[rgba(245,247,251,0.7)]'}`}>{cat.label}</span>
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
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">Nivel de severidade</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
            Selecione quais severidades exibir.
          </p>
        </div>
        <div className={rowBase}>
          <span className="text-[12px] font-semibold text-[rgba(245,247,251,0.9)]">Selecionar tudo</span>
          <ToggleSwitch on={allSevsSelected} onClick={() => toggleSelectAll('severities')} />
        </div>
        {ALL_SEVERITIES.map(sev => (
          <div key={sev.id} className={rowBase}>
            <span className={`text-[12px] font-medium ${allSevsSelected ? 'opacity-40' : ''} ${sev.color}`}>{sev.label}</span>
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
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">Maximo de eventos</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">Quantos eventos carregar por consulta.</p>
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
          <p className="text-[13px] font-semibold text-[rgba(245,247,251,0.9)]">Limpar logs de eventos</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
            Remove permanentemente todos os eventos registrados de todos os dispositivos da organizacao.
          </p>
        </div>
        {!clearConfirm ? (
          <button
            onClick={() => setClearConfirm(true)}
            className="px-4 py-2 rounded-lg text-[12px] font-medium border border-[rgba(248,113,113,0.3)] bg-[rgba(248,113,113,0.08)] text-[#f87171] hover:bg-[rgba(248,113,113,0.15)] transition-colors"
          >
            Limpar todos os logs
          </button>
        ) : (
          <div className="flex items-center gap-3">
            <span className="text-[12px] text-[rgba(245,247,251,0.6)]">Tem certeza? Esta acao nao pode ser desfeita.</span>
            <button
              onClick={handleClearEvents}
              disabled={clearing}
              className="px-4 py-1.5 rounded-lg text-[12px] font-medium bg-[#f87171] hover:bg-[#ef4444] text-white transition-colors disabled:opacity-50"
            >
              {clearing ? 'Limpando...' : 'Confirmar'}
            </button>
            <button
              onClick={() => setClearConfirm(false)}
              className="px-4 py-1.5 rounded-lg text-[12px] font-medium border border-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] transition-colors"
            >
              Cancelar
            </button>
          </div>
        )}
        {clearResult === 'success' && (
          <p className="text-[12px] text-[#4ade80]">Logs removidos com sucesso.</p>
        )}
        {clearResult === 'error' && (
          <p className="text-[12px] text-[#f87171]">Erro ao limpar logs. Tente novamente.</p>
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
        {saved ? 'Salvo!' : 'Salvar preferencias'}
      </button>
    </div>
  );
}
