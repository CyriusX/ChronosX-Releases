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
    const result = await sendCommand('startTracking');
    if (result.success) refreshData();
  };

  const onStopTracking = async () => {
    const result = await sendCommand('stopTracking');
    if (result.success) refreshData();
  };

  const onPauseTracking = async () => {
    const result = await sendCommand(isPaused ? 'resumeTracking' : 'pauseTracking');
    if (result.success) refreshData();
  };

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden">
      <Sidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <DashboardHeader
            activeTab={activeTab}
            onTabChange={setActiveTab}
            showTeamTab={canManageTeam}
          />
        </div>

        <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0">
          {/* Left Content Area — scrollable */}
          <div className="flex-1 flex flex-col gap-4 overflow-y-auto min-w-0 pr-1">
            <TopCards
              summary={todaySummary}
              isPaused={isPaused}
              isTracking={isTracking}
              focusModePolicy={focusModePolicy}
              onStartTracking={onStartTracking}
              onPauseTracking={onPauseTracking}
              onStopTracking={onStopTracking}
            />

            <BottomCards summary={todaySummary} />
          </div>

          {/* Right Panel — fixed width, scrollable */}
          <div className="w-[280px] flex-shrink-0 overflow-y-auto">
            <RightPanel
              summary={todaySummary}
              weeklyHistory={weeklyHistory}
              showTeamCard={canManageTeam}
            />
          </div>
        </div>
      </main>
    </div>
  );
}
