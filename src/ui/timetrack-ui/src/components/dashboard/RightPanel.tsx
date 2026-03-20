import { useEffect, useMemo } from 'react';
import { MoreVertical, ChevronDown, Globe } from 'lucide-react';
import { BarChart, Bar, XAxis, ResponsiveContainer, Tooltip } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { TeamMember, AppUsageItem } from './shared';
import { useTeamStatus } from '../../hooks/useTeamStatus';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../../types/ipc';
import type { TeamMemberStatus } from '../../types/member';

interface RightPanelProps {
  summary: TodaySummaryResponse | null;
  weeklyHistory: WeeklyHistoryItem[];
  showTeamCard?: boolean;
}

const MEMBER_GRADIENTS = [
  'from-[#ff8904] to-[#f6339a]',
  'from-[#51a2ff] to-[#00b8db]',
  'from-[#c27aff] to-[#f6339a]',
  'from-[#05df72] to-[#00bba7]',
  'from-[#fdc700] to-[#ff6900]',
];

function getMemberGradient(name: string): string {
  const hash = name.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
  return MEMBER_GRADIENTS[hash % MEMBER_GRADIENTS.length];
}

function mapMemberToProps(member: TeamMemberStatus) {
  return {
    initial: member.displayName.charAt(0).toUpperCase(),
    name: member.displayName,
    time: member.todayDurationFormatted,
    gradient: getMemberGradient(member.displayName),
  };
}

const productivityColors: Record<string, string> = {
  productive: '#4ade80',
  neutral: '#fbbf24',
  distraction: '#f87171',
};

const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-hidden";

export function RightPanel({ summary, weeklyHistory, showTeamCard = false }: RightPanelProps) {
  const { members, isLoading, loadTeamStatus } = useTeamStatus();

  useEffect(() => {
    if (showTeamCard) loadTeamStatus();
  }, [showTeamCard, loadTeamStatus]);

  const teamMembers = useMemo(() => {
    return members.filter((m) => m.status === 'Active').map(mapMemberToProps);
  }, [members]);

  // Use topAppsByExe for "Apps mais usados" — aggregates browser tabs into parent app
  const topApps = useMemo(() => {
    const apps = summary?.topAppsByExe ?? summary?.topApplications ?? [];
    return [...apps]
      .sort((a, b) => b.duration - a.duration)
      .slice(0, 5)
      .map((app) => ({
        icon: <Globe className="w-3.5 h-3.5" />,
        label: app.name,
        subtext: `${Math.round(app.percentage)}%`,
        time: formatDuration(app.duration),
        color: productivityColors[app.productivity] ?? '#94a3b8',
      }));
  }, [summary]);

  const weeklyTotal = weeklyHistory.reduce((sum, item) => sum + item.hours, 0);

  return (
    <div className="flex flex-col gap-4">
      {/* Equipe Agora */}
      {showTeamCard && (
        <Card className={cardBase}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Equipe agora</span>
              <button className="flex items-center gap-1 text-[10px] text-[rgba(245,247,251,0.4)]">
                Todos <ChevronDown className="w-3 h-3" />
              </button>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2.5 pb-3 px-4">
            <div className="space-y-2">
              {teamMembers.length > 0 ? (
                teamMembers.slice(0, 5).map((member, i) => (
                  <TeamMember key={i} {...member} />
                ))
              ) : (
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-2">
                  {isLoading ? 'Carregando...' : 'Nenhum membro ativo'}
                </p>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Tempo por Projeto */}
      <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Tempo por projeto</span>
            <span className="text-[11px] text-[rgba(245,247,251,0.4)]">{weeklyTotal.toFixed(1)}h</span>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="h-[120px] w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={weeklyHistory} margin={{ top: 4, right: 0, left: 0, bottom: 0 }}>
                <XAxis
                  dataKey="dayName"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 9, fill: 'rgba(245,247,251,0.3)' }}
                />
                <Tooltip
                  contentStyle={{ backgroundColor: '#1a1d2e', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '8px', fontSize: '11px' }}
                  formatter={(value) => [`${Number(value).toFixed(1)}h`, 'Horas']}
                />
                <Bar dataKey="hours" fill="#4ad9ff" radius={[3, 3, 0, 0]} maxBarSize={20} />
              </BarChart>
            </ResponsiveContainer>
          </div>

          {/* Apps mais usados */}
          <div className="border-t border-[rgba(255,255,255,0.04)] pt-2.5 mt-3">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[12px] font-medium text-[rgba(245,247,251,0.9)]">Apps mais usados</span>
            </div>
            <div className="space-y-2">
              {topApps.length > 0 ? (
                topApps.map((app) => (
                  <AppUsageItem key={app.label} {...app} />
                ))
              ) : (
                <p className="text-[10px] text-[rgba(245,247,251,0.4)] text-center py-1">Nenhum app</p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
