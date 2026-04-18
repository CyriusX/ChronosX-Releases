import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Activity, Users, Wifi } from 'lucide-react';
import { useTeamStatus } from '../../hooks/useTeamStatus';
import { MemberCard } from '../../components/teams/MemberCard';
import type { TeamMemberStatus } from '../../types/member';
import { getDemoMode } from '../demoMode';

export default function TeamsDemo() {
  const { t } = useTranslation();
  const { members, isLoading, error, activeCount, trackingCount, loadTeamStatus } = useTeamStatus();
  const demoMode = getDemoMode();

  useEffect(() => {
    loadTeamStatus(true);
  }, [loadTeamStatus]);

  const visibleMembers = useMemo(() => {
    const max = demoMode === 'mobile' ? 12 : 6;
    return (members as TeamMemberStatus[]).slice(0, max);
  }, [members, demoMode]);

  if (demoMode === 'mobile') {
    return (
      <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0 overflow-x-hidden">
        <main
          className="flex-1 overflow-y-auto px-5 pt-5 pb-4 min-w-0"
          data-demo-scroll-root="teams"
        >
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-2 min-w-0">
              <h1 className="text-[22px] font-bold text-[#f5f7fb] tracking-[-0.3px] truncate">
                {t('teams.title')}
              </h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.45)]">
                {t('teams.subtitle')}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)]">
                <Users className="w-3 h-3 text-[rgba(245,247,251,0.45)]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.6)]">
                  {members.length} {t('teams.membersCount')}
                </span>
              </div>
              {activeCount > 0 && (
                <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(5,223,114,0.08)] border border-[rgba(5,223,114,0.2)]">
                  <Wifi className="w-3 h-3 text-[#05df72]" />
                  <span className="text-[11px] text-[#05df72]">
                    {activeCount} {t('teams.activeCount')}
                  </span>
                </div>
              )}
              {trackingCount > 0 && (
                <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.2)]">
                  <Activity className="w-3 h-3 text-[#8B5CF6]" />
                  <span className="text-[11px] text-[#8B5CF6]">
                    {trackingCount} {t('teams.trackingCount')}
                  </span>
                </div>
              )}
            </div>

            {error && (
              <div className="px-4 py-3 rounded-[12px] bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)] text-[12px] text-[#f87171]">
                {error}
              </div>
            )}
          </div>

          <div className="mt-5">
            {isLoading && visibleMembers.length === 0 ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div
                    key={i}
                    className="rounded-[22px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] h-[210px] animate-pulse"
                  />
                ))}
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {visibleMembers.map((member: TeamMemberStatus, i: number) => (
                  <MemberCard
                    key={member.userId}
                    member={member}
                    index={i}
                    onSelect={() => {}}
                  />
                ))}
                <div data-demo-marker="teams-bottom" className="h-px w-full" />
              </div>
            )}
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0">
      <main className="flex-1 flex flex-col min-w-0 min-h-0 px-6 py-6">
        <div className="flex items-start justify-between gap-4 flex-shrink-0">
          <div className="min-w-0">
            <h1 className="text-[22px] font-bold text-[#f5f7fb] tracking-[-0.3px]">
              {t('teams.title')}
            </h1>
            <p className="text-[12px] text-[rgba(245,247,251,0.45)] mt-0.5">
              {t('teams.subtitle')}
            </p>
          </div>

          <div className="flex items-center gap-2 flex-shrink-0">
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)]">
              <Users className="w-3 h-3 text-[rgba(245,247,251,0.45)]" />
              <span className="text-[11px] text-[rgba(245,247,251,0.6)]">
                {members.length} {t('teams.membersCount')}
              </span>
            </div>
            {activeCount > 0 && (
              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(5,223,114,0.08)] border border-[rgba(5,223,114,0.2)]">
                <Wifi className="w-3 h-3 text-[#05df72]" />
                <span className="text-[11px] text-[#05df72]">
                  {activeCount} {t('teams.activeCount')}
                </span>
              </div>
            )}
            {trackingCount > 0 && (
              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.2)]">
                <Activity className="w-3 h-3 text-[#8B5CF6]" />
                <span className="text-[11px] text-[#8B5CF6]">
                  {trackingCount} {t('teams.trackingCount')}
                </span>
              </div>
            )}
          </div>
        </div>

        {error && (
          <div className="mt-4 px-4 py-3 rounded-[12px] bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)] text-[12px] text-[#f87171] flex-shrink-0">
            {error}
          </div>
        )}

        <div className="mt-5 flex-1 min-h-0 overflow-hidden">
          {isLoading && visibleMembers.length === 0 ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {Array.from({ length: 6 }).map((_, i) => (
                <div
                  key={i}
                  className="rounded-[22px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] h-[210px] animate-pulse"
                />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {visibleMembers.map((member: TeamMemberStatus, i: number) => (
                <MemberCard
                  key={member.userId}
                  member={member}
                  index={i}
                  onSelect={() => {}}
                />
              ))}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
