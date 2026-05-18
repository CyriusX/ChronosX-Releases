import { useCallback, useEffect, useState } from 'react';
import { motion } from 'motion/react';
import { Brain, TrendingUp, TrendingDown, Minus, Loader2, AlertCircle, RefreshCw } from 'lucide-react';
import { getLiveInsight, type LiveInsightResponse, type InsightItem } from '../../services/insightsApi';
import { ErrorBoundary } from '../ui/ErrorBoundary';

const POLL_INTERVAL = 60_000;

function TrendIcon({ trend }: { trend: string }) {
  if (trend === 'improving') return <TrendingUp className="w-3.5 h-3.5 text-[#22c55e]" />;
  if (trend === 'declining') return <TrendingDown className="w-3.5 h-3.5 text-[#ef4444]" />;
  return <Minus className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />;
}

function severityColor(severity: InsightItem['severity']) {
  switch (severity) {
    case 'positive': return '#22c55e';
    case 'negative': return '#ef4444';
    default: return '#eab308';
  }
}

export function AiInsightCard() {
  const [insight, setInsight] = useState<LiveInsightResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);

  const fetchInsight = useCallback(async () => {
    try {
      const response = await getLiveInsight();
      setInsight(response);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchInsight();
    const interval = setInterval(fetchInsight, POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [fetchInsight]);

  if (isLoading) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.15)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
          <span className="text-[12px] text-[rgba(245,247,251,0.4)]">Analyzing your day...</span>
        </div>
        <div className="space-y-2">
          <div className="h-3 bg-[rgba(255,255,255,0.04)] rounded w-full animate-pulse" />
          <div className="h-3 bg-[rgba(255,255,255,0.04)] rounded w-4/5 animate-pulse" />
          <div className="h-3 bg-[rgba(255,255,255,0.04)] rounded w-3/5 animate-pulse" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(239,68,68,0.12)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-6 h-6 rounded-lg bg-[rgba(239,68,68,0.15)] flex items-center justify-center">
            <AlertCircle className="w-3.5 h-3.5 text-[#ef4444]" />
          </div>
          <span className="text-[13px] font-medium text-[#f5f7fb]">AI Insight</span>
        </div>
        <p className="text-[12px] text-[rgba(245,247,251,0.45)] mb-3">Unable to load live insights.</p>
        <button
          onClick={() => { setError(false); setIsLoading(true); fetchInsight(); }}
          className="flex items-center gap-1.5 text-[12px] text-[#8B5CF6] hover:text-[#a78bfa] transition-colors"
        >
          <RefreshCw className="w-3 h-3" />
          Retry
        </button>
      </div>
    );
  }

  if (!insight || !insight.hasData || insight.insights.length === 0) return null;

  return (
    <ErrorBoundary>
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.15)] p-5"
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
            <Brain className="w-3.5 h-3.5 text-[#8B5CF6]" />
          </div>
          <span className="text-[13px] font-medium text-[#f5f7fb]">
            AI Insight
          </span>
        </div>

        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1 text-[11px] text-[rgba(245,247,251,0.45)]">
            <TrendIcon trend={insight.trend} />
            <span>{insight.trend === 'improving' ? 'Improving' : insight.trend === 'declining' ? 'Declining' : 'Stable'}</span>
          </div>
          {insight.latestAlert && (
            <span
              className="w-2 h-2 rounded-full animate-pulse"
              style={{ backgroundColor: insight.latestAlert.severity === 'warning' ? '#eab308' : '#22c55e' }}
            />
          )}
        </div>
      </div>

      <div className="space-y-2.5">
        {insight.insights.map((item, i) => (
          <motion.div
            key={i}
            initial={{ opacity: 0, x: -8 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: i * 0.08 }}
            className="flex items-start gap-2.5"
          >
            <div
              className="w-1.5 h-1.5 rounded-full mt-[7px] flex-shrink-0"
              style={{ backgroundColor: severityColor(item.severity) }}
            />
            <p className="text-[12px] text-[rgba(245,247,251,0.65)] leading-relaxed">
              {item.message}
            </p>
          </motion.div>
        ))}
      </div>
    </motion.div>
    </ErrorBoundary>
  );
}
