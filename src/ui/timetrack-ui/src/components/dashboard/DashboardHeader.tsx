import { Calendar, Search, MoreVertical } from 'lucide-react';

interface DashboardHeaderProps {
  activeTab: 'meu-dia' | 'equipe';
  onTabChange: (tab: 'meu-dia' | 'equipe') => void;
}

export function DashboardHeader({ activeTab, onTabChange }: DashboardHeaderProps) {
  return (
    <header className="flex items-center justify-between">
      {/* Tabs */}
      <div className="flex items-center gap-0">
        <button
          onClick={() => onTabChange('meu-dia')}
          className={`relative px-0 py-2 text-[14px] font-medium transition-colors ${
            activeTab === 'meu-dia'
              ? 'text-[#f5f7fb]'
              : 'text-[rgba(245,247,251,0.45)]'
          }`}
        >
          Meu dia
          {activeTab === 'meu-dia' && (
            <div className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] rounded-full shadow-[0px_10px_15px_0px_rgba(0,184,219,0.3),0px_4px_6px_0px_rgba(0,184,219,0.3)]" />
          )}
        </button>
        <button
          onClick={() => onTabChange('equipe')}
          className={`relative px-4 py-2 text-[14px] font-medium transition-colors ${
            activeTab === 'equipe'
              ? 'text-[#f5f7fb]'
              : 'text-[rgba(245,247,251,0.45)]'
          }`}
        >
          Equipe
          {activeTab === 'equipe' && (
            <div className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] rounded-full shadow-[0px_10px_15px_0px_rgba(0,184,219,0.3),0px_4px_6px_0px_rgba(0,184,219,0.3)]" />
          )}
        </button>
      </div>

      {/* Action Buttons */}
      <div className="flex items-center gap-3">
        <button className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors">
          <Calendar className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
        </button>
        <button className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors">
          <Search className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
        </button>
        <button className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors">
          <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
        </button>
      </div>
    </header>
  );
}
