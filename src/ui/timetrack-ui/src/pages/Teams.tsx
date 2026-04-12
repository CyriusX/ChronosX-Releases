/**
 * Teams page — card grid of all team members with a detail drawer.
 * Accessible to Admin and Gestor roles only.
 */

import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import { Users, Wifi, Activity, RefreshCw } from 'lucide-react';
import { usePermissions } from '../hooks/usePermissions';
import { useTeamStatus } from '../hooks/useTeamStatus';
import { MemberCard } from '../components/teams/MemberCard';
import { MemberDetailDrawer } from '../components/teams/MemberDetailDrawer';
import { staggerContainer, fadeUp } from '../lib/animation';
import { Sidebar } from '../components/dashboard/Sidebar';
import type { TeamMemberStatus } from '../types/member';

export default function Teams() {
  const { canManageTeam } = usePermissions();

  if (!canManageTeam) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0">
      <Sidebar />
      <TeamsContent />
    </div>
  );
}

function TeamsContent() {
  const { members, isLoading, error, activeCount, trackingCount, loadTeamStatus } = useTeamStatus();
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  // Load on mount
  useEffect(() => {
    loadTeamStatus(true);
  }, [loadTeamStatus]);

  const selectedMember = members.find((m: TeamMemberStatus) => m.userId === selectedMemberId) ?? null;

  const handleRefresh = async () => {
    setRefreshing(true);
    await loadTeamStatus(false);
    setRefreshing(false);
  };

  return (
    <main className="flex-1 overflow-y-auto px-6 py-6">
      {/* Page header */}
      <motion.div
        variants={fadeUp}
        initial="hidden"
        animate="visible"
        className="flex items-center justify-between mb-6"
      >
        <div>
          <h1 className="text-[22px] font-bold text-[#f5f7fb] tracking-[-0.3px]">Equipe</h1>
          <p className="text-[12px] text-[rgba(245,247,251,0.45)] mt-0.5">
            Acompanhe o status e produtividade da sua equipe em tempo real
          </p>
        </div>

        {/* Badges + refresh */}
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)]">
              <Users className="w-3 h-3 text-[rgba(245,247,251,0.45)]" />
              <span className="text-[11px] text-[rgba(245,247,251,0.6)]">{members.length} membros</span>
            </div>
            {activeCount > 0 && (
              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(5,223,114,0.08)] border border-[rgba(5,223,114,0.2)]">
                <Wifi className="w-3 h-3 text-[#05df72]" />
                <span className="text-[11px] text-[#05df72]">{activeCount} ativos</span>
              </div>
            )}
            {trackingCount > 0 && (
              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.2)]">
                <Activity className="w-3 h-3 text-[#8B5CF6]" />
                <span className="text-[11px] text-[#8B5CF6]">{trackingCount} monitorando</span>
              </div>
            )}
          </div>

          <button
            onClick={handleRefresh}
            disabled={isLoading || refreshing}
            className="p-2 rounded-lg text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.06)] transition-colors disabled:opacity-40"
            title="Atualizar"
          >
            <RefreshCw className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </motion.div>

      {/* Error state */}
      {error && (
        <div className="mb-4 px-4 py-3 rounded-[12px] bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)] text-[12px] text-[#f87171]">
          {error}
        </div>
      )}

      {/* Loading skeleton */}
      {isLoading && members.length === 0 ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="rounded-[22px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] h-[210px] animate-pulse" />
          ))}
        </div>
      ) : members.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 gap-3">
          <Users className="w-12 h-12 text-[rgba(245,247,251,0.15)]" />
          <p className="text-[13px] text-[rgba(245,247,251,0.35)]">Nenhum membro na equipe</p>
        </div>
      ) : (
        <motion.div
          variants={staggerContainer(0.06)}
          initial="hidden"
          animate="visible"
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4"
        >
          {members.map((member: TeamMemberStatus, i: number) => (
            <MemberCard
              key={member.userId}
              member={member}
              index={i}
              onSelect={setSelectedMemberId}
            />
          ))}
        </motion.div>
      )}

      {/* Detail drawer */}
      <AnimatePresence>
        {selectedMember && (
          <MemberDetailDrawer
            key={selectedMember.userId}
            member={selectedMember}
            onClose={() => setSelectedMemberId(null)}
          />
        )}
      </AnimatePresence>
    </main>
  );
}
