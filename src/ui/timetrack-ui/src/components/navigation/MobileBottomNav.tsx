import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import {
  BarChart3,
  Timer as TimerIcon,
  FolderOpen,
  Activity,
  Users,
  Cog,
  Play,
  Square,
  Loader2,
} from 'lucide-react';
import { useTrackingStore } from '../../stores/trackingStore';
import { useIpc } from '../../hooks/useIpc';
import { usePermissions } from '../../hooks/usePermissions';
import { SPRING } from '../../lib/animation';
import { useNotifications } from '../../stores/uiStore';

const BASE_NAV_ITEMS = [
  { icon: BarChart3, labelKey: 'mobileNav.dashboard', path: '/' },
  { icon: TimerIcon, labelKey: 'mobileNav.timer', path: '/timer' },
  { icon: FolderOpen, labelKey: 'mobileNav.projects', path: '/projects' },
  { icon: Activity, labelKey: 'mobileNav.activities', path: '/activities' },
  { icon: Users, labelKey: 'mobileNav.team', path: '/teams', managerOnly: true },
  { icon: Cog, labelKey: 'mobileNav.settings', path: '/settings' },
];

export function MobileBottomNav() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const isTracking = useTrackingStore(s => s.isTracking);
  const isPaused = useTrackingStore(s => s.isPaused);
  const setTracking = useTrackingStore(s => s.setTracking);
  const setPaused = useTrackingStore(s => s.setPaused);
  const { sendCommand } = useIpc();
  const { canManageTeam } = usePermissions();
  const { notify } = useNotifications();
  const [isBusy, setIsBusy] = useState(false);

  const NAV_ITEMS = BASE_NAV_ITEMS.filter(item => !item.managerOnly || canManageTeam);

  const isActive = isTracking && !isPaused;

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

  // Hide on login/register pages
  if (location.pathname === '/login' || location.pathname === '/register') {
    return null;
  }

  return (
    <nav className="md:hidden fixed bottom-0 inset-x-0 z-50 glass-sidebar border-t border-[rgba(255,255,255,0.06)]">
      <div className="flex items-end h-14">
        {/* Nav items — 6 equal columns */}
        {NAV_ITEMS.map(({ icon: Icon, labelKey, path }) => {
          const isCurrentPath = location.pathname === path;
          return (
            <button
              key={path}
              onClick={() => navigate(path)}
              className="flex-1 flex flex-col items-center justify-center h-full gap-0.5 relative"
            >
              {isCurrentPath && (
                <motion.div
                  layoutId="mobile-nav-active"
                  className="absolute inset-x-1 inset-y-1.5 rounded-xl bg-[rgba(139,92,246,0.10)] border border-[rgba(139,92,246,0.15)]"
                  transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
                />
              )}
              <span className="relative z-10">
                <Icon
                  className="w-[18px] h-[18px]"
                  style={{ color: isCurrentPath ? '#f5f7fb' : 'rgba(245,247,251,0.35)' }}
                />
              </span>
              <span
                className="relative z-10 text-[9px] font-medium"
                style={{ color: isCurrentPath ? 'rgba(245,247,251,0.9)' : 'rgba(245,247,251,0.3)' }}
              >
                {t(labelKey)}
              </span>
            </button>
          );
        })}

        {/* Tracking toggle button — compact, at the far right within layout */}
        <button
          onClick={handleToggleTracking}
          disabled={isBusy}
          className="flex-shrink-0 flex flex-col items-center justify-center h-full px-3 gap-0.5"
          title={isActive ? t('sidebar.stopMonitoring') : t('sidebar.resumeMonitoring')}
        >
          <div
            className={`relative w-7 h-7 rounded-full flex items-center justify-center transition-all ${
              isBusy
                ? 'bg-[rgba(255,255,255,0.04)]'
                : isActive
                  ? 'bg-[rgba(248,113,113,0.12)] border border-[rgba(248,113,113,0.25)]'
                  : 'bg-[rgba(5,223,114,0.10)] border border-[rgba(5,223,114,0.2)]'
            }`}
          >
            {/* Pulse ring when active */}
            {isActive && !isBusy && (
              <motion.div
                className="absolute inset-0 rounded-full border border-[rgba(248,113,113,0.4)]"
                animate={{ scale: [1, 1.4, 1], opacity: [0.6, 0, 0.6] }}
                transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
              />
            )}
            {isBusy ? (
              <Loader2 className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] animate-spin" />
            ) : isActive ? (
              <Square className="w-3 h-3 text-[#f87171]" />
            ) : (
              <Play className="w-3 h-3 text-[#05df72]" />
            )}
          </div>
          <span className="text-[9px] font-medium" style={{ color: isActive ? '#f87171' : '#05df72' }}>
            {isBusy ? '...' : isActive ? t('sidebar.stopMonitoring') : t('sidebar.resumeMonitoring')}
          </span>
        </button>
      </div>

      {/* Safe area spacer for iOS home indicator */}
      <div className="h-[env(safe-area-inset-bottom,0px)]" />
    </nav>
  );
}
