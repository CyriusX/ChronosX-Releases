/**
 * Dashboard — Web Admin Portal
 *
 * Team-focused dashboard that mirrors the desktop "Equipe" tab.
 * Responsive: sidebar on desktop, hamburger on mobile.
 * Member selector at top, cards flow below.
 */

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Users, ChevronDown, RefreshCw } from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { getMemberSummary, getTeamStatus } from '../services/memberApi';
import { TopCards } from '@desktop/components/dashboard/TopCards';
import { BottomCards } from '@desktop/components/dashboard/BottomCards';
import { RightPanel } from '@desktop/components/dashboard/RightPanel';
import { getMemberGradient } from '@desktop/components/dashboard/shared/styles';
import type { TodaySummaryResponse } from '@desktop/types/ipc';
import type { MemberSummaryResponse, TeamMemberStatus } from '@desktop/types/member';

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

  const [teamMembers, setTeamMembers] = useState<TeamMemberStatus[]>([]);
  const [teamLoading, setTeamLoading] = useState(true);
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [memberSummary, setMemberSummary] = useState<TodaySummaryResponse | null>(null);
  const [memberLastSyncAt, setMemberLastSyncAt] = useState<string | null>(null);
  const [memberLoading, setMemberLoading] = useState(false);
  const [memberError, setMemberError] = useState<string | null>(null);
  const memberPollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const [showMemberDropdown, setShowMemberDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click
  useEffect(() => {
    if (!showMemberDropdown) return;
    const handler = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setShowMemberDropdown(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [showMemberDropdown]);

  const fetchTeamStatus = useCallback(async () => {
    try {
      const data = await getTeamStatus();
      setTeamMembers(data.members.filter(m => m.status === 'Active'));
    } catch (err) {
      console.error('[Dashboard] Error fetching team status:', err);
    } finally {
      setTeamLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTeamStatus();
    const interval = setInterval(fetchTeamStatus, MEMBER_POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [fetchTeamStatus]);

  const fetchMemberSummary = useCallback(async (userId: string, isInitial = false) => {
    if (isInitial) { setMemberLoading(true); setMemberError(null); setMemberSummary(null); }
    try {
      const data = await getMemberSummary(userId);
      const raw = data as MemberSummaryResponse;
      setMemberSummary(raw as unknown as TodaySummaryResponse);
      setMemberLastSyncAt(raw.lastSyncAt ?? null);
      setMemberError(null);
    } catch (err) {
      if (isInitial) {
        setMemberSummary(null);
        setMemberError(err instanceof Error ? err.message : 'Erro ao carregar dados do membro');
      }
    } finally {
      if (isInitial) setMemberLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedMemberId) {
      fetchMemberSummary(selectedMemberId, true);
      memberPollRef.current = setInterval(() => fetchMemberSummary(selectedMemberId), MEMBER_POLL_INTERVAL_MS);
    } else {
      setMemberSummary(null); setMemberLastSyncAt(null); setMemberError(null);
    }
    return () => { if (memberPollRef.current) { clearInterval(memberPollRef.current); memberPollRef.current = null; } };
  }, [selectedMemberId, fetchMemberSummary]);

  const selectedMember = useMemo(
    () => teamMembers.find(m => m.userId === selectedMemberId) ?? null,
    [teamMembers, selectedMemberId]
  );

  const isViewingMember = !!selectedMemberId;
  const displaySummary = memberSummary;
  const displayWeeklyHistory = memberSummary?.weeklyHistory ?? [];

  return (
    <div className="flex h-screen bg-transparent overflow-hidden pt-[52px] md:pt-0">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header */}
        <div className="px-4 lg:px-5 pt-4 pb-2 flex-shrink-0">
          <header className="flex items-center justify-between">
            <div className="relative">
              <h1 className="text-[16px] sm:text-[20px] font-semibold text-[#f5f7fb] pb-2">Equipe</h1>
              <motion.div
                className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3)]"
                layoutId="web-dashboard-tab"
              />
              <p className="text-[11px] sm:text-[12px] text-[rgba(245,247,251,0.4)] mt-1">{user?.orgName}</p>
            </div>
            <motion.button
              onClick={() => { fetchTeamStatus(); if (selectedMemberId) fetchMemberSummary(selectedMemberId, true); }}
              whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
              className="w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <RefreshCw className={`w-4 h-4 text-[rgba(245,247,251,0.6)] ${memberLoading ? 'animate-spin' : ''}`} />
            </motion.button>
          </header>
        </div>

        {/* Member Selector */}
        <div className="px-4 lg:px-5 pb-3 flex-shrink-0" ref={dropdownRef}>
          <div className="relative">
            <motion.button
              onClick={() => setShowMemberDropdown(!showMemberDropdown)}
              className="w-full flex items-center gap-3 px-4 py-3 bg-gradient-to-r from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl hover:border-[rgba(255,255,255,0.1)] transition-colors"
              whileHover={{ scale: 1.003 }} whileTap={{ scale: 0.997 }}
            >
              {selectedMember ? (
                <>
                  <div className={`w-7 h-7 rounded-full bg-gradient-to-br ${getMemberGradient(selectedMember.displayName)} flex items-center justify-center flex-shrink-0`}>
                    <span className="text-[10px] font-semibold text-white">{selectedMember.displayName.charAt(0).toUpperCase()}</span>
                  </div>
                  <div className="flex-1 min-w-0 text-left">
                    <p className="text-[13px] font-medium text-[rgba(245,247,251,0.9)] truncate">{selectedMember.displayName}</p>
                    <p className="text-[10px] text-[rgba(245,247,251,0.4)]">
                      {selectedMember.todayDurationFormatted} hoje
                      {selectedMember.isTracking && <span className="text-[#05df72] ml-1">· Ativo</span>}
                    </p>
                  </div>
                </>
              ) : (
                <>
                  <Users className="w-4 h-4 text-[#8B5CF6] flex-shrink-0" />
                  <span className="text-[13px] text-[rgba(245,247,251,0.6)]">Selecionar membro da equipe</span>
                </>
              )}
              <motion.div animate={{ rotate: showMemberDropdown ? 180 : 0 }} transition={{ duration: 0.2 }} className="ml-auto">
                <ChevronDown className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              </motion.div>
            </motion.button>

            <AnimatePresence>
              {showMemberDropdown && (
                <motion.div
                  initial={{ opacity: 0, y: -8, scale: 0.95 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  exit={{ opacity: 0, y: -8, scale: 0.95 }}
                  transition={{ duration: 0.15 }}
                  className="absolute top-full left-0 right-0 mt-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl z-30 max-h-[300px] overflow-y-auto"
                >
                  {teamLoading ? (
                    <div className="flex items-center justify-center gap-2 py-4">
                      <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                      <span className="text-[11px] text-[rgba(245,247,251,0.4)]">Carregando...</span>
                    </div>
                  ) : teamMembers.length === 0 ? (
                    <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-4">Nenhum membro ativo</p>
                  ) : (
                    teamMembers.map((member) => {
                      const gradient = getMemberGradient(member.displayName);
                      const isSelected = member.userId === selectedMemberId;
                      return (
                        <button
                          key={member.userId}
                          onClick={() => { setSelectedMemberId(member.userId); setShowMemberDropdown(false); }}
                          className={`w-full flex items-center gap-3 px-4 py-2.5 hover:bg-[rgba(255,255,255,0.06)] transition-colors ${
                            isSelected ? 'bg-[rgba(139,92,246,0.08)] border-l-2 border-l-[#8B5CF6]' : ''
                          }`}
                        >
                          <div className={`w-6 h-6 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center flex-shrink-0`}>
                            <span className="text-[10px] font-semibold text-white">{member.displayName.charAt(0).toUpperCase()}</span>
                          </div>
                          <div className="flex-1 min-w-0 text-left">
                            <p className={`text-[11px] font-medium truncate ${isSelected ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.9)]'}`}>
                              {member.displayName}
                            </p>
                            <p className="text-[9px] text-[rgba(245,247,251,0.4)]">{member.todayDurationFormatted} hoje</p>
                          </div>
                          {member.isTracking && (
                            <div className="w-1.5 h-1.5 rounded-full bg-[#05df72] flex-shrink-0 animate-pulse" />
                          )}
                        </button>
                      );
                    })
                  )}
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto min-h-0">
          <div className="flex gap-5 px-4 lg:px-5 pb-4 min-h-0">
            {/* Main content */}
            <div className="flex-1 flex flex-col gap-4 min-w-0">
              {/* Empty state */}
              {!selectedMemberId && (
                <div className="flex items-center justify-center py-16">
                  <div className="text-center">
                    <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)] flex items-center justify-center">
                      <Users className="w-7 h-7 text-[rgba(139,92,246,0.5)]" />
                    </div>
                    <p className="text-[15px] font-medium text-[rgba(245,247,251,0.6)]">Selecione um membro</p>
                    <p className="text-[12px] text-[rgba(245,247,251,0.3)] mt-1 max-w-[260px] mx-auto">
                      Escolha um membro acima para visualizar o resumo do dia
                    </p>
                  </div>
                </div>
              )}

              {/* Loading */}
              {isViewingMember && memberLoading && (
                <div className="flex items-center justify-center py-12">
                  <div className="flex items-center gap-3">
                    <div className="w-5 h-5 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                    <span className="text-[13px] text-[rgba(245,247,251,0.5)]">Carregando dados do membro...</span>
                  </div>
                </div>
              )}

              {/* Error */}
              {isViewingMember && memberError && !memberLoading && (
                <div className="flex items-center justify-center py-8">
                  <div className="text-center">
                    <p className="text-[13px] text-[rgba(248,113,113,0.9)]">Erro ao carregar dados</p>
                    <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-1">{memberError}</p>
                    <button
                      onClick={() => selectedMemberId && fetchMemberSummary(selectedMemberId, true)}
                      className="mt-3 px-4 py-1.5 text-[11px] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)] rounded-lg hover:bg-[rgba(139,92,246,0.1)] transition-colors"
                    >
                      Tentar novamente
                    </button>
                  </div>
                </div>
              )}

              {/* Member data */}
              {isViewingMember && !memberLoading && !memberError && displaySummary && (
                <>
                  {/* Sync indicator */}
                  {memberLastSyncAt && (
                    <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)]">
                      <div className="w-1.5 h-1.5 rounded-full bg-[#8B5CF6] animate-pulse" />
                      <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                        Ultima sincronizacao: {formatSyncTime(memberLastSyncAt)}
                      </span>
                      <span className="text-[10px] text-[rgba(245,247,251,0.25)] hidden sm:inline">· Sincroniza a cada 60s</span>
                    </div>
                  )}

                  {/* TopCards — responsive */}
                  <div className="web-responsive-top-cards">
                    <TopCards summary={displaySummary} isPaused={false} isTracking={false} isTeamTab={true} />
                  </div>

                  {/* BottomCards — responsive */}
                  <div className="web-responsive-bottom-cards">
                    <BottomCards summary={displaySummary} />
                  </div>
                </>
              )}
            </div>

            {/* Right Panel — hidden on smaller screens, shown on xl+ */}
            {isViewingMember && !memberLoading && !memberError && displaySummary && (
              <div className="hidden xl:block w-[280px] flex-shrink-0 web-clean-apps">
                <RightPanel
                  summary={displaySummary}
                  weeklyHistory={displayWeeklyHistory}
                  showTeamCard={false}
                  selectedMemberId={selectedMemberId}
                  onMemberSelect={setSelectedMemberId}
                />
              </div>
            )}
          </div>

          {/* Right Panel content inline on smaller screens */}
          {isViewingMember && !memberLoading && !memberError && displaySummary && (
            <div className="xl:hidden px-4 lg:px-5 pb-4 web-clean-apps">
              <RightPanel
                summary={displaySummary}
                weeklyHistory={displayWeeklyHistory}
                showTeamCard={false}
                selectedMemberId={selectedMemberId}
                onMemberSelect={setSelectedMemberId}
              />
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
