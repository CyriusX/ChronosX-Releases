import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import {
  Brain,
  TrendingUp,
  TrendingDown,
  Users,
  BookOpen,
  Sparkles,
  Activity,
} from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import {
  getPatterns,
  getFocusTrend,
  getBenchmark,
  getNarrativesHistory,
  type PatternItem,
  type WeeklyTrendItem,
  type BenchmarkResponse,
  type NarrativeHistoryItem,
} from '../services/insightsApi';

const PATTERN_ICONS: Record<string, string> = {
  morning_productive: '🌅',
  afternoon_focus_drop: '📉',
  high_context_switching: '🔄',
  deep_work_capable: '🎯',
  high_distraction_risk: '⚠️',
};

const PATTERN_LABELS: Record<string, string> = {
  morning_productive: 'Morning Productive',
  afternoon_focus_drop: 'Afternoon Focus Drop',
  high_context_switching: 'High Context Switching',
  deep_work_capable: 'Deep Work Capable',
  high_distraction_risk: 'High Distraction Risk',
};

function PatternSection({ patterns }: { patterns: PatternItem[] }) {
  const { t } = useTranslation();

  if (patterns.length === 0) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <Brain className="w-4 h-4 text-[#8B5CF6]" />
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">
          {t('settings.insights.patterns', 'Your Active Patterns')}
        </h3>
      </div>
      <div className="grid gap-3">
        {patterns.map((p) => (
          <motion.div
            key={p.id}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            className="bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4"
          >
            <div className="flex items-center justify-between mb-2">
              <div className="flex items-center gap-2">
                <span className="text-[16px]">{PATTERN_ICONS[p.patternTag] ?? '📊'}</span>
                <span className="text-[13px] font-medium text-[#f5f7fb]">
                  {PATTERN_LABELS[p.patternTag] ?? p.patternTag}
                </span>
              </div>
              <span className="text-[11px] font-mono text-[#8B5CF6]">
                {Math.round(p.strength * 100)}%
              </span>
            </div>
            <div className="w-full h-1.5 bg-[rgba(255,255,255,0.06)] rounded-full overflow-hidden mb-2">
              <div
                className="h-full bg-[#8B5CF6] rounded-full transition-all"
                style={{ width: `${Math.round(p.strength * 100)}%` }}
              />
            </div>
            {p.description && (
              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{p.description}</p>
            )}
          </motion.div>
        ))}
      </div>
    </div>
  );
}

function FocusTrendSection({ data }: { data: WeeklyTrendItem[] }) {
  const { t } = useTranslation();

  if (data.length === 0) return null;

  const chartData = data.map((d) => ({
    week: d.weekStart,
    focus: Math.round(d.avgFocusScore),
    ratio: Math.round(d.avgProductiveRatio * 100),
  }));

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <Activity className="w-4 h-4 text-[#8B5CF6]" />
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">
          {t('settings.insights.focusTrend', 'Focus Trend')}
        </h3>
      </div>
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4">
        <div className="h-[200px]">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.04)" />
              <XAxis
                dataKey="week"
                stroke="rgba(245,247,251,0.3)"
                tick={{ fontSize: 10 }}
                tickFormatter={(v: string) => v.slice(5)}
              />
              <YAxis
                stroke="rgba(245,247,251,0.3)"
                tick={{ fontSize: 10 }}
                domain={[0, 100]}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: '#1a1d2e',
                  border: '1px solid rgba(255,255,255,0.08)',
                  borderRadius: '8px',
                  fontSize: '12px',
                  color: '#f5f7fb',
                }}
              />
              <Line
                type="monotone"
                dataKey="focus"
                stroke="#8B5CF6"
                strokeWidth={2}
                dot={{ r: 3, fill: '#8B5CF6' }}
                name="Focus Score"
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}

function BenchmarkSection({ benchmark }: { benchmark: BenchmarkResponse | null }) {
  const { t } = useTranslation();

  if (!benchmark?.hasData || !benchmark.user || !benchmark.team) return null;

  const diff = benchmark.focusDiff ?? 0;
  const diffPercent = benchmark.focusDiffPercent ?? 0;
  const isAbove = diff >= 0;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <Users className="w-4 h-4 text-[#8B5CF6]" />
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">
          {t('settings.insights.benchmark', 'Team Benchmark')}
        </h3>
      </div>
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4">
        <div className="flex items-center gap-4">
          <div className="flex-1 text-center">
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-1">You</p>
            <p className="text-[24px] font-bold text-[#f5f7fb]">
              {Math.round(benchmark.user.avgFocusScore)}
            </p>
            <p className="text-[10px] text-[rgba(245,247,251,0.3)]">avg focus score</p>
          </div>

          <div className="flex flex-col items-center gap-1">
            {isAbove ? (
              <TrendingUp className="w-5 h-5 text-[#22c55e]" />
            ) : (
              <TrendingDown className="w-5 h-5 text-[#ef4444]" />
            )}
            <span className={`text-[14px] font-bold ${isAbove ? 'text-[#22c55e]' : 'text-[#ef4444]'}`}>
              {isAbove ? '+' : ''}{Math.round(diffPercent)}%
            </span>
          </div>

          <div className="flex-1 text-center">
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-1">Team avg</p>
            <p className="text-[24px] font-bold text-[rgba(245,247,251,0.5)]">
              {Math.round(benchmark.team.avgFocusScore)}
            </p>
            <p className="text-[10px] text-[rgba(245,247,251,0.3)]">
              {benchmark.team.userCount} members
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function NarrativeHistorySection({ narratives }: { narratives: NarrativeHistoryItem[] }) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState<string | null>(null);

  if (narratives.length === 0) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <BookOpen className="w-4 h-4 text-[#8B5CF6]" />
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">
          {t('settings.insights.narrativeHistory', 'Weekly Summaries History')}
        </h3>
      </div>
      <div className="space-y-2">
        {narratives.map((n) => (
          <button
            key={n.id}
            onClick={() => setExpanded(expanded === n.id ? null : n.id)}
            className="w-full text-left bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4 hover:border-[rgba(139,92,246,0.2)] transition-colors"
          >
            <div className="flex items-center justify-between mb-1">
              <span className="text-[12px] font-medium text-[#f5f7fb]">
                {new Date(n.createdAt).toLocaleDateString()}
              </span>
              {n.reviewOutcome && (
                <span className={`text-[10px] px-2 py-0.5 rounded-md ${
                  n.reviewOutcome === 'useful' ? 'bg-[rgba(34,197,94,0.1)] text-[#22c55e]' :
                  n.reviewOutcome === 'not_useful' ? 'bg-[rgba(239,68,68,0.1)] text-[#ef4444]' :
                  'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)]'
                }`}>
                  {n.reviewOutcome}
                </span>
              )}
            </div>
            {expanded === n.id && n.narrative ? (
              <p className="text-[12px] text-[rgba(245,247,251,0.6)] leading-relaxed mt-2">
                {n.narrative}
              </p>
            ) : (
              <p className="text-[11px] text-[rgba(245,247,251,0.3)] truncate">
                {n.narrative ?? 'No content'}
              </p>
            )}
          </button>
        ))}
      </div>
    </div>
  );
}

export default function Insights() {
  const { t } = useTranslation();
  const [patterns, setPatterns] = useState<PatternItem[]>([]);
  const [trend, setTrend] = useState<WeeklyTrendItem[]>([]);
  const [benchmark, setBenchmark] = useState<BenchmarkResponse | null>(null);
  const [narratives, setNarratives] = useState<NarrativeHistoryItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchAll = useCallback(async () => {
    setIsLoading(true);
    try {
      const [p, tr, bm, nh] = await Promise.allSettled([
        getPatterns(),
        getFocusTrend(),
        getBenchmark(),
        getNarrativesHistory(),
      ]);

      if (p.status === 'fulfilled') setPatterns(p.value);
      if (tr.status === 'fulfilled') setTrend(tr.value);
      if (bm.status === 'fulfilled') setBenchmark(bm.value);
      if (nh.status === 'fulfilled') setNarratives(nh.value);
    } catch {
      // handled by settled
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const hasData = patterns.length > 0 || trend.length > 0;

  return (
    <div className="flex h-screen bg-transparent overflow-hidden pb-14 md:pb-0">
      {/* Sidebar placeholder — reuses dashboard layout */}
      <div className="hidden md:flex w-[220px] flex-shrink-0 border-r border-[rgba(255,255,255,0.04)] bg-gradient-to-b from-[rgba(11,13,20,0.3)] to-[rgba(17,19,28,0.3)]" />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
              <Sparkles className="w-5 h-5 text-[#8B5CF6]" />
            </div>
            <div>
              <h1 className="text-[20px] font-semibold text-[#f5f7fb]">
                {t('settings.insights.title', 'Insights')}
              </h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                {t('settings.insights.subtitle', 'Your productivity patterns and trends')}
              </p>
            </div>
          </div>
        </div>

        <div className="flex-1 overflow-y-auto px-5 pb-4">
          {isLoading ? (
            <div className="space-y-4 mt-4">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-[120px] rounded-xl bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] animate-pulse" />
              ))}
            </div>
          ) : !hasData ? (
            <div className="text-center py-16">
              <Brain className="w-12 h-12 text-[rgba(245,247,251,0.15)] mx-auto mb-4" />
              <p className="text-[14px] text-[rgba(245,247,251,0.4)]">
                {t('settings.insights.noData', 'Not enough data yet')}
              </p>
              <p className="text-[12px] text-[rgba(245,247,251,0.25)] mt-1">
                Insights will appear after 2 weeks of tracking
              </p>
            </div>
          ) : (
            <div className="space-y-6 mt-4">
              <PatternSection patterns={patterns} />
              <FocusTrendSection data={trend} />
              <BenchmarkSection benchmark={benchmark} />
              <NarrativeHistorySection narratives={narratives} />
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
