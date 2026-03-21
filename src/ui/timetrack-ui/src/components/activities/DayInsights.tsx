/**
 * DayInsights — Row of insight pills comparing the selected day to averages
 */

import { TrendingUp, TrendingDown, Clock, Zap } from 'lucide-react';
import type { TodaySummaryResponse } from '../../types/ipc';

interface DayInsightsProps {
  summary: TodaySummaryResponse | null;
  comparisonText: string;
  productivityComparison: string;
  isToday: boolean;
}

export function DayInsights({
  summary,
  comparisonText,
  productivityComparison,
  isToday,
}: DayInsightsProps) {
  if (!summary) return null;

  const isAboveAverage = comparisonText.startsWith('+');
  const sessionsCount = summary.sessionsCount;

  return (
    <div className="flex gap-3 flex-wrap">
      {/* Tracked time vs average */}
      {comparisonText && (
        <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          {isAboveAverage ? (
            <TrendingUp className="w-3.5 h-3.5 text-[#4ade80]" />
          ) : (
            <TrendingDown className="w-3.5 h-3.5 text-[#f87171]" />
          )}
          <span className="text-[11px] text-[rgba(245,247,251,0.7)]">
            {comparisonText}
          </span>
        </div>
      )}

      {/* Productivity percentage */}
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
        <Zap className="w-3.5 h-3.5 text-[#fbbf24]" />
        <span className="text-[11px] text-[rgba(245,247,251,0.7)]">
          Produtividade: <span className="text-[rgba(245,247,251,0.9)] font-medium">{productivityComparison}</span>
        </span>
      </div>

      {/* Sessions count */}
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
        <Clock className="w-3.5 h-3.5 text-[#4ad9ff]" />
        <span className="text-[11px] text-[rgba(245,247,251,0.7)]">
          {sessionsCount} {sessionsCount === 1 ? 'sessão' : 'sessões'}
          {isToday && (
            <span className="ml-1.5 text-[9px] text-[#05df72] bg-[rgba(5,223,114,0.15)] px-1.5 py-0.5 rounded-full">
              ao vivo
            </span>
          )}
        </span>
      </div>
    </div>
  );
}
