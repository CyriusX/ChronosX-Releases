import { useEffect, useMemo, useRef, useState } from 'react';
import { ChevronDown, Users } from 'lucide-react';
import { BarChart, Bar, XAxis, ResponsiveContainer, Tooltip } from 'recharts';
import { motion, AnimatePresence } from 'motion/react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { AppIcon } from './shared';
import { useTeamStatus } from '../../hooks/useTeamStatus';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../../types/ipc';
import { fadeUp, staggerContainer, STAGGER, SPRING } from '../../lib/animation';

interface RightPanelProps {
  summary: TodaySummaryResponse | null;
  weeklyHistory: WeeklyHistoryItem[];
  showTeamCard?: boolean;
  selectedMemberId?: string | null;
  onMemberSelect?: (memberId: string | null) => void;
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

function getGradientColors(gradientClass: string): [string, string] {
  const fromMatch = gradientClass.match(/from-\[([^\]]+)\]/);
  const toMatch = gradientClass.match(/to-\[([^\]]+)\]/);
  return [fromMatch?.[1] ?? '#4ad9ff', toMatch?.[1] ?? '#3c7bff'];
}

const productivityColors: Record<string, string> = {
  productive: '#4ade80',
  neutral: '#fbbf24',
  distraction: '#f87171',
};

const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-hidden";

export function RightPanel({ summary, weeklyHistory, showTeamCard = false, selectedMemberId, onMemberSelect }: RightPanelProps) {
  const { members, isLoading, loadTeamStatus } = useTeamStatus();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (showTeamCard) loadTeamStatus();
  }, [showTeamCard, loadTeamStatus]);

  const activeMembers = useMemo(() => {
    return members.filter((m) => m.status === 'Active');
  }, [members]);

  const selectedMember = useMemo(() => {
    return activeMembers.find((m) => m.userId === selectedMemberId) ?? null;
  }, [activeMembers, selectedMemberId]);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    if (dropdownOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [dropdownOpen]);

  const topApps = useMemo(() => {
    const apps = summary?.topAppsByExe ?? summary?.topApplications ?? [];
    return [...apps]
      .sort((a, b) => b.duration - a.duration)
      .slice(0, 5)
      .map((app) => ({
        icon: <AppIcon name={app.name} size={14} />,
        label: app.name,
        subtext: `${Math.round(app.percentage)}%`,
        time: formatDuration(app.duration),
        color: productivityColors[app.productivity ?? ''] ?? '#94a3b8',
      }));
  }, [summary]);

  const weeklyTotal = weeklyHistory.reduce((sum, item) => sum + item.hours, 0);

  return (
    <motion.div
      initial={{ opacity: 0, x: 16 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ type: 'spring', ...SPRING.gentle, delay: 0.2 }}
      className="flex flex-col gap-4"
    >
      {/* Equipe Agora */}
      {showTeamCard && (
        <Card className={cardBase}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Equipe agora</span>
              <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                {activeMembers.length} membro{activeMembers.length !== 1 ? 's' : ''}
              </span>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2.5 pb-3 px-4">
            <div ref={dropdownRef}>
              <button
                onClick={() => setDropdownOpen(!dropdownOpen)}
                className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.07)] transition-colors"
              >
                {selectedMember ? (
                  <>
                    <div className={`w-6 h-6 rounded-full bg-gradient-to-br ${getMemberGradient(selectedMember.displayName)} flex items-center justify-center flex-shrink-0`}>
                      <span className="text-[10px] font-semibold text-white">
                        {selectedMember.displayName.charAt(0).toUpperCase()}
                      </span>
                    </div>
                    <div className="flex-1 min-w-0 text-left">
                      <p className="text-[11px] font-medium text-[rgba(245,247,251,0.9)] truncate">
                        {selectedMember.displayName}
                      </p>
                      <p className="text-[9px] text-[rgba(245,247,251,0.4)]">
                        {selectedMember.todayDurationFormatted} hoje
                      </p>
                    </div>
                  </>
                ) : (
                  <>
                    <Users className="w-4 h-4 text-[rgba(245,247,251,0.4)] flex-shrink-0" />
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)] flex-1 text-left">
                      Selecionar membro
                    </span>
                  </>
                )}
                <motion.div animate={{ rotate: dropdownOpen ? 180 : 0 }} transition={{ duration: 0.2 }}>
                  <ChevronDown className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] flex-shrink-0" />
                </motion.div>
              </button>

              <AnimatePresence>
                {dropdownOpen && (
                  <motion.div
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: 'auto', opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    transition={{ duration: 0.2, ease: [0.4, 0, 0.2, 1] }}
                    style={{ overflow: 'hidden' }}
                    className="mt-2"
                  >
                    <div className="rounded-lg bg-[rgba(0,0,0,0.25)] border border-[rgba(255,255,255,0.06)] max-h-[240px] overflow-y-auto">
                      {isLoading ? (
                        <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-3">Carregando...</p>
                      ) : activeMembers.length > 0 ? (
                        <motion.div
                          variants={staggerContainer(STAGGER.fast)}
                          initial="hidden"
                          animate="visible"
                        >
                          {activeMembers.map((member) => {
                            const gradient = getMemberGradient(member.displayName);
                            const [fromColor] = getGradientColors(gradient);
                            const isSelected = member.userId === selectedMemberId;
                            return (
                              <motion.button
                                key={member.userId}
                                variants={fadeUp}
                                onClick={() => {
                                  onMemberSelect?.(member.userId);
                                  setDropdownOpen(false);
                                }}
                                className={`w-full flex items-center gap-2.5 px-3 py-2 hover:bg-[rgba(255,255,255,0.06)] transition-colors first:rounded-t-lg last:rounded-b-lg ${isSelected ? 'bg-[rgba(74,217,255,0.08)] border-l-2 border-l-[#4ad9ff]' : ''}`}
                              >
                                <div className={`w-6 h-6 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center flex-shrink-0`}>
                                  <span className="text-[10px] font-semibold text-white">
                                    {member.displayName.charAt(0).toUpperCase()}
                                  </span>
                                </div>
                                <div className="flex-1 min-w-0 text-left">
                                  <p className={`text-[11px] font-medium truncate ${isSelected ? 'text-[#4ad9ff]' : 'text-[rgba(245,247,251,0.9)]'}`}>
                                    {member.displayName}
                                  </p>
                                  <p className="text-[9px] text-[rgba(245,247,251,0.4)]">
                                    {member.todayDurationFormatted} hoje
                                  </p>
                                </div>
                                {member.isTracking && (
                                  <div
                                    className="w-1.5 h-1.5 rounded-full flex-shrink-0 animate-pulse"
                                    style={{ backgroundColor: fromColor, boxShadow: `0 0 4px ${fromColor}` }}
                                  />
                                )}
                              </motion.button>
                            );
                          })}
                        </motion.div>
                      ) : (
                        <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-3">
                          Nenhum membro ativo
                        </p>
                      )}
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Atividade semanal */}
      {(!showTeamCard || selectedMemberId) && <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Atividade semanal</span>
            <span className="text-[11px] text-[rgba(245,247,251,0.4)]">{Math.floor(weeklyTotal)}h {Math.round((weeklyTotal % 1) * 60)}m total</span>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          {weeklyTotal > 0 ? (
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
                    formatter={(value) => {
                      const totalMinutes = Math.round(Number(value) * 60);
                      const h = Math.floor(totalMinutes / 60);
                      const m = totalMinutes % 60;
                      return [h > 0 ? `${h}h ${m}m` : `${m}m`, 'Tempo'];
                    }}
                  />
                  <Bar dataKey="hours" fill="#4ad9ff" radius={[3, 3, 0, 0]} maxBarSize={20} animationDuration={800} animationEasing="ease-out" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          ) : (
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-6">No data yet</p>
          )}

          {/* Apps mais usados */}
          <div className="border-t border-[rgba(255,255,255,0.04)] pt-2.5 mt-3">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[12px] font-medium text-[rgba(245,247,251,0.9)]">Apps mais usados</span>
            </div>
            <motion.div
              variants={staggerContainer(STAGGER.listItems)}
              initial="hidden"
              animate="visible"
              className="space-y-2"
            >
              {topApps.length > 0 ? (
                topApps.map((app) => (
                  <motion.div key={app.label} variants={fadeUp} className="flex items-center gap-2 min-w-0">
                    <AppIcon name={app.label} size={14} />
                    <div className="flex-1 min-w-0">
                      <p className="text-[11px] font-medium text-[rgba(245,247,251,0.9)] truncate">{app.label}</p>
                      <div className="flex items-center gap-1.5">
                        <div
                          className="w-[5px] h-[5px] rounded-full flex-shrink-0"
                          style={{ backgroundColor: app.color, boxShadow: `0px 0px 3px 0px ${app.color}` }}
                        />
                        <span className="text-[9px] text-[rgba(245,247,251,0.4)]">{app.subtext}</span>
                      </div>
                    </div>
                    <span className="text-[11px] text-[rgba(245,247,251,0.4)] flex-shrink-0">{app.time}</span>
                  </motion.div>
                ))
              ) : (
                <p className="text-[10px] text-[rgba(245,247,251,0.4)] text-center py-1">No data yet</p>
              )}
            </motion.div>
          </div>
        </CardContent>
      </Card>}
    </motion.div>
  );
}
