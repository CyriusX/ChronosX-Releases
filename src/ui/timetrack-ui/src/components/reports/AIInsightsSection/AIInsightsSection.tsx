import { useTranslation } from 'react-i18next';
import {
  Brain, Lightbulb, AlertCircle, TrendingUp,
  TrendingDown, Minus, CheckCircle2, Users, Shield,
  RefreshCw,
} from 'lucide-react';
import { ErrorBoundary } from '../../ui/ErrorBoundary';
import type { ReportsInsightsResponse } from '../../../services/insightsApi';

export interface AIInsightsSectionProps {
  data: ReportsInsightsResponse | null;
  isLoading?: boolean;
  error?: boolean;
  onRetry?: () => void;
}

const trendConfig = {
  improving: { icon: TrendingUp, color: '#05df72', label: 'improving' },
  declining: { icon: TrendingDown, color: '#f87171', label: 'declining' },
  stable: { icon: Minus, color: '#fbbf24', label: 'stable' },
};

const patternLabels: Record<string, string> = {
  morning_productive: 'Manha Produtiva',
  afternoon_focus_drop: 'Queda de Foco a Tarde',
  high_context_switching: 'Trocas de Contexto',
  deep_work_capable: 'Foco Profundo',
  high_distraction_risk: 'Risco de Distração',
};

export function AIInsightsSection({ data, isLoading, error, onRetry }: AIInsightsSectionProps) {
  const { t } = useTranslation();

  if (error) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(239,68,68,0.12)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-6 h-6 rounded-lg bg-[rgba(239,68,68,0.15)] flex items-center justify-center">
            <AlertCircle className="w-3.5 h-3.5 text-[#ef4444]" />
          </div>
          <span className="text-[13px] font-medium text-[#f5f7fb]">{t('reports.aiInsights')}</span>
        </div>
        <p className="text-[12px] text-[rgba(245,247,251,0.45)] mb-3">{t('reports.aiNoInsights')}</p>
        {onRetry && (
          <button
            onClick={onRetry}
            className="flex items-center gap-1.5 text-[12px] text-[#8B5CF6] hover:text-[#a78bfa] transition-colors"
          >
            <RefreshCw className="w-3 h-3" />
            Retry
          </button>
        )}
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.12)] p-5">
        <div className="flex items-center gap-2 mb-4">
          <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
            <Brain className="w-3.5 h-3.5 text-[#8B5CF6] animate-pulse" />
          </div>
          <span className="text-[13px] font-medium text-[#f5f7fb]">{t('reports.aiInsights')}</span>
        </div>
        <div className="flex items-center justify-center py-8">
          <div className="w-6 h-6 border-2 border-[rgba(139,92,246,0.4)] border-t-[#8B5CF6] rounded-full animate-spin" />
        </div>
      </div>
    );
  }

  if (!data || !data.hasData) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
            <Brain className="w-3.5 h-3.5 text-[#8B5CF6]" />
          </div>
          <span className="text-[13px] font-medium text-[#f5f7fb]">{t('reports.aiInsights')}</span>
        </div>
        <p className="text-[12px] text-[rgba(245,247,251,0.4)] text-center py-4">{t('reports.aiNoInsights')}</p>
      </div>
    );
  }

  if (data.isTeamView) {
    return <ErrorBoundary><TeamView data={data} /></ErrorBoundary>;
  }

  return <ErrorBoundary><IndividualView data={data} /></ErrorBoundary>;
}

function IndividualView({ data }: { data: ReportsInsightsResponse }) {
  const { t } = useTranslation();
  const trend = data.comparison?.trend ?? data.summary?.trend ?? 'stable';
  const tc = trendConfig[trend as keyof typeof trendConfig] ?? trendConfig.stable;
  const TrendIcon = tc.icon;

  return (
    <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.12)] p-5">
      {/* Header */}
      <div className="flex items-center gap-2 mb-4">
        <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <Brain className="w-3.5 h-3.5 text-[#8B5CF6]" />
        </div>
        <span className="text-[13px] font-medium text-[#f5f7fb]">{t('reports.aiInsights')}</span>
        <div className="flex-1" />
        {data.focusScore != null && (
          <div className="flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)]">
            <TrendIcon className="w-3 h-3" style={{ color: tc.color }} />
            <span className="text-[10px] font-medium" style={{ color: tc.color }}>
              {t(`reports.ai${trend.charAt(0).toUpperCase() + trend.slice(1)}`)}
            </span>
          </div>
        )}
      </div>

      {/* Top Apps & Distractions */}
      <div className="grid grid-cols-2 gap-3 mb-4">
        {data.topApps && data.topApps.length > 0 && (
          <div>
            <h4 className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-1.5">Apps mais usados</h4>
            <div className="space-y-1">
              {data.topApps.slice(0, 4).map((app, i) => (
                <div key={i} className="flex items-center gap-1.5">
                  <span className="text-[9px] font-medium text-[rgba(139,92,246,0.6)] w-3">{i + 1}</span>
                  <span className="text-[11px] text-[rgba(245,247,251,0.7)] truncate">{app}</span>
                </div>
              ))}
            </div>
          </div>
        )}
        {data.topDistractions && data.topDistractions.length > 0 && (
          <div>
            <h4 className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-1.5">Distrações</h4>
            <div className="space-y-1">
              {data.topDistractions.slice(0, 4).map((app, i) => (
                <div key={i} className="flex items-center gap-1.5">
                  <span className="text-[9px] font-medium text-[rgba(248,113,113,0.6)] w-3">{i + 1}</span>
                  <span className="text-[11px] text-[rgba(245,247,251,0.6)] truncate">{app}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Suggestion */}
      {data.suggestion && (
        <div className="flex items-start gap-2.5 px-3 py-2.5 rounded-xl bg-[rgba(139,92,246,0.06)] border border-[rgba(139,92,246,0.15)] mb-4">
          <Lightbulb className="w-4 h-4 text-[#fbbf24] flex-shrink-0 mt-0.5" />
          <div>
            <span className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)]">{t('reports.aiSuggestion')}</span>
            <p className="text-[11px] text-[rgba(245,247,251,0.7)] mt-0.5 leading-relaxed">{data.suggestion}</p>
          </div>
        </div>
      )}

      {/* Bottom Grid: Patterns + Attention + Benchmark */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Active Patterns */}
        <div>
          <h4 className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-2">{t('reports.aiActivePatterns')}</h4>
          {data.patterns && data.patterns.length > 0 ? (
            <div className="space-y-1.5">
              {data.patterns.map((p, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span className="text-[11px] text-[rgba(245,247,251,0.7)] flex-1 truncate">
                    {patternLabels[p.patternTag] ?? p.patternTag}
                  </span>
                  <div className="w-16 h-1.5 rounded-full bg-[rgba(255,255,255,0.06)] overflow-hidden">
                    <div
                      className="h-full rounded-full bg-[#8B5CF6]"
                      style={{ width: `${Math.round(p.strength * 100)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-[11px] text-[rgba(245,247,251,0.3)] italic">{t('reports.aiNoPatterns')}</p>
          )}
        </div>

        {/* Attention + Benchmark */}
        <div className="space-y-3">
          {/* Attention Points */}
          <div>
            <h4 className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-2">{t('reports.aiAttentionPoints')}</h4>
            {data.anomalies && data.anomalies.length > 0 ? (
              <div className="space-y-1">
                {data.anomalies.slice(0, 3).map((a, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <AlertCircle className="w-3 h-3" style={{ color: a.severity === 'high' ? '#f87171' : '#fbbf24' }} />
                    <span className="text-[11px] text-[rgba(245,247,251,0.6)] truncate">{a.anomalyType.replace(/_/g, ' ')}</span>
                  </div>
                ))}
              </div>
            ) : data.alerts && data.alerts.length > 0 ? (
              <div className="space-y-1">
                {data.alerts.slice(0, 3).map((a) => (
                  <div key={a.id} className="flex items-center gap-2">
                    <AlertCircle className="w-3 h-3" style={{ color: a.severity === 'alert' ? '#f87171' : '#fbbf24' }} />
                    <span className="text-[11px] text-[rgba(245,247,251,0.6)] truncate">{a.message}</span>
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <CheckCircle2 className="w-3 h-3 text-[#05df72]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.4)]">{t('reports.aiNoAlerts')}</span>
              </div>
            )}
          </div>

          {/* Benchmark */}
          {data.benchmark && data.benchmark.hasData && (
            <div>
              <h4 className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-2">{t('reports.aiBenchmark')}</h4>
              <div className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] text-[rgba(245,247,251,0.6)]">{t('reports.aiYourFocus')}</span>
                  <span className="text-[12px] font-semibold text-[#f5f7fb]">{data.benchmark.user.avgFocusScore}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-[11px] text-[rgba(245,247,251,0.6)]">{t('reports.aiTeamAvgFocus')}</span>
                  <span className="text-[12px] font-semibold text-[rgba(245,247,251,0.6)]">{data.benchmark.team.avgFocusScore}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <Shield className="w-3 h-3" style={{ color: data.benchmark.focusDiff >= 0 ? '#05df72' : '#f87171' }} />
                  <span className="text-[11px] font-medium" style={{ color: data.benchmark.focusDiff >= 0 ? '#05df72' : '#f87171' }}>
                    {Math.abs(data.benchmark.focusDiff)} {data.benchmark.focusDiff >= 0 ? t('reports.aiAboveAvg') : t('reports.aiBelowAvg')}
                  </span>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function TeamView({ data }: { data: ReportsInsightsResponse }) {
  const { t } = useTranslation();
  const summary = data.teamSummary;

  if (!summary) return null;

  return (
    <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.12)] p-5">
      <div className="flex items-center gap-2 mb-4">
        <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <Users className="w-3.5 h-3.5 text-[#8B5CF6]" />
        </div>
        <span className="text-[13px] font-medium text-[#f5f7fb]">{t('reports.aiTeamSummary')}</span>
      </div>

      <div className="grid grid-cols-3 gap-3 mb-4">
        <div className="text-center px-3 py-2 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          <div className="text-[14px] font-semibold text-[#f5f7fb]">{summary.avgFocusScore}</div>
          <div className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase">{t('reports.aiFocusTrend')}</div>
        </div>
        <div className="text-center px-3 py-2 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          <div className="text-[14px] font-semibold text-[#f5f7fb]">{summary.memberCount}</div>
          <div className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase">{t('reports.aiTeamMembersAnalyzed')}</div>
        </div>
        <div className="text-center px-3 py-2 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          <div className="text-[14px] font-semibold text-[#f87171]">{summary.totalAlerts}</div>
          <div className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase">{t('reports.aiTeamAlerts')}</div>
        </div>
      </div>

      {summary.topPatterns.length > 0 && (
        <div>
          <h4 className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-2">{t('reports.aiActivePatterns')}</h4>
          <div className="space-y-1.5">
            {summary.topPatterns.map((p, i) => (
              <div key={i} className="flex items-center gap-2">
                <span className="text-[11px] text-[rgba(245,247,251,0.7)] flex-1 truncate">
                  {patternLabels[p.patternTag] ?? p.patternTag}
                </span>
                <div className="w-16 h-1.5 rounded-full bg-[rgba(255,255,255,0.06)] overflow-hidden">
                  <div
                    className="h-full rounded-full bg-[#8B5CF6]"
                    style={{ width: `${Math.round(p.strength * 100)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
