import { User, Settings2, Bell, Activity, Users, Building2, Info } from 'lucide-react';
import { motion } from 'motion/react';
import { SPRING } from '../../lib/animation';
import type { SettingsSection, SettingsSectionDef, SettingsGroup } from '../../types/settingsNav';

interface SettingsSidebarProps {
  activeSection: SettingsSection;
  onSectionChange: (section: SettingsSection) => void;
  canManageTeam: boolean;
  canViewOrgPolicies: boolean;
}

const SECTIONS: SettingsSectionDef[] = [
  { id: 'profile', label: 'Perfil', icon: User, group: 'personal' },
  { id: 'general', label: 'Geral', icon: Settings2, group: 'personal' },
  { id: 'notifications', label: 'Notificações', icon: Bell, group: 'personal' },
  { id: 'focus-timer', label: 'Foco & Timer', icon: Activity, group: 'personal' },
  { id: 'team', label: 'Equipe', icon: Users, group: 'management', requiresPermission: 'canManageTeam' },
  { id: 'organization', label: 'Organização', icon: Building2, group: 'management', requiresPermission: 'canViewOrgPolicies' },
  { id: 'about', label: 'Sobre', icon: Info, group: 'system' },
];

const GROUP_LABELS: Record<SettingsGroup, string> = {
  personal: 'Pessoal',
  management: 'Gestão',
  system: 'Sistema',
};

export function SettingsSidebar({ activeSection, onSectionChange, canManageTeam, canViewOrgPolicies }: SettingsSidebarProps) {
  const permissions: Record<string, boolean> = {
    canManageTeam,
    canViewOrgPolicies,
  };

  const visibleSections = SECTIONS.filter(
    (s) => !s.requiresPermission || permissions[s.requiresPermission]
  );

  // Group sections for rendering with dividers
  const groups: { group: SettingsGroup; items: SettingsSectionDef[] }[] = [];
  let currentGroup: SettingsGroup | null = null;

  for (const section of visibleSections) {
    if (section.group !== currentGroup) {
      currentGroup = section.group;
      groups.push({ group: section.group, items: [] });
    }
    groups[groups.length - 1].items.push(section);
  }

  return (
    <>
      {/* Desktop: Vertical sidebar */}
      <nav className="hidden md:flex flex-col w-[220px] flex-shrink-0 border-r border-[rgba(255,255,255,0.04)] bg-gradient-to-b from-[rgba(11,13,20,0.3)] to-[rgba(17,19,28,0.3)] overflow-y-auto py-4 px-3 gap-1">
        {groups.map((group, gi) => (
          <div key={group.group}>
            {gi > 0 && <div className="h-px bg-[rgba(255,255,255,0.04)] my-2" />}
            <p className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.3)] px-3 pt-3 pb-1 font-medium">
              {GROUP_LABELS[group.group]}
            </p>
            {group.items.map((section) => (
              <SidebarItem
                key={section.id}
                section={section}
                isActive={activeSection === section.id}
                onClick={() => onSectionChange(section.id)}
              />
            ))}
          </div>
        ))}
      </nav>

      {/* Mobile: Horizontal scroll pill bar */}
      <div className="flex md:hidden overflow-x-auto gap-1 px-4 py-3 border-b border-[rgba(255,255,255,0.04)] bg-[rgba(26,29,46,0.4)]">
        {visibleSections.map((section) => (
          <button
            key={section.id}
            onClick={() => onSectionChange(section.id)}
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
    </>
  );
}

// ============================================================================
// Sidebar Nav Item (desktop)
// ============================================================================

interface SidebarItemProps {
  section: SettingsSectionDef;
  isActive: boolean;
  onClick: () => void;
}

function SidebarItem({ section, isActive, onClick }: SidebarItemProps) {
  return (
    <motion.button
      onClick={onClick}
      whileHover={{ x: isActive ? 0 : 3 }}
      transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
      className={`w-full flex items-center gap-3 px-3 py-[10px] rounded-[10px] transition-colors relative ${
        isActive
          ? 'text-[#f5f7fb]'
          : 'text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)]'
      }`}
    >
      {isActive && (
        <motion.div
          layoutId="settings-nav-active"
          className="absolute inset-0 bg-[#1c1f2e] rounded-[10px]"
          transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
        />
      )}
      <section.icon className="w-4 h-4 relative z-10" />
      <span className="relative z-10 text-[13px] font-medium">{section.label}</span>
    </motion.button>
  );
}
