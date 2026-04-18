import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Activity, Clock, Target } from 'lucide-react';
import { SummaryCard, ProductivityTrend } from '../../components/reports';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card';
import { toLocalDateStr } from '../../types/reports';
import type { ReportsBundleResponse } from '../../types/reports';
import { getReportsBundle } from '../../services/reportApi';
import { getDemoMode } from '../demoMode';

function formatDuration(seconds: number): string {
  if (!seconds) return '0h';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  return `${minutes}m`;
}

function getLastNDaysRange(n: number): { startDate: string; endDate: string } {
  const end = new Date();
  const start = new Date();
  start.setDate(start.getDate() - (n - 1));
  return { startDate: toLocalDateStr(start), endDate: toLocalDateStr(end) };
}

export default function ReportsDemo() {
  const { t } = useTranslation();
  const [{ startDate, endDate }] = useState(() => getLastNDaysRange(30));
  const [bundle, setBundle] = useState<ReportsBundleResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const demoMode = getDemoMode();

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getReportsBundle(startDate, endDate, 'day', { topApps: 6, topPaths: 0, topFolders: 0 })
      .then((res) => {
        if (cancelled) return;
        setBundle(res);
      })
      .catch(() => {
        if (cancelled) return;
        setBundle(null);
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [startDate, endDate]);

  const summary = useMemo(() => {
    const days = bundle?.dailySummaryRange?.days ?? [];
    const active = days.reduce((sum, d) => sum + (d.totalActiveSeconds ?? 0), 0);
    const idle = days.reduce((sum, d) => sum + (d.totalIdleSeconds ?? 0), 0);
    const focus = bundle?.dailySummaryRange?.periodFocusScore
      ?? (days.length > 0
        ? Math.round(days.reduce((sum, d) => sum + (d.focusScore ?? 0), 0) / days.length)
        : 0);
    return { active, idle, focus };
  }, [bundle]);

  const topApps = bundle?.topApps?.apps ?? [];

  if (demoMode === 'mobile') {
    return (
      <div className="min-h-[100dvh] bg-[#0b0d14] pb-14 md:pb-0 overflow-x-hidden">
        <main className="px-5 pt-5 pb-4 overflow-x-hidden" data-demo-scroll-root="reports">
          <div className="flex flex-col gap-2 flex-shrink-0 min-w-0">
            <h1 className="text-[22px] font-bold text-[#f5f7fb] tracking-[-0.3px] truncate">
              {t('reports.title')}
            </h1>
            <p className="text-[12px] text-[rgba(245,247,251,0.45)]">
              {t('reports.subtitle')}
            </p>
            <div className="text-[10px] text-[rgba(245,247,251,0.35)]">
              <span className="truncate">{startDate}</span>
              <span className="mx-1">—</span>
              <span className="truncate">{endDate}</span>
            </div>
          </div>

          <div className="mt-4 grid grid-cols-1 gap-4 flex-shrink-0">
            <SummaryCard
              title={t('reports.activeTime')}
              value={formatDuration(summary.active)}
              subtitle={t('reports.total')}
              icon={Clock}
              badge={t('reports.last30')}
              badgeColor="blue"
              isLoading={loading}
            />
            <SummaryCard
              title={t('reports.idleTime')}
              value={formatDuration(summary.idle)}
              subtitle={t('reports.ofTotal')}
              icon={Activity}
              badge={t('reports.sources')}
              badgeColor="yellow"
              isLoading={loading}
            />
            <SummaryCard
              title={t('reports.focusScore')}
              value={`${summary.focus}`}
              subtitle={t('reports.focusScoreDesc')}
              icon={Target}
              badge={t('reports.avgPerDay')}
              badgeColor="green"
              isLoading={loading}
            />
          </div>

          <div className="mt-4 flex flex-col gap-4 min-w-0">
            <ProductivityTrend
              periods={bundle?.productivityTrend?.periods ?? []}
              isLoading={loading}
              maxHeight={240}
            />

            <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl flex flex-col overflow-hidden">
              <CardHeader className="pb-2 pt-3 px-4 shrink-0">
                <CardTitle className="flex items-center justify-between min-w-0">
                  <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)] truncate">
                    {t('reports.topApps')}
                  </span>
                  <span className="text-[10px] text-[rgba(245,247,251,0.35)] shrink-0">
                    {topApps.length} {t('reports.apps')}
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent className="px-4 pb-4 min-w-0">
                {loading ? (
                  <div className="space-y-2">
                    {Array.from({ length: 5 }).map((_, i) => (
                      <div
                        key={i}
                        className="h-11 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] animate-pulse"
                      />
                    ))}
                  </div>
                ) : topApps.length === 0 ? (
                  <div className="flex items-center justify-center h-24 text-[rgba(245,247,251,0.4)] text-[12px]">
                    {t('reports.noDataToShow')}
                  </div>
                  ) : (
                  <div className="space-y-2">
                    {topApps.slice(0, 8).map((app) => (
                      <div
                        key={app.displayName}
                        className="flex items-center justify-between gap-3 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] px-3 py-2"
                      >
                        <div className="min-w-0">
                          <div className="text-[12px] text-[rgba(245,247,251,0.85)] truncate">
                            {app.displayName}
                          </div>
                          <div className="text-[10px] text-[rgba(245,247,251,0.35)] truncate">
                            {app.subcategory ?? ''}
                          </div>
                        </div>
                        <div className="text-[11px] text-[rgba(245,247,251,0.55)] shrink-0">
                          {formatDuration(app.totalSeconds)}
                        </div>
                      </div>
                    ))}
                    <div data-demo-marker="reports-bottom" className="h-px w-full" />
                    <div data-demo-scroll-end className="h-px w-full" />
                  </div>
                )}
              </CardContent>
            </Card>
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
              {t('reports.title')}
            </h1>
            <p className="text-[12px] text-[rgba(245,247,251,0.45)] mt-0.5">
              {t('reports.subtitle')}
            </p>
          </div>
          <div className="hidden sm:flex items-center gap-2 text-[10px] text-[rgba(245,247,251,0.35)]">
            <span>{startDate}</span>
            <span>—</span>
            <span>{endDate}</span>
          </div>
        </div>

        <div className="mt-5 grid grid-cols-1 sm:grid-cols-3 gap-4 flex-shrink-0">
          <SummaryCard
            title={t('reports.activeTime')}
            value={formatDuration(summary.active)}
            subtitle={t('reports.total')}
            icon={Clock}
            badge={t('reports.last30')}
            badgeColor="blue"
            isLoading={loading}
          />
          <SummaryCard
            title={t('reports.idleTime')}
            value={formatDuration(summary.idle)}
            subtitle={t('reports.ofTotal')}
            icon={Activity}
            badge={t('reports.sources')}
            badgeColor="yellow"
            isLoading={loading}
          />
          <SummaryCard
            title={t('reports.focusScore')}
            value={`${summary.focus}`}
            subtitle={t('reports.focusScoreDesc')}
            icon={Target}
            badge={t('reports.avgPerDay')}
            badgeColor="green"
            isLoading={loading}
          />
        </div>

        <div className="mt-4 grid grid-cols-1 lg:grid-cols-[1fr_360px] gap-4 flex-1 min-h-0 overflow-hidden">
          <div className="min-h-0 overflow-hidden">
            <ProductivityTrend
              periods={bundle?.productivityTrend?.periods ?? []}
              isLoading={loading}
              maxHeight={220}
            />
          </div>

          <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl h-full flex flex-col overflow-hidden">
            <CardHeader className="pb-2 pt-3 px-4 shrink-0">
              <CardTitle className="flex items-center justify-between">
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
                  {t('reports.topApps')}
                </span>
                <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                  {topApps.length} apps
                </span>
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-2 pb-3 px-4 flex-1 overflow-hidden">
              {loading ? (
                <div className="flex items-center justify-center h-full">
                  <div className="w-6 h-6 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                </div>
              ) : topApps.length === 0 ? (
                <div className="flex items-center justify-center h-full text-[rgba(245,247,251,0.4)] text-[12px]">
                  {t('reports.noDataToShow')}
                </div>
              ) : (
                <div className="space-y-2 overflow-hidden">
                  {topApps.slice(0, 6).map((app) => (
                    <div
                      key={app.displayName}
                      className="flex items-center justify-between gap-3 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] px-3 py-2"
                    >
                      <div className="min-w-0">
                        <div className="text-[12px] text-[rgba(245,247,251,0.85)] truncate">
                          {app.displayName}
                        </div>
                        <div className="text-[10px] text-[rgba(245,247,251,0.35)] truncate">
                          {app.subcategory ?? ''}
                        </div>
                      </div>
                      <div className="text-[11px] text-[rgba(245,247,251,0.55)] shrink-0">
                        {formatDuration(app.totalSeconds)}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
