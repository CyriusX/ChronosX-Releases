import { useState } from 'react';
import { Timer as TimerIcon, BarChart3, FolderOpen, Activity, CalendarDays, Cog, LogOut, Play, Square, Loader2, Users } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { NavItem } from './shared';
import { SidebarMiniDash } from './shared/SidebarMiniDash';
import { useAuthStore } from '../../stores/authStore';
import { useTrackingStore } from '../../stores/trackingStore';
import { useIpc } from '../../hooks/useIpc';
import { useAgentStatus } from '../../hooks/useAgentStatus';
import { usePermissions } from '../../hooks/usePermissions';
import { SPRING } from '../../lib/animation';
import logoImg from '../../assets/logo-64.png';
import { useNotifications } from '../../stores/uiStore';

export function Sidebar() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { logout, user } = useAuthStore();
  const { canManageTeam } = usePermissions();
  const isTracking = useTrackingStore(s => s.isTracking);
  const isPaused = useTrackingStore(s => s.isPaused);
  const todaySummary = useTrackingStore(s => s.todaySummary);
  const { sendCommand } = useIpc();
  const { status: agentStatus } = useAgentStatus();
  const { notify } = useNotifications();
  const [isBusy, setIsBusy] = useState(false);

  const isActive = isTracking && !isPaused;

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const setTracking = useTrackingStore(s => s.setTracking);
  const setPaused = useTrackingStore(s => s.setPaused);

  const handleToggleTracking = async () => {
    if (isBusy) return;
    setIsBusy(true);
    try {
      if (isActive) {
        const res = await sendCommand('pauseTracking', { reason: 'Tracking Stopped' });
        if (res.success) { setTracking(false); setPaused(true); }
        else notify.error('Failed to pause tracking', res.error);
      } else {
        const res = await sendCommand('startTracking');
        if (res.success) { setTracking(true); setPaused(false); }
        else notify.error('Failed to start tracking', res.error);
      }
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <aside className="hidden md:flex w-[180px] flex-shrink-0 glass-sidebar flex-col overflow-hidden">
      {/* pt-8 clears macOS traffic light buttons; pt-4 is enough on Windows */}
      <div className={`px-4 pb-5 flex-shrink-0 ${navigator.platform?.startsWith('Mac') ? 'pt-8' : 'pt-4'}`}>
        <div className="flex items-center gap-2">
          <motion.img
            src={logoImg}
            alt="ChronosX"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ type: 'spring', stiffness: SPRING.gentle.stiffness, damping: SPRING.gentle.damping, delay: 0.2 }}
            className="w-8 h-8 rounded-lg shadow-[0px_6px_10px_0px_rgba(139,92,246,0.25)]"
          />
          <motion.span
            initial={{ opacity: 0, x: -8 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.3, duration: 0.3 }}
            className="text-[15px] font-semibold text-[#f5f7fb] tracking-[-0.3px]"
          >
            ChronosX
          </motion.span>

          {/* Tracking Status LED */}
          <motion.div
            className="ml-auto flex-shrink-0 relative"
            initial={{ opacity: 0, scale: 0.5 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ delay: 0.4, duration: 0.3 }}
            title={isActive ? t('sidebar.trackingActive') : t('sidebar.trackingInactive')}
          >
            {isActive && (
              <motion.div
                className="absolute inset-0 rounded-full bg-[#05df72]"
                animate={{ opacity: [0.4, 0.1, 0.4], scale: [1, 1.8, 1] }}
                transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
              />
            )}
            <div
              className={`w-2.5 h-2.5 rounded-full relative ${
                isActive ? 'bg-[#05df72] shadow-[0_0_6px_rgba(5,223,114,0.5)]' : 'bg-[#f87171] shadow-[0_0_6px_rgba(248,113,113,0.4)]'
              }`}
            />
          </motion.div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 flex flex-col gap-[2px] overflow-y-auto">
        <NavItem icon={<BarChart3 className="w-[16px] h-[16px]" />} label={t('sidebar.dashboard')} active={location.pathname === '/'} onClick={() => navigate('/')} />
        <NavItem icon={<TimerIcon className="w-[16px] h-[16px]" />} label={t('sidebar.timer')} active={location.pathname === '/timer'} onClick={() => navigate('/timer')} />
        <NavItem icon={<FolderOpen className="w-[16px] h-[16px]" />} label={t('sidebar.projects')} active={location.pathname === '/projects'} onClick={() => navigate('/projects')} />
        {canManageTeam && (
          <NavItem icon={<Users className="w-[16px] h-[16px]" />} label={t('sidebar.team')} active={location.pathname === '/teams'} onClick={() => navigate('/teams')} />
        )}
        <NavItem icon={<Activity className="w-[16px] h-[16px]" />} label={t('sidebar.activities')} active={location.pathname === '/activities'} onClick={() => navigate('/activities')} />
        <NavItem icon={<CalendarDays className="w-[16px] h-[16px]" />} label={t('sidebar.reports')} active={location.pathname === '/reports'} onClick={() => navigate('/reports')} />
        <NavItem icon={<Cog className="w-[16px] h-[16px]" />} label={t('sidebar.settings')} active={location.pathname === '/settings'} onClick={() => navigate('/settings')} />
      </nav>

      {/* Tracking Toggle Button — above separator & user section */}
      <div className="px-3 pb-2 flex-shrink-0">
        <button
          onClick={handleToggleTracking}
          disabled={isBusy}
          className={`w-full flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-[11px] font-medium transition-all ${
            isBusy
              ? 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[rgba(245,247,251,0.4)] cursor-not-allowed'
              : isActive
                ? 'bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)] text-[#f87171] hover:bg-[rgba(248,113,113,0.14)]'
                : 'bg-[rgba(5,223,114,0.08)] border border-[rgba(5,223,114,0.2)] text-[#05df72] hover:bg-[rgba(5,223,114,0.14)]'
          }`}
        >
          {isBusy ? (
            <>
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
              {isActive ? t('sidebar.stopping') : t('sidebar.starting')}
            </>
          ) : isActive ? (
            <>
              <Square className="w-3.5 h-3.5" />
              {t('sidebar.stopMonitoring')}
            </>
          ) : (
            <>
              <Play className="w-3.5 h-3.5" />
              {t('sidebar.resumeMonitoring')}
            </>
          )}
        </button>
      </div>

      {/* Mini Dashboard Widget */}
      <div className="border-t border-[rgba(255,255,255,0.04)] flex-shrink-0 pt-2">
        <SidebarMiniDash
          userName={user?.displayName || t('common.user')}
          userInitial={user?.displayName?.charAt(0)?.toUpperCase() || '?'}
          userRole={user?.role || ''}
          totalDuration={todaySummary?.totalDuration ?? 0}
          productiveTime={todaySummary?.productiveTime ?? 0}
          productivityScore={(() => {
            const total = todaySummary?.totalDuration ?? 0;
            const prod = (todaySummary?.categories ?? [])
              .filter(c => c.productivity === 'productive')
              .reduce((sum, c) => sum + c.duration, 0);
            return total > 0 ? Math.round((prod / total) * 100) : 0;
          })()}
          topAppName={
            (todaySummary?.topApplications ?? []).length > 0
              ? [...(todaySummary?.topApplications ?? [])].sort((a, b) => b.duration - a.duration)[0]?.name ?? null
              : null
          }
          topAppDuration={
            (todaySummary?.topApplications ?? []).length > 0
              ? [...(todaySummary?.topApplications ?? [])].sort((a, b) => b.duration - a.duration)[0]?.duration ?? 0
              : 0
          }
          dailyGoalSeconds={28800}
        />

        {/* Logout */}
        <div className="px-3 pb-1">
          <button
            onClick={handleLogout}
            className="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
          >
            <LogOut className="w-[14px] h-[14px]" />
            {t('sidebar.logout')}
          </button>
        </div>

        {/* Version */}
        <div className="px-4 pb-3">
          <span className="text-[9px] text-[rgba(245,247,251,0.2)] select-none">
            v{agentStatus.desktopHostVersion ?? agentStatus.version}
          </span>
        </div>
      </div>
    </aside>
  );
}
