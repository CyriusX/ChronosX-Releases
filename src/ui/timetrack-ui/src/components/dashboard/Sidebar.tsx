import { Timer as TimerIcon, BarChart3, FolderOpen, Activity, CalendarDays, Layers, Cog, LogOut } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { NavItem } from './shared';
import { useAuthStore } from '../../stores/authStore';

export function Sidebar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { logout, user } = useAuthStore();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <aside className="w-[200px] bg-gradient-to-b from-[rgba(11,13,20,0.5)] to-[rgba(17,19,28,0.5)] border-r border-[rgba(255,255,255,0.04)] flex flex-col">
      {/* Logo */}
      <div className="px-5 py-6">
        <div className="flex items-center gap-2">
          <div className="w-7 h-7 rounded-lg bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center shadow-[0px_10px_15px_0px_rgba(0,184,219,0.2),0px_4px_6px_0px_rgba(0,184,219,0.2)]">
            <TimerIcon className="w-4 h-4 text-white" />
          </div>
          <span className="text-base font-semibold text-[#f5f7fb] tracking-[-0.4px]">A Gente</span>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-5 flex flex-col gap-[2px]">
        <NavItem
          icon={<BarChart3 className="w-[18px] h-[18px]" />}
          label="Dashboard"
          active={location.pathname === '/'}
          onClick={() => navigate('/')}
        />
        <NavItem
          icon={<TimerIcon className="w-[18px] h-[18px]" />}
          label="Timer"
          onClick={() => navigate('/')}
        />
        <NavItem
          icon={<FolderOpen className="w-[18px] h-[18px]" />}
          label="Projetos"
          active={location.pathname === '/projects'}
          onClick={() => navigate('/projects')}
        />
        <NavItem
          icon={<Activity className="w-[18px] h-[18px]" />}
          label="Atividade"
          onClick={() => navigate('/')}
        />
        <NavItem
          icon={<CalendarDays className="w-[18px] h-[18px]" />}
          label="Relatórios"
          onClick={() => navigate('/reports')}
        />
        <NavItem
          icon={<Layers className="w-[18px] h-[18px]" />}
          label="Agente"
          onClick={() => navigate('/')}
        />
        <NavItem
          icon={<Cog className="w-[18px] h-[18px]" />}
          label="Configurações"
          active={location.pathname === '/settings'}
          onClick={() => navigate('/settings')}
        />
      </nav>

      {/* Bottom Stats Section */}
      <div className="px-5 pt-[25px] pb-4 border-t border-[rgba(255,255,255,0.04)] flex flex-col gap-3">
        {/* Dashboard Progress Card */}
        <div className="bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.06)] rounded-[14px] p-[13px]">
          <div className="flex items-center justify-between mb-3">
            <span className="text-[10px] font-medium text-[rgba(245,247,251,0.6)] tracking-[0.25px]">DASHBOARD</span>
            <div className="flex gap-1">
              <div className="w-1 h-1 rounded-full bg-[#00d3f3]" />
              <div className="w-1 h-1 rounded-full bg-[rgba(255,255,255,0.2)]" />
            </div>
          </div>
          <div className="flex items-center gap-[10px]">
            <div className="w-8 h-8 rounded-full bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
              <span className="text-[12px] font-semibold text-white">2</span>
            </div>
            <div>
              <p className="text-[14px] font-semibold text-[#f5f7fb]">8h 2,4m</p>
              <p className="text-[10px] text-[rgba(245,247,251,0.4)]">VS ontem</p>
            </div>
          </div>
        </div>

        {/* Session Time Card */}
        <div className="bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.06)] rounded-[14px] p-[13px]">
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center gap-2">
              <div className="w-1 h-1 rounded-full bg-[#05df72]" />
              <span className="text-[10px] font-medium text-[rgba(245,247,251,0.6)]">3,16m</span>
            </div>
            <div className="flex items-center gap-[2px]">
              <span className="text-[9px] text-[rgba(245,247,251,0.4)]">Máx.</span>
              <span className="text-[9px] font-medium text-[rgba(245,247,251,0.6)]">4</span>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 flex items-center justify-center">
              <svg className="w-12 h-12 -rotate-90" viewBox="0 0 48 48">
                <circle cx="24" cy="24" r="20" fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="4" />
                <circle cx="24" cy="24" r="20" fill="none" stroke="#05df72" strokeWidth="4" strokeDasharray="125.6" strokeDashoffset="31.4" strokeLinecap="round" />
              </svg>
            </div>
            <div>
              <p className="text-[18px] font-semibold text-[#f5f7fb]">2h 18m</p>
              <p className="text-[10px] text-[rgba(245,247,251,0.4)]">Hoje sessão 87</p>
            </div>
          </div>
        </div>

        {/* Legend Section */}
        <div className="px-5 pt-2 flex items-center gap-[6px]">
          <div className="flex items-center gap-[6px]">
            <div className="w-2 h-2 rounded-full bg-[#f3d05d]" />
            <span className="text-[10px] text-[rgba(245,247,251,0.4)]">Cat invito</span>
          </div>
          <div className="flex items-center gap-[6px]">
            <div className="w-2 h-2 rounded-full bg-[#4a9fff]" />
            <span className="text-[10px] text-[rgba(245,247,251,0.4)]">Sign in</span>
          </div>
          <div className="flex items-center gap-[6px]">
            <div className="w-2 h-2 rounded-full bg-[#ff6b7a]" />
            <span className="text-[10px] text-[rgba(245,247,251,0.4)]">Projetos</span>
          </div>
        </div>

        {/* User & Logout */}
        <div className="mt-3 pt-3 border-t border-[rgba(255,255,255,0.04)]">
          <div className="flex items-center gap-2 px-2 mb-2">
            <div className="w-7 h-7 rounded-full bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center text-white text-[11px] font-medium">
              {user?.displayName?.charAt(0)?.toUpperCase() || '?'}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-[11px] font-medium text-[#f5f7fb] truncate">{user?.displayName}</p>
              <p className="text-[9px] text-[rgba(245,247,251,0.4)]">{user?.role}</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
          >
            <LogOut className="w-[14px] h-[14px]" />
            Sair
          </button>
        </div>
      </div>
    </aside>
  );
}
