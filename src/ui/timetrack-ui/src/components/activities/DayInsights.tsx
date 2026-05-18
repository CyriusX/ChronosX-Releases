/**
 * DayInsights — Row of insight pills comparing the selected day to averages
 */

import { motion } from 'motion/react';
import { TrendingUp, TrendingDown, Zap } from 'lucide-react';
import { scaleIn, staggerContainer, STAGGER } from '../../lib/animation';
import type { TodaySummaryResponse } from '../../types/ipc';

interface DayInsightsProps {
  summary: TodaySummaryResponse | null;
  comparisonText: string;
  productivityComparison: string;
  isToday: boolean;
  /**
   * Whether the displayed user is *actively* tracking right now. Drives the "Ao vivo"
   * badge. Defaults to false so the badge never shows a false positive — callers must
   * opt in by passing the live signal sourced from heartbeat / local tracking state.
   */
  isLive?: boolean;
}

export function DayInsights({
  summary,
  comparisonText,
  productivityComparison,
  isToday,
  isLive = false,
}: DayInsightsProps) {
  if (!summary) return null;

  const isAboveAverage = comparisonText.startsWith('+');

  return (
    <motion.div
      className="flex gap-3 flex-wrap"
      variants={staggerContainer(STAGGER.pills)}
      initial="hidden"
      animate="visible"
    >
      {/* Tracked time vs average */}
      {comparisonText && (
        <motion.div
          className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]"
          variants={scaleIn}
        >
          {isAboveAverage ? (
            <TrendingUp className="w-3.5 h-3.5 text-[#4ade80]" />
          ) : (
            <TrendingDown className="w-3.5 h-3.5 text-[#f87171]" />
          )}
          <span className="text-[11px] text-[rgba(245,247,251,0.7)]">
            {comparisonText}
          </span>
        </motion.div>
      )}

      {/* Productivity percentage */}
      <motion.div
        className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]"
        variants={scaleIn}
      >
        <Zap className="w-3.5 h-3.5 text-[#fbbf24]" />
        <span className="text-[11px] text-[rgba(245,247,251,0.7)]">
          Produtividade: <span className="text-[rgba(245,247,251,0.9)] font-medium">{productivityComparison}</span>
        </span>
      </motion.div>

      {/* Live indicator — only when the displayed user is actively tracking today */}
      {isToday && isLive && (
        <motion.div
          className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(5,223,114,0.05)] border border-[rgba(5,223,114,0.15)]"
          variants={scaleIn}
        >
          <div className="w-2 h-2 rounded-full bg-[#05df72] animate-pulse" />
          <span className="text-[11px] text-[#05df72] font-medium">Ao vivo</span>
        </motion.div>
      )}
    </motion.div>
  );
}
