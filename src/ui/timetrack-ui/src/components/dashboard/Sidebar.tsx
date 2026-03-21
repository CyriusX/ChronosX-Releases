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
    <aside className="w-[180px] flex-shrink-0 bg-gradient-to-b from-[rgba(11,13,20,0.5)] to-[rgba(17,19,28,0.5)] border-r border-[rgba(255,255,255,0.04)] flex flex-col overflow-hidden">
      {/* Logo */}
      <div className="px-4 py-5 flex-shrink-0">
        <div className="flex items-center gap-2">
          <div className="w-7 h-7 rounded-lg bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center shadow-[0px_6px_10px_0px_rgba(0,184,219,0.2)]">
            <TimerIcon className="w-4 h-4 text-white" />
          </div>
          <span className="text-[15px] font-semibold text-[#f5f7fb] tracking-[-0.3px]">XChronus</span>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 flex flex-col gap-[2px] overflow-y-auto">
        <NavItem icon={<BarChart3 className="w-[16px] h-[16px]" />} label="Dashboard" active={location.pathname === '/'} onClick={() => navigate('/')} />
        <NavItem icon={<TimerIcon className="w-[16px] h-[16px]" />} label="Timer" active={location.pathname === '/timer'} onClick={() => navigate('/timer')} />
        <NavItem icon={<FolderOpen className="w-[16px] h-[16px]" />} label="Projetos" active={location.pathname === '/projects'} onClick={() => navigate('/projects')} />
        <NavItem icon={<Activity className="w-[16px] h-[16px]" />} label="Atividade" onClick={() => navigate('/')} />
        <NavItem icon={<CalendarDays className="w-[16px] h-[16px]" />} label="Relatórios" active={location.pathname === '/reports'} onClick={() => navigate('/reports')} />
        <NavItem icon={<Cog className="w-[16px] h-[16px]" />} label="Configurações" active={location.pathname === '/settings'} onClick={() => navigate('/settings')} />
      </nav>

      {/* User & Logout */}
      <div className="px-3 py-3 border-t border-[rgba(255,255,255,0.04)] flex-shrink-0">
        <div className="flex items-center gap-2 px-2 mb-2">
          <div className="w-7 h-7 rounded-full bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center text-white text-[11px] font-medium flex-shrink-0">
            {user?.displayName?.charAt(0)?.toUpperCase() || '?'}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-[11px] font-medium text-[#f5f7fb] truncate">{user?.displayName || 'Usuário'}</p>
            <p className="text-[9px] text-[rgba(245,247,251,0.4)]">{user?.role || ''}</p>
          </div>
        </div>
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
        >
          <LogOut className="w-[14px] h-[14px]" />
          Sair
        </button>
      </div>
    </aside>
  );
}
