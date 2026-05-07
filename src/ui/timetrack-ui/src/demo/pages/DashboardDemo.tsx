import { useEffect, useState } from 'react';
import { useDashboardData } from '../../hooks/useDashboardData';
import { useIpc } from '../../hooks/useIpc';
import { useFocusModePolicy } from '../../hooks/useFocusModePolicy';
import { isDesktopRuntime } from '../../lib/runtime';
import { useNotifications } from '../../stores/uiStore';
import {
  Sidebar,
  DashboardHeader,
  TopCards,
  ActivitySection,
  BottomCards,
  RightPanel,
} from '../../components/dashboard';
import { getDemoMode } from '../demoMode';

export default function DashboardDemo() {
  const { sendQuery, sendCommand, isConnected } = useIpc();
  const desktopRuntime = isDesktopRuntime();
  const { todaySummary, weeklyHistory, isPaused, isTracking, refreshData } = useDashboardData();
  const { focusModePolicy } = useFocusModePolicy();
  const { notify } = useNotifications();
  const [workGoalSeconds, setWorkGoalSeconds] = useState<number>(28800);
  const demoMode = getDemoMode();
  const isMobileDemo = demoMode === 'mobile';

  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;
    sendQuery('getSettings').then((res) => {
      if (res.success && res.data) {
        const data = res.data as { workGoalSeconds?: number | null };
        if (data.workGoalSeconds) setWorkGoalSeconds(data.workGoalSeconds);
      }
    });
  }, [desktopRuntime, isConnected, sendQuery]);

  const onStartTracking = async () => {
    if (!desktopRuntime || !isConnected) {
      notify.error('Agent offline', 'AgentService is not connected. Please wait a moment and try again.');
      return;
    }
    const result = await sendCommand('startTracking');
    if (result.success) refreshData();
    else notify.error('Failed to start tracking', result.error);
  };

  const onStopTracking = async () => {
    if (!desktopRuntime || !isConnected) {
      notify.error('Agent offline', 'AgentService is not connected. Please wait a moment and try again.');
      return;
    }
    const result = await sendCommand('stopTracking');
    if (result.success) refreshData();
    else notify.error('Failed to stop tracking', result.error);
  };

  const onPauseTracking = async () => {
    if (!desktopRuntime || !isConnected) {
      notify.error('Agent offline', 'AgentService is not connected. Please wait a moment and try again.');
      return;
    }
    const result = await sendCommand(isPaused ? 'resumeTracking' : 'pauseTracking');
    if (result.success) refreshData();
    else notify.error('Failed to update tracking', result.error);
  };

  if (isMobileDemo) {
    return (
      <div className="min-h-[100dvh] bg-transparent pb-14 md:pb-0 overflow-x-hidden">
        <main className="px-5 pt-4 pb-4 space-y-4 overflow-x-hidden" data-demo-scroll-root="dashboard">
          <DashboardHeader />
          <TopCards
            summary={todaySummary}
            isPaused={isPaused}
            isTracking={isTracking}
            focusModePolicy={focusModePolicy}
            onStartTracking={onStartTracking}
            onPauseTracking={onPauseTracking}
            onStopTracking={onStopTracking}
            isTeamTab={false}
            weeklyHistory={weeklyHistory}
            workGoalSeconds={workGoalSeconds}
          />
          <ActivitySection />
          <BottomCards summary={todaySummary} />
          <RightPanel
            summary={todaySummary}
            weeklyHistory={weeklyHistory}
            showTeamCard={false}
            selectedMemberId={null}
            onMemberSelect={() => {}}
          />
          <div data-demo-scroll-end className="h-px w-full" />
        </main>
      </div>
    );
  }

  // Embed mode: keep the window-fitted layout (no root scrolling).
  return (
    <div className="flex h-screen bg-transparent overflow-hidden pb-14 md:pb-0">
      <Sidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <DashboardHeader />
        </div>

        <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0">
          <div className="flex-1 flex flex-col gap-4 min-w-0 overflow-y-auto">
            <TopCards
              summary={todaySummary}
              isPaused={isPaused}
              isTracking={isTracking}
              focusModePolicy={focusModePolicy}
              onStartTracking={onStartTracking}
              onPauseTracking={onPauseTracking}
              onStopTracking={onStopTracking}
              isTeamTab={false}
              weeklyHistory={weeklyHistory}
              workGoalSeconds={workGoalSeconds}
            />
            <ActivitySection />
            <BottomCards summary={todaySummary} />

            <div className="lg:hidden">
              <RightPanel
                summary={todaySummary}
                weeklyHistory={weeklyHistory}
                showTeamCard={false}
                selectedMemberId={null}
                onMemberSelect={() => {}}
              />
            </div>
          </div>

          <div className="hidden lg:flex w-[280px] flex-shrink-0 min-h-0 overflow-hidden">
            <RightPanel
              summary={todaySummary}
              weeklyHistory={weeklyHistory}
              showTeamCard={false}
              selectedMemberId={null}
              onMemberSelect={() => {}}
            />
          </div>
        </div>
      </main>
    </div>
  );
}

