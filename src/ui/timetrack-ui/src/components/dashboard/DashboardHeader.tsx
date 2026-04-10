import { Calendar, Search, SlidersHorizontal } from 'lucide-react';
import { motion } from 'motion/react';
import { SPRING } from '../../lib/animation';
import { NotificationsBell } from './NotificationsBell';

interface DashboardHeaderProps {
  activeTab: 'meu-dia' | 'equipe';
  onTabChange: (tab: 'meu-dia' | 'equipe') => void;
  showTeamTab?: boolean;
}

export function DashboardHeader({ activeTab, onTabChange, showTeamTab = false }: DashboardHeaderProps) {
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
            <motion.div
              layoutId="dashboard-tab-indicator"
              className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3),0px_4px_6px_0px_rgba(139,92,246,0.3)]"
              transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
            />
          )}
        </button>
        {showTeamTab && (
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
              <motion.div
                layoutId="dashboard-tab-indicator"
                className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3),0px_4px_6px_0px_rgba(139,92,246,0.3)]"
                transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
              />
            )}
          </button>
        )}
      </div>

      {/* Action Buttons */}
      <div className="flex items-center gap-2">
        <NotificationsBell />
        {[Calendar, Search].map((Icon, i) => (
          <motion.button
            key={i}
            whileHover={{ scale: 1.08 }}
            whileTap={{ scale: 0.95 }}
            className="w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] backdrop-blur-sm flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <Icon className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
          </motion.button>
        ))}
        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          className="flex items-center gap-1.5 px-3 py-2 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] backdrop-blur-sm hover:bg-[rgba(255,255,255,0.08)] transition-colors"
        >
          <SlidersHorizontal className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
          <span className="text-[11px] font-medium text-[rgba(245,247,251,0.6)]">Filtros</span>
        </motion.button>
      </div>
    </header>
  );
}
