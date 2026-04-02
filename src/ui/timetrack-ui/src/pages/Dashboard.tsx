import { useState, useEffect, useCallback, useRef } from 'react';
import { useDashboardData } from '../hooks/useDashboardData';
import { useIpc } from '../hooks/useIpc';
import { usePermissions } from '../hooks/usePermissions';
import { useFocusModePolicy } from '../hooks/useFocusModePolicy';
import { getMemberSummary } from '../services/memberApi';
import type { MemberSummaryResponse } from '../types/member';
import {
  Sidebar,
  DashboardHeader,
  TopCards,
  ActivitySection,
  BottomCards,
  RightPanel,
} from '../components/dashboard';
import type { TodaySummaryResponse } from '../types/ipc';

const MEMBER_POLL_INTERVAL_MS = 30_000; // Refresh member data every 30s

function formatSyncTime(isoString: string): string {
  const syncDate = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - syncDate.getTime();
  const diffMin = Math.floor(diffMs / 60000);

  if (diffMin < 1) return 'agora';
  if (diffMin < 60) return `há ${diffMin}min`;

  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `há ${diffHours}h ${diffMin % 60}min`;

  return syncDate.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
}

export default function Dashboard() {
  const [activeTab, setActiveTab] = useState<'meu-dia' | 'equipe'>('meu-dia');
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [memberSummary, setMemberSummary] = useState<TodaySummaryResponse | null>(null);
  const [memberLastSyncAt, setMemberLastSyncAt] = useState<string | null>(null);
  const [memberLoading, setMemberLoading] = useState(false);
  const [memberError, setMemberError] = useState<string | null>(null);
  const memberPollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const { sendCommand, sendQuery } = useIpc();
  const { todaySummary, weeklyHistory, isPaused, isTracking, refreshData } = useDashboardData();
  const { canManageTeam } = usePermissions();
  const { focusModePolicy } = useFocusModePolicy();
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

  // Fetch selected member's summary
  const fetchMemberSummary = useCallback(async (userId: string, isInitial = false) => {
    if (isInitial) {
      setMemberLoading(true);
      setMemberError(null);
      setMemberSummary(null); // Clear stale data so UI doesn't show previous member's data
    }
    try {
      console.log('[Dashboard] Fetching member summary for:', userId);
      const data = await getMemberSummary(userId);
      const raw = data as MemberSummaryResponse;
      setMemberSummary(raw as unknown as TodaySummaryResponse);
      setMemberLastSyncAt(raw.lastSyncAt ?? null);
      setMemberError(null);
    } catch (err) {
      console.error('[Dashboard] Error fetching member summary:', err);
      if (isInitial) {
        setMemberSummary(null);
        setMemberError(err instanceof Error ? err.message : 'Erro ao carregar dados do membro');
      }
    } finally {
      if (isInitial) setMemberLoading(false);
    }
  }, []);

  // Fetch on member selection + start polling
  useEffect(() => {
    if (selectedMemberId && activeTab === 'equipe') {
      fetchMemberSummary(selectedMemberId, true);

      // Poll for fresh data
      memberPollRef.current = setInterval(() => {
        fetchMemberSummary(selectedMemberId);
      }, MEMBER_POLL_INTERVAL_MS);
    }

    return () => {
      if (memberPollRef.current) {
        clearInterval(memberPollRef.current);
        memberPollRef.current = null;
      }
    };
  }, [selectedMemberId, activeTab, fetchMemberSummary]);

  // Clear member selection when switching back to "Meu dia"
  useEffect(() => {
    if (activeTab === 'meu-dia') {
      setSelectedMemberId(null);
      setMemberSummary(null);
      setMemberLastSyncAt(null);
      setMemberError(null);
      setMemberLoading(false);
    }
  }, [activeTab]);

  const isTeamTab = activeTab === 'equipe';
  const isViewingMember = isTeamTab && !!selectedMemberId;

  // When viewing a member, show their data; otherwise show own data
  const displaySummary = isViewingMember ? memberSummary : todaySummary;
  const displayWeeklyHistory = isViewingMember && memberSummary
    ? memberSummary.weeklyHistory
    : weeklyHistory;

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
    <div className="flex h-screen bg-transparent overflow-hidden">
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
            {/* Loading overlay for member data */}
            {isViewingMember && memberLoading && (
              <div className="flex items-center justify-center py-8">
                <div className="flex items-center gap-3">
                  <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                  <span className="text-[13px] text-[rgba(245,247,251,0.5)]">Carregando dados do membro...</span>
                </div>
              </div>
            )}

            {/* Error state for member data */}
            {isViewingMember && memberError && !memberLoading && (
              <div className="flex items-center justify-center py-6">
                <div className="text-center">
                  <p className="text-[13px] text-[rgba(248,113,113,0.9)]">Erro ao carregar dados</p>
                  <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-1">{memberError}</p>
                  <button
                    onClick={() => selectedMemberId && fetchMemberSummary(selectedMemberId, true)}
                    className="mt-2 px-3 py-1 text-[11px] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)] rounded-md hover:bg-[rgba(139,92,246,0.1)] transition-colors"
                  >
                    Tentar novamente
                  </button>
                </div>
              </div>
            )}

            {/* Prompt to select a member when on team tab with no selection */}
            {isTeamTab && !selectedMemberId && (
              <div className="flex items-center justify-center py-12">
                <div className="text-center">
                  <p className="text-[14px] text-[rgba(245,247,251,0.6)]">Selecione um membro da equipe</p>
                  <p className="text-[11px] text-[rgba(245,247,251,0.3)] mt-1">
                    Use o seletor no painel lateral para visualizar os dados de um membro
                  </p>
                </div>
              </div>
            )}

            {/* "Meu dia" tab — always show own data + activity timeline */}
            {!isTeamTab && (
              <>
                <TopCards
                  summary={displaySummary}
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
                <BottomCards summary={displaySummary} />
              </>
            )}

            {/* Team tab — show member data when selected and loaded */}
            {isViewingMember && !memberLoading && !memberError && displaySummary && (
              <>
                {/* Last sync indicator */}
                {memberLastSyncAt && (
                  <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)]">
                    <div className="w-1.5 h-1.5 rounded-full bg-[#8B5CF6] animate-pulse" />
                    <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                      Última sincronização: {formatSyncTime(memberLastSyncAt)}
                    </span>
                    <span className="text-[10px] text-[rgba(245,247,251,0.25)]">
                      · Sincroniza a cada 60s
                    </span>
                  </div>
                )}
                <TopCards
                  summary={displaySummary}
                  isPaused={false}
                  isTracking={false}
                  isTeamTab={true}
                  workGoalSeconds={workGoalSeconds}
                />
                <BottomCards summary={displaySummary} />
              </>
            )}
          </div>

          {/* Right Panel — fixed width, scrollable */}
          <div className="w-[280px] flex-shrink-0 overflow-y-auto">
            <RightPanel
              summary={displaySummary}
              weeklyHistory={displayWeeklyHistory}
              showTeamCard={isTeamTab}
              selectedMemberId={selectedMemberId}
              onMemberSelect={setSelectedMemberId}
            />
          </div>
        </div>
      </main>
    </div>
  );
}
