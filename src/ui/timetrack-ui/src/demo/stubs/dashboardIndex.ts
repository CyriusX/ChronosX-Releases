// Demo-only dashboard barrel:
// - Re-exports the real components/styles
// - Replaces Sidebar with a no-op so LP demo shows the page content only

export { Sidebar } from './Sidebar';

export { DashboardHeader } from '../../components/dashboard/DashboardHeader';
export { TopCards } from '../../components/dashboard/TopCards';
export { ActivitySection } from '../../components/dashboard/ActivitySection';
export { BottomCards } from '../../components/dashboard/BottomCards';
export { RightPanel } from '../../components/dashboard/RightPanel';
export { MyTasksWidget } from '../../components/dashboard/MyTasksWidget';
export { NotificationsBell } from '../../components/dashboard/NotificationsBell';

// Timer Focus Card (CX-139)
export * from '../../components/dashboard/TimerFocusCard';

// Re-export shared components (NavItem, styles, icons, etc.)
export * from '../../components/dashboard/shared';

