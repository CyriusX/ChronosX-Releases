/**
 * Dashboard — Web Admin Portal
 *
 * Mirrors the desktop Dashboard's "Equipe" (team) tab exactly.
 * Uses the same components: TopCards, BottomCards, RightPanel.
 * Data comes from cloud API via getMemberSummary.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { getMemberSummary } from '../services/memberApi';
import { TopCards } from '@desktop/components/dashboard/TopCards';
import { BottomCards } from '@desktop/components/dashboard/BottomCards';
import { RightPanel } from '@desktop/components/dashboard/RightPanel';
import type { TodaySummaryResponse } from '@desktop/types/ipc';
import type { MemberSummaryResponse } from '@desktop/types/member';

const MEMBER_POLL_INTERVAL_MS = 30_000;

function formatSyncTime(isoString: string): string {
  const syncDate = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - syncDate.getTime();
  const diffMin = Math.floor(diffMs / 60000);

  if (diffMin < 1) return 'agora';
  if (diffMin < 60) return `ha ${diffMin}min`;

  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `ha ${diffHours}h ${diffMin % 60}min`;

  return syncDate.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
}

export default function Dashboard() {
  const user = useAuthStore((state) => state.user);

  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [memberSummary, setMemberSummary] = useState<TodaySummaryResponse | null>(null);
  const [memberLastSyncAt, setMemberLastSyncAt] = useState<string | null>(null);
  const [memberLoading, setMemberLoading] = useState(false);
  const [memberError, setMemberError] = useState<string | null>(null);
  const memberPollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Fetch selected member's summary from cloud API
  const fetchMemberSummary = useCallback(async (userId: string, isInitial = false) => {
    if (isInitial) {
      setMemberLoading(true);
      setMemberError(null);
      setMemberSummary(null);
    }
    try {
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
    if (selectedMemberId) {
      fetchMemberSummary(selectedMemberId, true);

      memberPollRef.current = setInterval(() => {
        fetchMemberSummary(selectedMemberId);
      }, MEMBER_POLL_INTERVAL_MS);
    } else {
      setMemberSummary(null);
      setMemberLastSyncAt(null);
      setMemberError(null);
    }

    return () => {
      if (memberPollRef.current) {
        clearInterval(memberPollRef.current);
        memberPollRef.current = null;
      }
    };
  }, [selectedMemberId, fetchMemberSummary]);

  const isViewingMember = !!selectedMemberId;
  const displaySummary = memberSummary;
  const displayWeeklyHistory = memberSummary?.weeklyHistory ?? [];

  return (
    <div className="flex h-screen bg-transparent overflow-hidden">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header — simplified, no tabs (always team view) */}
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <div>
            <h1 className="text-[20px] font-semibold text-[#f5f7fb]">Dashboard</h1>
            <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
              Visao geral da equipe · {user?.orgName}
            </p>
          </div>
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

            {/* Prompt to select a member */}
            {!selectedMemberId && (
              <div className="flex items-center justify-center py-12">
                <div className="text-center">
                  <p className="text-[14px] text-[rgba(245,247,251,0.6)]">Selecione um membro da equipe</p>
                  <p className="text-[11px] text-[rgba(245,247,251,0.3)] mt-1">
                    Use o seletor no painel lateral para visualizar os dados de um membro
                  </p>
                </div>
              </div>
            )}

            {/* Member data — same layout as desktop team tab */}
            {isViewingMember && !memberLoading && !memberError && displaySummary && (
              <>
                {/* Last sync indicator */}
                {memberLastSyncAt && (
                  <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)]">
                    <div className="w-1.5 h-1.5 rounded-full bg-[#8B5CF6] animate-pulse" />
                    <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                      Ultima sincronizacao: {formatSyncTime(memberLastSyncAt)}
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
              showTeamCard={true}
              selectedMemberId={selectedMemberId}
              onMemberSelect={setSelectedMemberId}
            />
          </div>
        </div>
      </main>
    </div>
  );
}
