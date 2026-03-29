import { BarChart3, Activity, CalendarDays, Cog, LogOut, Shield } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { motion } from 'motion/react';
import { NavItem } from '@desktop/components/dashboard/shared/NavItem';
import { useAuthStore } from '../stores/authStore';
import { SPRING } from '@desktop/lib/animation';
import logoImg from '@desktop/assets/logo-64.png';

export function WebSidebar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { logout, user } = useAuthStore();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <aside className="w-[200px] flex-shrink-0 glass-sidebar flex flex-col overflow-hidden">
      {/* Logo + Admin Badge */}
      <div className="px-4 py-5 flex-shrink-0">
        <div className="flex items-center gap-2">
          <motion.img
            src={logoImg}
            alt="ChronosX"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ type: 'spring', stiffness: SPRING.gentle.stiffness, damping: SPRING.gentle.damping, delay: 0.2 }}
            className="w-8 h-8 rounded-lg shadow-[0px_6px_10px_0px_rgba(139,92,246,0.25)]"
          />
          <div className="flex flex-col">
            <motion.span
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.3, duration: 0.3 }}
              className="text-[15px] font-semibold text-[#f5f7fb] tracking-[-0.3px]"
            >
              ChronosX
            </motion.span>
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ delay: 0.5 }}
              className="flex items-center gap-1"
            >
              <Shield className="w-2.5 h-2.5 text-[#8B5CF6]" />
              <span className="text-[9px] text-[rgba(139,92,246,0.8)] font-medium uppercase tracking-wider">
                Admin Portal
              </span>
            </motion.div>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 flex flex-col gap-[2px] overflow-y-auto">
        <NavItem icon={<BarChart3 className="w-[16px] h-[16px]" />} label="Dashboard" active={location.pathname === '/'} onClick={() => navigate('/')} />
        <NavItem icon={<Activity className="w-[16px] h-[16px]" />} label="Atividade" active={location.pathname === '/activities'} onClick={() => navigate('/activities')} />
        <NavItem icon={<CalendarDays className="w-[16px] h-[16px]" />} label="Relatorios" active={location.pathname === '/reports'} onClick={() => navigate('/reports')} />
        <NavItem icon={<Cog className="w-[16px] h-[16px]" />} label="Configuracoes" active={location.pathname === '/settings'} onClick={() => navigate('/settings')} />
      </nav>

      {/* User section + Logout */}
      <div className="border-t border-[rgba(255,255,255,0.04)] flex-shrink-0 px-3 py-3">
        {/* User Info */}
        <div className="flex items-center gap-2.5 px-2 py-2">
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-[#8B5CF6] to-[#3B82F6] flex items-center justify-center flex-shrink-0">
            <span className="text-[12px] font-bold text-white">
              {user?.displayName?.charAt(0)?.toUpperCase() || '?'}
            </span>
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-[12px] font-medium text-[rgba(245,247,251,0.9)] truncate">
              {user?.displayName || 'Usuario'}
            </p>
            <p className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">
              {user?.role === 'Admin' ? 'Administrador' : 'Gestor'} · {user?.orgName}
            </p>
          </div>
        </div>

        {/* Logout */}
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
