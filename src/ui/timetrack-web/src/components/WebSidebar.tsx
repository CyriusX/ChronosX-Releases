import { useState, useEffect, useRef } from 'react';
import { BarChart3, Activity, CalendarDays, Cog, LogOut, Shield, Menu, X, Wrench, Bell, ChevronDown } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import { NavItem } from '@desktop/components/dashboard/shared/NavItem';
import { useAuthStore } from '../stores/authStore';
import { useHealthAlertStore } from '../stores/healthAlertStore';
import { SPRING } from '@desktop/lib/animation';
import logoImg from '@desktop/assets/logo-64.png';
import { listOrgDevices } from '../services/maintenanceApi';

function fmtAgo(isoString: string | null): string {
  if (!isoString) return '';
  const mins = Math.floor((Date.now() - new Date(isoString).getTime()) / 60000);
  if (mins < 1) return ' · agora';
  if (mins < 60) return ` · ${mins}min atrás`;
  return ` · ${Math.floor(mins / 60)}h atrás`;
}

export function WebSidebar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { logout, user } = useAuthStore();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [notifOpen, setNotifOpen] = useState(false);
  const alerts = useHealthAlertStore((state) => state.alerts);
  const setAlerts = useHealthAlertStore((state) => state.setAlerts);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Poll device list every 60s (Admin only) to keep the bell in sync on any page
  useEffect(() => {
    if (user?.role !== 'Admin' || !user?.orgId) return;
    const orgId = user.orgId;
    const fetchAlerts = async () => {
      try {
        const data = await listOrgDevices(orgId);
        const derived = data.devices
          .filter(d => d.status === 'offline' || d.healthStatus === 'unhealthy' || d.healthStatus === 'degraded')
          .map(d => ({
            deviceId: d.deviceId,
            hostname: d.hostname,
            userDisplayName: d.userDisplayName,
            issue: d.status === 'offline' ? 'offline' : (d.healthStatus ?? 'unhealthy'),
            lastSeenAt: d.lastSeenAt,
            healthStatus: d.status === 'offline' ? 'offline' : d.healthStatus,
          }));
        setAlerts(derived);
      } catch { /* non-critical */ }
    };
    fetchAlerts();
    pollRef.current = setInterval(fetchAlerts, 60_000);
    return () => { if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; } };
  }, [user?.role, user?.orgId, setAlerts]);

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const handleNav = (path: string) => {
    navigate(path);
    setMobileOpen(false);
  };

  const navItems = [
    { icon: <BarChart3 className="w-[16px] h-[16px]" />, label: 'Dashboard', path: '/' },
    { icon: <Activity className="w-[16px] h-[16px]" />, label: 'Atividade', path: '/activities' },
    { icon: <CalendarDays className="w-[16px] h-[16px]" />, label: 'Relatorios', path: '/reports' },
    { icon: <Cog className="w-[16px] h-[16px]" />, label: 'Configuracoes', path: '/settings' },
    ...(user?.role === 'Admin' ? [
      { icon: <Wrench className="w-[16px] h-[16px]" />, label: 'Manutencao', path: '/maintenance' },
    ] : []),
  ];

  const sidebarContent = (
    <>
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
            <span className="text-[15px] font-semibold text-[#f5f7fb] tracking-[-0.3px]">ChronosX</span>
            <div className="flex items-center gap-1">
              <Shield className="w-2.5 h-2.5 text-[#8B5CF6]" />
              <span className="text-[9px] text-[rgba(139,92,246,0.8)] font-medium uppercase tracking-wider">
                Admin Portal
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 flex flex-col gap-[2px] overflow-y-auto">
        {navItems.map((item) => (
          <NavItem
            key={item.path}
            icon={item.icon}
            label={item.label}
            active={location.pathname === item.path}
            onClick={() => handleNav(item.path)}
          />
        ))}
      </nav>

      {/* Notification Bell — Admin only */}
      {user?.role === 'Admin' && (
        <div className="px-3 pb-2 flex-shrink-0">
          <motion.button
            onClick={() => setNotifOpen(!notifOpen)}
            whileHover={{ x: 3 }}
            transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
            className={`w-full flex items-center gap-3 px-3 py-[10px] rounded-[12px] transition-colors ${
              alerts.length > 0
                ? 'text-[rgba(248,113,113,0.85)] hover:bg-[rgba(248,113,113,0.06)]'
                : 'text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)] hover:bg-[rgba(255,255,255,0.04)]'
            }`}
          >
            <div className="relative flex-shrink-0">
              <Bell className="w-[16px] h-[16px]" />
              {alerts.length > 0 && (
                <span className="absolute -top-1.5 -right-1.5 w-3.5 h-3.5 rounded-full bg-[#f87171] flex items-center justify-center animate-pulse">
                  <span className="text-[7px] font-bold text-white leading-none">
                    {alerts.length > 9 ? '9+' : alerts.length}
                  </span>
                </span>
              )}
            </div>
            <span className="text-[13px] font-medium flex-1 text-left">
              {alerts.length > 0 ? `${alerts.length} Alerta${alerts.length > 1 ? 's' : ''}` : 'Alertas'}
            </span>
            <motion.div animate={{ rotate: notifOpen ? 180 : 0 }} transition={{ duration: 0.2 }}>
              <ChevronDown className="w-3 h-3 opacity-40" />
            </motion.div>
          </motion.button>

          {/* Inline expanded alert list */}
          <AnimatePresence>
            {notifOpen && (
              <motion.div
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto' }}
                exit={{ opacity: 0, height: 0 }}
                className="overflow-hidden"
              >
                {alerts.length === 0 ? (
                  <p className="px-3 py-2 text-[10px] text-[rgba(245,247,251,0.3)]">Todos os dispositivos saudáveis</p>
                ) : (
                  <div className="mt-1 space-y-0.5 max-h-[200px] overflow-y-auto">
                    {alerts.map((alert) => {
                      const isCritical = alert.issue === 'offline' || alert.issue === 'unhealthy';
                      return (
                        <button
                          key={alert.deviceId}
                          onClick={() => { handleNav(`/maintenance?device=${encodeURIComponent(alert.deviceId)}`); setNotifOpen(false); }}
                          className="w-full flex items-start gap-2 px-3 py-2 rounded-lg hover:bg-[rgba(255,255,255,0.04)] transition-colors text-left"
                        >
                          <span className={`w-1.5 h-1.5 rounded-full flex-shrink-0 mt-1.5 ${isCritical ? 'bg-[#f87171]' : 'bg-[#fbbf24]'}`} />
                          <div className="flex-1 min-w-0">
                            <p className="text-[11px] font-medium text-[rgba(245,247,251,0.85)] truncate">
                              {alert.userDisplayName || alert.hostname}
                            </p>
                            <p className="text-[9px] text-[rgba(245,247,251,0.4)] truncate">{alert.hostname}</p>
                            <p className={`text-[9px] font-bold uppercase mt-0.5 ${isCritical ? 'text-[rgba(248,113,113,0.85)]' : 'text-[#fbbf24]'}`}>
                              {alert.issue === 'offline' ? 'OFFLINE' : alert.issue === 'unhealthy' ? 'UNHEALTHY' : 'DEGRADADO'}
                              <span className="font-normal normal-case">{fmtAgo(alert.lastSeenAt)}</span>
                            </p>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                )}
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      {/* User section + Logout */}
      <div className="border-t border-[rgba(255,255,255,0.04)] flex-shrink-0 px-3 py-3">
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
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
        >
          <LogOut className="w-[14px] h-[14px]" />
          Sair
        </button>
      </div>
    </>
  );

  return (
    <>
      {/* Desktop sidebar */}
      <aside className="hidden md:flex w-[200px] flex-shrink-0 glass-sidebar flex-col overflow-hidden">
        {sidebarContent}
      </aside>

      {/* Mobile header bar */}
      <div className="md:hidden fixed top-0 left-0 right-0 z-40 flex items-center gap-3 px-4 py-3 glass-sidebar border-b border-[rgba(255,255,255,0.04)]">
        <button onClick={() => setMobileOpen(true)} className="p-1">
          <Menu className="w-5 h-5 text-[rgba(245,247,251,0.7)]" />
        </button>
        <img src={logoImg} alt="ChronosX" className="w-6 h-6 rounded-md" />
        <span className="text-[14px] font-semibold text-[#f5f7fb]">ChronosX</span>
        <div className="ml-auto flex items-center gap-1">
          <Shield className="w-2.5 h-2.5 text-[#8B5CF6]" />
          <span className="text-[8px] text-[rgba(139,92,246,0.8)] font-medium uppercase tracking-wider">Admin</span>
        </div>
      </div>

      {/* Mobile drawer overlay */}
      <AnimatePresence>
        {mobileOpen && (
          <>
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="md:hidden fixed inset-0 z-50 bg-black/60 backdrop-blur-sm"
              onClick={() => setMobileOpen(false)}
            />
            <motion.aside
              initial={{ x: -280 }}
              animate={{ x: 0 }}
              exit={{ x: -280 }}
              transition={{ type: 'spring', stiffness: 300, damping: 30 }}
              className="md:hidden fixed left-0 top-0 bottom-0 z-50 w-[260px] glass-sidebar flex flex-col overflow-hidden"
            >
              <div className="absolute top-4 right-3">
                <button onClick={() => setMobileOpen(false)} className="p-1 rounded-lg hover:bg-[rgba(255,255,255,0.06)]">
                  <X className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
                </button>
              </div>
              {sidebarContent}
            </motion.aside>
          </>
        )}
      </AnimatePresence>
    </>
  );
}
