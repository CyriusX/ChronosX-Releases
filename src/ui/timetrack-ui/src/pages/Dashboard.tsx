import { useState } from 'react';
import { useDashboardData } from '../hooks/useDashboardData';
import { useIpc } from '../hooks/useIpc';
import { usePermissions } from '../hooks/usePermissions';
import { useFocusModePolicy } from '../hooks/useFocusModePolicy';
import {
  Sidebar,
  DashboardHeader,
  TopCards,
  ActivitySection,
  BottomCards,
  RightPanel,
} from '../components/dashboard';

export default function Dashboard() {
  const [activeTab, setActiveTab] = useState<'meu-dia' | 'equipe'>('meu-dia');
  const { sendCommand } = useIpc();
  const { todaySummary, weeklyHistory, isPaused, isTracking, refreshData } = useDashboardData();
  const { canManageTeam } = usePermissions();
  const { focusModePolicy } = useFocusModePolicy();

  const onStartTracking = async () => {
    console.log('[Dashboard] Starting tracking...');
    const result = await sendCommand('startTracking');
    console.log('[Dashboard] Start tracking result:', result);
    if (result.success) {
      refreshData(); // Refresh tracking state to update UI
    }
  };

  const onStopTracking = async () => {
    console.log('[Dashboard] Stopping tracking...');
    const result = await sendCommand('stopTracking');
    console.log('[Dashboard] Stop tracking result:', result);
    if (result.success) {
      refreshData(); // Refresh tracking state to update UI
    }
  };

  const onPauseTracking = async () => {
    console.log('[Dashboard] Toggling pause, currently paused:', isPaused);
    const result = await sendCommand(isPaused ? 'resumeTracking' : 'pauseTracking');
    console.log('[Dashboard] Pause toggle result:', result);
    if (result.success) {
      refreshData(); // Refresh tracking state to update UI
    }
  };

  return (
    <div className="flex h-screen bg-[#0b0d14]">
      <Sidebar />

      {/* Main content */}
      <main className="flex-1 overflow-auto bg-[#0b0d14] p-6">
        <div className="h-full flex gap-6">
          {/* Left Content Area */}
          <div className="flex-1 flex flex-col gap-6">
            <DashboardHeader
              activeTab={activeTab}
              onTabChange={setActiveTab}
              showTeamTab={canManageTeam}
            />

            <TopCards
              summary={todaySummary}
              isPaused={isPaused}
              isTracking={isTracking}
              focusModePolicy={focusModePolicy}
              onStartTracking={onStartTracking}
              onPauseTracking={onPauseTracking}
              onStopTracking={onStopTracking}
            />

            <ActivitySection />

            <BottomCards summary={todaySummary} />
          </div>

          <RightPanel
            summary={todaySummary}
            weeklyHistory={weeklyHistory}
            showTeamCard={canManageTeam}
          />
        </div>
      </main>
    </div>
  );
}
