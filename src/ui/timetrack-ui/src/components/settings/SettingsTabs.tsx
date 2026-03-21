import { Settings2, Info, Users, AppWindow } from 'lucide-react';
import { motion } from 'motion/react';
import { SPRING } from '../../lib/animation';

export type SettingsTab = 'preferences' | 'about' | 'members' | 'apps';

interface SettingsTabsProps {
  activeTab: SettingsTab;
  onTabChange: (tab: SettingsTab) => void;
  showMembersTab?: boolean;
  showAppsTab?: boolean;
}

interface TabDef {
  id: SettingsTab;
  icon: typeof Settings2;
  label: string;
  show: boolean;
}

export function SettingsTabs({ activeTab, onTabChange, showMembersTab = false, showAppsTab = false }: SettingsTabsProps) {
  const tabs: TabDef[] = [
    { id: 'preferences', icon: Settings2, label: 'Preferências', show: true },
    { id: 'members', icon: Users, label: 'Membros', show: showMembersTab },
    { id: 'apps', icon: AppWindow, label: 'Aplicativos', show: showAppsTab },
    { id: 'about', icon: Info, label: 'Sobre', show: true },
  ];

  return (
    <div className="flex items-center gap-1 bg-[rgba(26,29,46,0.4)] rounded-xl p-1 w-fit">
      {tabs.filter(t => t.show).map((tab) => (
        <button
          key={tab.id}
          onClick={() => onTabChange(tab.id)}
          className={`relative flex items-center gap-2 px-4 py-2 rounded-lg text-[13px] font-medium transition-colors ${
            activeTab === tab.id
              ? 'text-[#f5f7fb]'
              : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
          }`}
        >
          {activeTab === tab.id && (
            <motion.div
              layoutId="settings-tab"
              className="absolute inset-0 bg-[rgba(74,217,255,0.15)] rounded-lg"
              transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
            />
          )}
          <tab.icon className="w-4 h-4 relative z-10" />
          <span className="relative z-10">{tab.label}</span>
        </button>
      ))}
    </div>
  );
}
