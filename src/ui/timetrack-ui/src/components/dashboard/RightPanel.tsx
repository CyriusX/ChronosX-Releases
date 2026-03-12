import { useEffect, useMemo } from 'react';
import { MoreVertical, ChevronDown, Code2, Layers, Globe } from 'lucide-react';
import { BarChart, Bar, XAxis, ResponsiveContainer, Tooltip } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { TeamMember, AppUsageItem } from './shared';
import { useTeamStatus } from '../../hooks/useTeamStatus';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../../types/ipc';
import type { TeamMemberStatus } from '../../types/member';

interface RightPanelProps {
  summary: TodaySummaryResponse | null;
  weeklyHistory: WeeklyHistoryItem[];
  showTeamCard?: boolean;
}

// Gradient colors for team member avatars
const MEMBER_GRADIENTS = [
  'from-[#ff8904] to-[#f6339a]',
  'from-[#51a2ff] to-[#00b8db]',
  'from-[#c27aff] to-[#f6339a]',
  'from-[#05df72] to-[#00bba7]',
  'from-[#fdc700] to-[#ff6900]',
  'from-[#ff6b6b] to-[#ee5a24]',
  'from-[#a29bfe] to-[#6c5ce7]',
  'from-[#fd79a8] to-[#e84393]',
];

// Generate a consistent gradient index based on member name
function getMemberGradient(name: string): string {
  const hash = name.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
  return MEMBER_GRADIENTS[hash % MEMBER_GRADIENTS.length];
}

// Map TeamMemberStatus to TeamMember props
function mapMemberToProps(member: TeamMemberStatus) {
  return {
    initial: member.displayName.charAt(0).toUpperCase(),
    name: member.displayName,
    time: member.todayDurationFormatted,
    gradient: getMemberGradient(member.displayName),
  };
}

// Apps most used mock data
const appsMostUsed = [
  { icon: <Code2 className="w-4 h-4" />, label: 'VS Code', subtext: '3,5m', time: '22m', color: '#4a9fff' },
  { icon: <Layers className="w-4 h-4" />, label: 'Figma', subtext: '16,3m', time: '18m', color: '#a855f7' },
  { icon: <Globe className="w-4 h-4" />, label: 'Chrome', subtext: '8,8m', time: '22m', color: '#ff8c6b' },
];

export function RightPanel({ weeklyHistory, showTeamCard = false }: RightPanelProps) {
  const { members, isLoading, loadTeamStatus } = useTeamStatus();

  // Load team status when team card is visible
  useEffect(() => {
    if (showTeamCard) {
      loadTeamStatus();
    }
  }, [showTeamCard, loadTeamStatus]);

  // Filter only active members and map to TeamMember props
  const teamMembers = useMemo(() => {
    return members
      .filter((member) => member.status === 'Active')
      .map(mapMemberToProps);
  }, [members]);

  // Calculate weekly total
  const weeklyTotal = weeklyHistory.reduce((sum, item) => sum + item.hours, 0);

  return (
    <aside className="w-[260px] flex flex-col gap-4">
      {/* Equipe Agora Card - Apenas Admin/Gestor */}
      {showTeamCard && (
        <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
          <CardHeader className="pb-0 pt-[17px] px-[17px]">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Equipe agora</span>
              <button className="w-4 h-4 flex items-center justify-center">
                <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              </button>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-3 pb-4 px-[17px]">
            {/* Team Selector */}
            <div className="bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] rounded-[10px] px-[13px] py-1 flex items-center justify-between mb-3">
              <span className="text-[12px] font-medium text-[rgba(245,247,251,0.6)]">Todos os times</span>
              <ChevronDown className="w-[14px] h-[14px] text-[rgba(245,247,251,0.4)]" />
            </div>
            {/* Team Members */}
            <div className="space-y-2">
              {isLoading ? (
                <div className="text-[12px] text-[rgba(245,247,251,0.4)] text-center py-2">
                  Carregando...
                </div>
              ) : teamMembers.length === 0 ? (
                <div className="text-[12px] text-[rgba(245,247,251,0.4)] text-center py-2">
                  Nenhum membro ativo
                </div>
              ) : (
                teamMembers.map((member) => (
                  <TeamMember key={member.initial + member.name} {...member} />
                ))
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Tempo por Projeto Card */}
      <Card className="flex-1 bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[16px] px-[16px]">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Tempo por projeto</span>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-0 px-[16px] pb-4">
          {/* Filter Tabs */}
          <div className="flex gap-[6px] mt-3 mb-2">
            <button className="px-3 py-1 text-[10px] font-medium rounded-lg bg-gradient-to-r from-[rgba(74,217,255,0.15)] to-[rgba(60,123,255,0.15)] border border-[rgba(74,217,255,0.2)] text-[rgba(245,247,251,0.9)]">Todos</button>
            <button className="px-3 py-1 text-[10px] font-medium rounded-lg bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)]">A Gente</button>
            <button className="px-3 py-1 text-[10px] font-medium rounded-lg bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)]">Clientes</button>
          </div>
          {/* Total Time */}
          <div className="py-2">
            <p className="text-[30px] font-semibold text-[#f5f7fb]">{weeklyTotal.toFixed(1)}h</p>
            <p className="text-[10px] text-[rgba(245,247,251,0.4)]">Semana</p>
          </div>

          {/* Bar Chart */}
          <div className="h-[80px]">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={weeklyHistory} margin={{ top: 5, right: 5, left: 5, bottom: 5 }}>
                <XAxis
                  dataKey="dayName"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fill: 'rgba(245,247,251,0.3)', fontSize: 9 }}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: 'rgba(26,29,46,0.9)',
                    border: '1px solid rgba(255,255,255,0.1)',
                    borderRadius: '8px',
                    color: '#f5f7fb',
                  }}
                  formatter={(value) => [`${Number(value).toFixed(1)}h`, 'Horas']}
                />
                <Bar
                  dataKey="hours"
                  fill="#4ad9ff"
                  radius={[4, 4, 0, 0]}
                  maxBarSize={24}
                />
              </BarChart>
            </ResponsiveContainer>
          </div>

          {/* Apps mais usados */}
          <div className="border-t border-[rgba(255,255,255,0.04)] pt-3 mt-4">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Apps mais usados</span>
              <button className="w-4 h-4 flex items-center justify-center">
                <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              </button>
            </div>
            <div className="space-y-3">
              {appsMostUsed.map((app) => (
                <AppUsageItem key={app.label} {...app} />
              ))}
            </div>
          </div>
        </CardContent>
      </Card>
    </aside>
  );
}
