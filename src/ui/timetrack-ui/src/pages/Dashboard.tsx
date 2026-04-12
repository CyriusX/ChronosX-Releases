import { useState, useEffect } from 'react';
import { AnimatePresence } from 'motion/react';
import { motion } from 'motion/react';
import { useDashboardData } from '../hooks/useDashboardData';
import { useIpc } from '../hooks/useIpc';
import { usePermissions } from '../hooks/usePermissions';
import { useFocusModePolicy } from '../hooks/useFocusModePolicy';
import { useTeamStatus } from '../hooks/useTeamStatus';
import {
  Sidebar,
  DashboardHeader,
  TopCards,
  ActivitySection,
  BottomCards,
  RightPanel,
} from '../components/dashboard';
import { MemberCard } from '../components/teams/MemberCard';
import { MemberDetailDrawer } from '../components/teams/MemberDetailDrawer';
import { staggerContainer } from '../lib/animation';
import type { TeamMemberStatus } from '../types/member';

export default function Dashboard() {
  const [activeTab, setActiveTab] = useState<'meu-dia' | 'equipe'>('meu-dia');
  const [drawerMemberId, setDrawerMemberId] = useState<string | null>(null);
  const { sendQuery } = useIpc();
  const { todaySummary, weeklyHistory, isPaused, isTracking, refreshData } = useDashboardData();
  const { sendCommand } = useIpc();
  const { canManageTeam } = usePermissions();
  const { focusModePolicy } = useFocusModePolicy();
  const { members, isLoading, loadTeamStatus } = useTeamStatus();
  const [workGoalSeconds, setWorkGoalSeconds] = useState<number>(28800);

  // Fetch work goal setting once
  useEffect(() => {
    sendQuery('getSettings').then((res) => {
      if (res.success && res.data) {
        const data = res.data as { workGoalSeconds?: number | null };
        if (data.workGoalSeconds) setWorkGoalSeconds(data.workGoalSeconds);
      }
    });
  }, [sendQuery]);

  // Load team members when switching to equipe tab
  useEffect(() => {
    if (activeTab === 'equipe') {
      loadTeamStatus(true);
    }
  }, [activeTab, loadTeamStatus]);

  // Clear drawer when switching away from equipe tab
  useEffect(() => {
    if (activeTab === 'meu-dia') {
      setDrawerMemberId(null);
    }
  }, [activeTab]);

  const isTeamTab = activeTab === 'equipe';
  const drawerMember = members.find((m: TeamMemberStatus) => m.userId === drawerMemberId) ?? null;

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
    <div className="flex h-screen bg-transparent overflow-hidden pb-14 md:pb-0">
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
          {/* Main Content Area — scrollable */}
          <div className="flex-1 flex flex-col gap-4 min-w-0 overflow-y-auto">

            {/* "Meu dia" tab — own data + activity timeline */}
            {!isTeamTab && (
              <>
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
              </>
            )}

            {/* "Equipe" tab — member card grid */}
            {isTeamTab && (
              <>
                {isLoading && members.length === 0 ? (
                  <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
                    {Array.from({ length: 6 }).map((_, i) => (
                      <div
                        key={i}
                        className="rounded-[22px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] h-[210px] animate-pulse"
                      />
                    ))}
                  </div>
                ) : members.length === 0 ? (
                  <div className="flex items-center justify-center py-24">
                    <p className="text-[13px] text-[rgba(245,247,251,0.35)]">Nenhum membro na equipe</p>
                  </div>
                ) : (
                  <motion.div
                    variants={staggerContainer(0.06)}
                    initial="hidden"
                    animate="visible"
                    className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4"
                  >
                    {members.map((member: TeamMemberStatus, i: number) => (
                      <MemberCard
                        key={member.userId}
                        member={member}
                        index={i}
                        onSelect={setDrawerMemberId}
                      />
                    ))}
                  </motion.div>
                )}
              </>
            )}

            {/* Right panel content — shown inline on mobile/tablet (< lg) */}
            {!isTeamTab && (
              <div className="lg:hidden">
                <RightPanel
                  summary={todaySummary}
                  weeklyHistory={weeklyHistory}
                  showTeamCard={false}
                  selectedMemberId={null}
                  onMemberSelect={() => {}}
                />
              </div>
            )}
          </div>

          {/* Right Panel — fixed width, desktop only, hidden on team tab */}
          {!isTeamTab && (
            <div className="hidden lg:flex w-[280px] flex-shrink-0 min-h-0 overflow-hidden">
              <RightPanel
                summary={todaySummary}
                weeklyHistory={weeklyHistory}
                showTeamCard={false}
                selectedMemberId={null}
                onMemberSelect={() => {}}
              />
            </div>
          )}
        </div>
      </main>

      {/* Member detail drawer */}
      <AnimatePresence>
        {drawerMember && (
          <MemberDetailDrawer
            key={drawerMember.userId}
            member={drawerMember}
            onClose={() => setDrawerMemberId(null)}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
