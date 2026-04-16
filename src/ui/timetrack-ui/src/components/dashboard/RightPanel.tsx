import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronDown, Users, MoreVertical } from 'lucide-react';
import { LineChart, Line, XAxis, ResponsiveContainer, Tooltip } from 'recharts';
import { motion, AnimatePresence } from 'motion/react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { AppIcon } from './shared';
import { MyTasksWidget } from './MyTasksWidget';
import { FoldersAccessedCard } from './FoldersAccessedCard';
import { useTeamStatus } from '../../hooks/useTeamStatus';
import { useAuthStore } from '../../stores/authStore';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../../types/ipc';
import { fadeUp, staggerContainer, STAGGER, SPRING } from '../../lib/animation';
import { cardBase as sharedCardBase, getMemberGradient } from './shared/styles';

interface RightPanelProps {
  summary: TodaySummaryResponse | null;
  weeklyHistory: WeeklyHistoryItem[];
  showTeamCard?: boolean;
  selectedMemberId?: string | null;
  onMemberSelect?: (memberId: string | null) => void;
}

function getGradientColors(gradientClass: string): [string, string] {
  const fromMatch = gradientClass.match(/from-\[([^\]]+)\]/);
  const toMatch = gradientClass.match(/to-\[([^\]]+)\]/);
  return [fromMatch?.[1] ?? '#8B5CF6', toMatch?.[1] ?? '#6D28D9'];
}

const productivityColors: Record<string, string> = {
  productive: '#4ade80',
  neutral: '#fbbf24',
  distraction: '#f87171',
};

const cardBase = sharedCardBase + " overflow-hidden";

export function RightPanel({ summary, weeklyHistory, showTeamCard = false, selectedMemberId, onMemberSelect }: RightPanelProps) {
  const { t } = useTranslation();
  const { members, isLoading, loadTeamStatus } = useTeamStatus();
  const currentUser = useAuthStore((state) => state.user);
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [projectFilter, setProjectFilter] = useState<string | null>(null);

  useEffect(() => {
    if (showTeamCard) loadTeamStatus();
  }, [showTeamCard, loadTeamStatus]);

  const activeMembers = useMemo(() => {
    return members.filter((m) => m.status === 'Active'); // TODO: restore currentUser filter: && m.userId !== currentUser?.id
  }, [members, currentUser?.id]);

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
      className="flex flex-col gap-3 h-full overflow-hidden"
    >
      {/* Minhas Tarefas — only on meu-dia tab */}
      {!showTeamCard && <MyTasksWidget />}
      {!showTeamCard && <FoldersAccessedCard />}

      {/* Equipe Agora */}
      {showTeamCard && (
        <Card className={cardBase}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{t('dashboard.teamNow')}</span>
              <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                {activeMembers.length} {activeMembers.length !== 1 ? t('dashboard.membersPlural') : t('dashboard.members')}
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
                        {selectedMember.todayDurationFormatted} {t('dashboard.today')}
                      </p>
                    </div>
                  </>
                ) : (
                  <>
                    <Users className="w-4 h-4 text-[rgba(245,247,251,0.4)] flex-shrink-0" />
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)] flex-1 text-left">
                      {t('dashboard.selectMember')}
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
                        <div className="flex items-center justify-center gap-2 py-3">
                          <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                          <span className="text-[11px] text-[rgba(245,247,251,0.4)]">{t('common.loading')}</span>
                        </div>
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
                                className={`w-full flex items-center gap-2.5 px-3 py-2 hover:bg-[rgba(255,255,255,0.06)] transition-colors first:rounded-t-lg last:rounded-b-lg ${isSelected ? 'bg-[rgba(139,92,246,0.08)] border-l-2 border-l-[#8B5CF6]' : ''}`}
                              >
                                <div className={`w-6 h-6 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center flex-shrink-0`}>
                                  <span className="text-[10px] font-semibold text-white">
                                    {member.displayName.charAt(0).toUpperCase()}
                                  </span>
                                </div>
                                <div className="flex-1 min-w-0 text-left">
                                  <p className={`text-[11px] font-medium truncate ${isSelected ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.9)]'}`}>
                                    {member.displayName}
                                  </p>
                                  <p className="text-[9px] text-[rgba(245,247,251,0.4)]">
                                    {member.todayDurationFormatted} {t('dashboard.today')}
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
                          {t('dashboard.noActiveMember')}
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

      {/* Minha Produtividade */}
      {(!showTeamCard || selectedMemberId) && <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{t('dashboard.myProductivity')}</span>
            <div className="flex items-center gap-1.5">
              <span className="text-[11px] text-[rgba(245,247,251,0.4)]">{Math.floor(weeklyTotal)}h {Math.round((weeklyTotal % 1) * 60)}m</span>
              <span className="text-[9px] text-[rgba(245,247,251,0.25)]">{t('dashboard.week')}</span>
              <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)] ml-1" />
            </div>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          {/* Project filter tabs */}
          <div className="flex items-center gap-1 mb-2 overflow-x-auto">
            <button
              onClick={() => setProjectFilter(null)}
              className={`px-2 py-0.5 rounded-md text-[9px] font-medium transition-all flex-shrink-0 ${
                !projectFilter
                  ? 'bg-[rgba(139,92,246,0.12)] text-[#8B5CF6]'
                  : 'text-[rgba(245,247,251,0.35)] hover:text-[rgba(245,247,251,0.6)]'
              }`}
            >
              {t('dashboard.all')}
            </button>
            {(summary?.topProjects ?? []).slice(0, 3).map((proj) => (
              <button
                key={proj.name}
                onClick={() => setProjectFilter(proj.name)}
                className={`px-2 py-0.5 rounded-md text-[9px] font-medium transition-all flex-shrink-0 ${
                  projectFilter === proj.name
                    ? 'bg-[rgba(139,92,246,0.12)] text-[#8B5CF6]'
                    : 'text-[rgba(245,247,251,0.35)] hover:text-[rgba(245,247,251,0.6)]'
                }`}
              >
                {proj.name}
              </button>
            ))}
          </div>

          {weeklyTotal > 0 ? (
            <div className="h-[120px] w-full">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={weeklyHistory} margin={{ top: 4, right: 8, left: 8, bottom: 0 }}>
                  <XAxis
                    dataKey="dayName"
                    axisLine={false}
                    tickLine={false}
                    tick={{ fontSize: 9, fill: 'rgba(245,247,251,0.3)' }}
                  />
                  <Tooltip
                    contentStyle={{ backgroundColor: 'rgba(18,21,33,0.95)', border: '1px solid rgba(255,255,255,0.10)', borderRadius: '16px', fontSize: '11px', backdropFilter: 'blur(20px)' }}
                    formatter={(value) => {
                      const totalMinutes = Math.round(Number(value) * 60);
                      const h = Math.floor(totalMinutes / 60);
                      const m = totalMinutes % 60;
                      return [h > 0 ? `${h}h ${m}m` : `${m}m`, t('dashboard.time')];
                    }}
                  />
                  <defs>
                    <linearGradient id="lineGradient" x1="0" y1="0" x2="1" y2="0">
                      <stop offset="0%" stopColor="#8B5CF6" />
                      <stop offset="100%" stopColor="#22D3EE" />
                    </linearGradient>
                  </defs>
                  <Line
                    type="monotone"
                    dataKey="hours"
                    stroke="url(#lineGradient)"
                    strokeWidth={2}
                    dot={{ r: 3, fill: '#22D3EE', stroke: 'rgb(10,12,18)', strokeWidth: 2 }}
                    activeDot={{ r: 5, fill: '#22D3EE', stroke: 'rgb(10,12,18)', strokeWidth: 2 }}
                    animationDuration={800}
                    animationEasing="ease-out"
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          ) : (
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-6">{t('dashboard.noDataYet')}</p>
          )}

          {/* Apps mais usados */}
          <div className="border-t border-[rgba(255,255,255,0.04)] pt-2.5 mt-3">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[12px] font-medium text-[rgba(245,247,251,0.9)]">{t('dashboard.topApps')}</span>
              <MoreVertical className="w-3 h-3 text-[rgba(245,247,251,0.2)]" />
            </div>
            <motion.div
              variants={staggerContainer(STAGGER.listItems)}
              initial="hidden"
              animate="visible"
              className="space-y-2.5"
            >
              {topApps.length > 0 ? (
                topApps.map((app) => {
                  // Calculate bar width relative to max app
                  const maxDuration = topApps[0] ? parseFloat(topApps[0].subtext) : 1;
                  const currentPct = parseFloat(app.subtext) || 0;
                  const barWidth = maxDuration > 0 ? (currentPct / maxDuration) * 100 : 0;

                  return (
                    <motion.div key={app.label} variants={fadeUp} className="relative">
                      {/* Background usage bar */}
                      <div
                        className="absolute inset-0 rounded-md opacity-[0.06]"
                        style={{
                          width: `${barWidth}%`,
                          backgroundColor: app.color,
                        }}
                      />
                      <div className="relative flex items-center gap-2 min-w-0 py-0.5">
                        <AppIcon name={app.label} size={16} />
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
                        <span className="text-[11px] text-[rgba(245,247,251,0.5)] flex-shrink-0 font-medium">{app.time}</span>
                      </div>
                    </motion.div>
                  );
                })
              ) : (
                <p className="text-[10px] text-[rgba(245,247,251,0.4)] text-center py-1">{t('dashboard.noDataYet')}</p>
              )}
            </motion.div>
          </div>
        </CardContent>
      </Card>}
    </motion.div>
  );
}
