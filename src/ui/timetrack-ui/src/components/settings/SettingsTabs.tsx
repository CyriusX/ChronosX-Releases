import { Settings2, Info } from 'lucide-react';

interface SettingsTabsProps {
  activeTab: 'preferences' | 'about';
  onTabChange: (tab: 'preferences' | 'about') => void;
}

/**
 * SettingsTabs - Navegação entre abas de configurações
 *
 * Segue padrão visual do DashboardHeader (underline gradient)
 */
export function SettingsTabs({ activeTab, onTabChange }: SettingsTabsProps) {
  return (
    <div className="flex items-center gap-1 bg-[rgba(26,29,46,0.4)] rounded-xl p-1 w-fit">
      <button
        onClick={() => onTabChange('preferences')}
        className={`flex items-center gap-2 px-4 py-2 rounded-lg text-[13px] font-medium transition-all ${
          activeTab === 'preferences'
            ? 'bg-[rgba(74,217,255,0.15)] text-[#f5f7fb]'
            : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.03)]'
        }`}
      >
        <Settings2 className="w-4 h-4" />
        Preferências
      </button>
      <button
        onClick={() => onTabChange('about')}
        className={`flex items-center gap-2 px-4 py-2 rounded-lg text-[13px] font-medium transition-all ${
          activeTab === 'about'
            ? 'bg-[rgba(74,217,255,0.15)] text-[#f5f7fb]'
            : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.03)]'
        }`}
      >
        <Info className="w-4 h-4" />
        Sobre
      </button>
    </div>
  );
}
