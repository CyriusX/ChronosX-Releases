import { useState, useEffect } from 'react';
import { ArrowLeft, Calendar, Clock, TrendingUp, Users, AppWindow, BarChart3 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { Sidebar } from '../components/dashboard/Sidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useAuthStore, selectAccessToken } from '../stores/authStore';
import { useNotifications } from '../stores/uiStore';
import { getDailySummary, getTopApps } from '../services/reportApi';
import type { DailySummaryResponse, TopAppsResponse } from '../types/reports';
import { fadeUp, staggerContainer, STAGGER, SPRING } from '../lib/animation';
import { SkeletonShimmer } from '../components/ui/SkeletonShimmer';

export default function Reports() {
  const navigate = useNavigate();
  const { canManageTeam } = usePermissions();
  const accessToken = useAuthStore(selectAccessToken);
  const user = useAuthStore((state) => state.user);
  const { notify } = useNotifications();

  const [selectedDate, setSelectedDate] = useState<string>(
    new Date().toISOString().split('T')[0]
  );
  const [startDate, setStartDate] = useState<string>(
    new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]
  );
  const [endDate, setEndDate] = useState<string>(
    new Date().toISOString().split('T')[0]
  );

  const [selectedUserId, setSelectedUserId] = useState<string | undefined>(undefined);
  const [dailySummary, setDailySummary] = useState<DailySummaryResponse | null>(null);
  const [topApps, setTopApps] = useState<TopAppsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadReportData();
  }, [selectedDate, startDate, endDate, selectedUserId]);

  const loadReportData = async () => {
    if (!accessToken || !user?.orgId) return;

    setIsLoading(true);
    setError(null);

    try {
      const [daily, apps] = await Promise.all([
        getDailySummary(accessToken, selectedDate, selectedUserId),
        getTopApps(accessToken, startDate, endDate, 10, selectedUserId),
      ]);

      setDailySummary(daily);
      setTopApps(apps);
    } catch (err) {
      console.error('[Reports] Error loading report data:', err);
      setError('Não foi possível carregar os relatórios');
      notify.error('Erro ao carregar relatórios');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoBack = () => {
    navigate(-1);
  };

  const formatDuration = (seconds: number): string => {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;

    if (hours > 0) return `${hours}h ${minutes}m`;
    if (minutes > 0) return `${minutes}m ${secs}s`;
    return `${secs}s`;
  };

  const formatPercentage = (value: number, total: number): string => {
    if (total === 0) return '0%';
    return `${Math.round((value / total) * 100)}%`;
  };

  return (
    <div className="flex h-screen bg-[#0b0d14]">
      <Sidebar />

      <main className="flex-1 overflow-auto bg-[#0b0d14] p-6">
        <div className="max-w-7xl mx-auto space-y-6">
          {/* Header */}
          <motion.div
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
            className="flex items-center justify-between"
          >
            <div className="flex items-center gap-4">
              <motion.button
                whileHover={{ scale: 1.08 }}
                whileTap={{ scale: 0.95 }}
                onClick={handleGoBack}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              </motion.button>
              <div>
                <h1 className="text-[20px] font-semibold text-[#f5f7fb]">Relatórios</h1>
                <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                  Análise de tempo e produtividade
                </p>
              </div>
            </div>

            <div className="flex items-center gap-4">
              <div className="flex items-center gap-2">
                <Calendar className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
                <input
                  type="date"
                  value={selectedDate}
                  onChange={(e) => setSelectedDate(e.target.value)}
                  className="bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
                />
              </div>
            </div>
          </motion.div>

          {/* User Filter */}
          {canManageTeam && (
            <motion.div
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1, duration: 0.3 }}
              className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-4"
            >
              <div className="flex items-center gap-3">
                <Users className="w-5 h-5 text-[#4ad9ff]" />
                <span className="text-[13px] text-[rgba(245,247,251,0.6)]">Visualizando como:</span>
                <select
                  value={selectedUserId || ''}
                  onChange={(e) => setSelectedUserId(e.target.value || undefined)}
                  className="bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
                >
                  <option value="">Meus dados</option>
                  <option value="all">Toda a equipe</option>
                </select>
              </div>
            </motion.div>
          )}

          {/* Loading State */}
          {isLoading && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className="space-y-6"
            >
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                {Array.from({ length: 4 }).map((_, i) => (
                  <div key={i} className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5 space-y-4">
                    <SkeletonShimmer width={120} height={16} />
                    <SkeletonShimmer width={80} height={28} />
                    <SkeletonShimmer width={100} height={12} />
                  </div>
                ))}
              </div>
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {Array.from({ length: 2 }).map((_, i) => (
                  <div key={i} className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5 space-y-3">
                    <SkeletonShimmer width={150} height={16} />
                    {Array.from({ length: 4 }).map((_, j) => (
                      <SkeletonShimmer key={j} height={48} rounded="rounded-xl" />
                    ))}
                  </div>
                ))}
              </div>
            </motion.div>
          )}

          {/* Error State */}
          {error && !isLoading && (
            <motion.div
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,107,107,0.2)] rounded-2xl p-6"
            >
              <p className="text-[14px] text-[#ff6b6b]">{error}</p>
              <button onClick={loadReportData} className="mt-3 text-[12px] text-[#4ad9ff] hover:underline">
                Tentar novamente
              </button>
            </motion.div>
          )}

          {/* Report Content */}
          {!isLoading && !error && dailySummary && (
            <div className="space-y-6">
              {/* Daily Summary Cards */}
              <motion.div
                variants={staggerContainer(STAGGER.pills)}
                initial="hidden"
                animate="visible"
                className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4"
              >
                {[
                  { icon: Clock, color: '#4ad9ff', bg: 'rgba(74,217,255,0.15)', title: 'Tempo Ativo', value: formatDuration(dailySummary.totalActiveSeconds), sub: dailySummary.date },
                  { icon: TrendingUp, color: '#f3d05d', bg: 'rgba(243,208,93,0.15)', title: 'Tempo Idle', value: formatDuration(dailySummary.totalIdleSeconds), sub: `${formatPercentage(dailySummary.totalIdleSeconds, dailySummary.totalActiveSeconds + dailySummary.totalIdleSeconds)} do total` },
                  { icon: Calendar, color: '#05df72', bg: 'rgba(5,223,114,0.15)', title: 'Primeira Atividade', value: dailySummary.firstActivity || '--:--', sub: 'Início do dia' },
                  { icon: BarChart3, color: '#8a5cf6', bg: 'rgba(138,92,246,0.15)', title: 'Última Atividade', value: dailySummary.lastActivity || '--:--', sub: 'Fim do dia' },
                ].map((card, i) => (
                  <motion.div
                    key={i}
                    variants={fadeUp}
                    transition={{ type: 'spring', ...SPRING.gentle }}
                    whileHover={{ y: -2, transition: { duration: 0.2 } }}
                    className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5 hover:border-[rgba(74,217,255,0.15)] transition-colors duration-200"
                  >
                    <div className="flex items-center gap-3 mb-4">
                      <motion.div
                        initial={{ scale: 0 }}
                        animate={{ scale: 1 }}
                        transition={{ type: 'spring', ...SPRING.bouncy, delay: 0.2 + i * 0.08 }}
                        className="w-10 h-10 rounded-xl flex items-center justify-center"
                        style={{ backgroundColor: card.bg }}
                      >
                        <card.icon className="w-5 h-5" style={{ color: card.color }} />
                      </motion.div>
                      <h3 className="text-[14px] font-medium text-[#f5f7fb]">{card.title}</h3>
                    </div>
                    <div className="space-y-2">
                      <span className="text-[24px] font-semibold text-[#f5f7fb]">{card.value}</span>
                      <p className="text-[13px] text-[rgba(245,247,251,0.5)]">{card.sub}</p>
                    </div>
                  </motion.div>
                ))}
              </motion.div>

              {/* Apps Grid */}
              <motion.div
                variants={staggerContainer(STAGGER.cards)}
                initial="hidden"
                animate="visible"
                className="grid grid-cols-1 lg:grid-cols-2 gap-6"
              >
                {/* Daily Apps */}
                <motion.div variants={fadeUp} className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-[rgba(255,107,107,0.15)] flex items-center justify-center">
                        <AppWindow className="w-5 h-5 text-[#ff6b6b]" />
                      </div>
                      <div>
                        <h3 className="text-[14px] font-medium text-[#f5f7fb]">Apps do Dia</h3>
                        <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{dailySummary.apps.length} aplicativos</p>
                      </div>
                    </div>
                  </div>
                  <motion.div
                    variants={staggerContainer(STAGGER.listItems)}
                    initial="hidden"
                    animate="visible"
                    className="space-y-3 max-h-[400px] overflow-auto"
                  >
                    {dailySummary.apps.length === 0 ? (
                      <p className="text-[13px] text-[rgba(245,247,251,0.4)] text-center py-4">Nenhuma atividade registrada</p>
                    ) : (
                      dailySummary.apps.map((app, index) => (
                        <motion.div
                          key={index}
                          variants={fadeUp}
                          whileHover={{ x: 4, backgroundColor: 'rgba(255,255,255,0.04)' }}
                          className="flex items-center justify-between p-3 rounded-xl bg-[rgba(255,255,255,0.02)] transition-colors"
                        >
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-lg bg-[rgba(74,217,255,0.1)] flex items-center justify-center">
                              <span className="text-[11px] font-medium text-[#4ad9ff]">{app.displayName.charAt(0).toUpperCase()}</span>
                            </div>
                            <div>
                              <p className="text-[13px] font-medium text-[#f5f7fb]">{app.displayName}</p>
                              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{app.sessionCount} sessões</p>
                            </div>
                          </div>
                          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.8)]">{formatDuration(app.totalSeconds)}</span>
                        </motion.div>
                      ))
                    )}
                  </motion.div>
                </motion.div>

                {/* Top Apps (Period) */}
                <motion.div variants={fadeUp} className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-[rgba(74,217,255,0.15)] flex items-center justify-center">
                        <TrendingUp className="w-5 h-5 text-[#4ad9ff]" />
                      </div>
                      <div>
                        <h3 className="text-[14px] font-medium text-[#f5f7fb]">Top Apps</h3>
                        <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{startDate} a {endDate}</p>
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 mb-4">
                    <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} className="flex-1 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[12px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]" />
                    <span className="text-[rgba(245,247,251,0.4)]">até</span>
                    <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} className="flex-1 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[12px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]" />
                  </div>

                  <motion.div
                    variants={staggerContainer(STAGGER.listItems)}
                    initial="hidden"
                    animate="visible"
                    className="space-y-3 max-h-[400px] overflow-auto"
                  >
                    {!topApps || topApps.apps.length === 0 ? (
                      <p className="text-[13px] text-[rgba(245,247,251,0.4)] text-center py-4">Nenhuma atividade no período</p>
                    ) : (
                      topApps.apps.map((app, index) => (
                        <motion.div
                          key={index}
                          variants={fadeUp}
                          whileHover={{ x: 4, backgroundColor: 'rgba(255,255,255,0.04)' }}
                          className="flex items-center justify-between p-3 rounded-xl bg-[rgba(255,255,255,0.02)] transition-colors"
                        >
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center">
                              <span className="text-[11px] font-bold text-white">#{index + 1}</span>
                            </div>
                            <div>
                              <p className="text-[13px] font-medium text-[#f5f7fb]">{app.displayName}</p>
                              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{app.sessionCount} sessões</p>
                            </div>
                          </div>
                          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.8)]">{formatDuration(app.totalSeconds)}</span>
                        </motion.div>
                      ))
                    )}
                  </motion.div>
                </motion.div>
              </motion.div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
