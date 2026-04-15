/**
 * Dashboard — Web Admin Portal
 *
 * Shows all team members as a card grid.
 * Clicking a card opens a detail drawer with full data + manager actions.
 */

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { useTeamStatus } from '../hooks/useTeamStatus';
import { MemberCard } from '@desktop/components/teams/MemberCard';
import { MemberDetailDrawer } from '@desktop/components/teams/MemberDetailDrawer';
import { staggerContainer } from '@desktop/lib/animation';
import type { TeamMemberStatus } from '@desktop/types/member';

export default function Dashboard() {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const { members, isLoading, lastFetchedAt, loadTeamStatus } = useTeamStatus();
  const [drawerMemberId, setDrawerMemberId] = useState<string | null>(null);

  // Load members on mount
  useEffect(() => {
    loadTeamStatus();
  }, [loadTeamStatus]);

  const drawerMember = members.find((m: TeamMemberStatus) => m.userId === drawerMemberId) ?? null;

  return (
    <div className="flex h-screen bg-transparent overflow-hidden pt-[52px] md:pt-0">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header */}
        <div className="px-4 lg:px-5 pt-4 pb-3 flex-shrink-0">
          <header className="flex items-center justify-between">
            <div className="relative">
              <h1 className="text-[16px] sm:text-[20px] font-semibold text-[#f5f7fb] pb-2">{t('dashboard.teamHeader')}</h1>
              <motion.div
                className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3)]"
                layoutId="web-dashboard-tab"
              />
              <p className="text-[11px] sm:text-[12px] text-[rgba(245,247,251,0.4)] mt-1">{user?.orgName}</p>
              {lastFetchedAt && (
                <p className="text-[10px] text-[rgba(245,247,251,0.25)] mt-0.5">
                  {t('common.refresh')} {lastFetchedAt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                </p>
              )}
            </div>
            <motion.button
              onClick={() => loadTeamStatus()}
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              className="w-9 h-9 rounded-[12px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              title={t('common.refresh')}
            >
              <RefreshCw className={`w-4 h-4 text-[rgba(245,247,251,0.6)] ${isLoading ? 'animate-spin' : ''}`} />
            </motion.button>
          </header>
        </div>

        {/* Card grid */}
        <div className="flex-1 overflow-y-auto px-4 lg:px-5 pb-4">
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
              <p className="text-[13px] text-[rgba(245,247,251,0.35)]">{t('dashboard.noMembers')}</p>
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
