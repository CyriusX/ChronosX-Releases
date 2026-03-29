/**
 * ProductivityTrend - Stacked bar chart for productivity over time
 *
 * SRP: Apenas exibe gráfico de tendência de produtividade
 * OCP: Extensível via props
 * DIP: Recebe dados via props
 *
 * Composition: Composto por TrendBar
 */

import { useMemo } from 'react';
import { motion } from 'motion/react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { fadeUp, staggerContainer, STAGGER } from '../../../lib/animation';
import type { ProductivityTrendPeriodItem } from '../../../types/reports';
export interface ProductivityTrendProps {
  /** Trend data */
  periods: ProductivityTrendPeriodItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Title */
  title?: string;
  /** Max height in pixels */
  maxHeight?: number;
}

interface TrendBarProps {
  period: string;
  productive: number;
  neutral: number;
  distraction: number;
  idle: number;
  maxSeconds: number;
}

function formatHours(seconds: number): string {
  if (seconds === 0) return '0h';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

function TrendBar({ period, productive, neutral, distraction, idle, maxSeconds }: TrendBarProps) {
  const total = productive + neutral + distraction + idle;
  const scale = maxSeconds > 0 ? 100 / maxSeconds : 0;

  const productiveWidth = productive * scale;
  const neutralWidth = neutral * scale;
  const distractionWidth = distraction * scale;
  const idleWidth = idle * scale;

  return (
    <div className="flex items-center gap-2 min-w-0">
      <span className="text-[10px] text-[rgba(245,247,251,0.4)] w-15 text-right shrink-0 truncate">
        {period}
      </span>
      <div className="flex-1 h-4 bg-[rgba(255,255,255,0.03)] rounded overflow-hidden flex min-w-25">
        {productive > 0 && (
          <motion.div
            className="h-full bg-[rgba(5,223,114,0.7)]"
            initial={{ width: 0 }}
            animate={{ width: `${productiveWidth}%` }}
            transition={{ duration: 0.6, ease: 'easeOut' }}
            title={`Produtivo: ${formatHours(productive)}`}
          />
        )}
        {neutral > 0 && (
          <motion.div
            className="h-full bg-[rgba(139,92,246,0.6)]"
            initial={{ width: 0 }}
            animate={{ width: `${neutralWidth}%` }}
            transition={{ duration: 0.6, ease: 'easeOut', delay: 0.1 }}
            title={`Neutro: ${formatHours(neutral)}`}
          />
        )}
        {distraction > 0 && (
          <motion.div
            className="h-full bg-[rgba(251,191,36,0.6)]"
            initial={{ width: 0 }}
            animate={{ width: `${distractionWidth}%` }}
            transition={{ duration: 0.6, ease: 'easeOut', delay: 0.15 }}
            title={`Distração: ${formatHours(distraction)}`}
          />
        )}
        {idle > 0 && (
          <motion.div
            className="h-full bg-[rgba(255,255,255,0.1)]"
            initial={{ width: 0 }}
            animate={{ width: `${idleWidth}%` }}
            transition={{ duration: 0.6, ease: 'easeOut', delay: 0.2 }}
            title={`Idle: ${formatHours(idle)}`}
          />
        )}
      </div>
      <span className="text-[10px] text-[rgba(245,247,251,0.5)] w-10 text-right shrink-0">
        {formatHours(total)}
      </span>
    </div>
  );
}

export function ProductivityTrend({
  periods,
  isLoading = false,
  title = 'Tendência de Produtividade',
  maxHeight = 280,
}: ProductivityTrendProps) {
  const maxSeconds = useMemo(() => {
    if (periods.length === 0) return 28800;
    return Math.max(
      ...periods.map(p => p.productiveSeconds + p.neutralSeconds + p.distractionSeconds + p.idleSeconds),
      3600
    );
  }, [periods]);

  // Calculate totals for summary
  const summary = useMemo(() => {
    if (periods.length === 0) return null;

    const totals = periods.reduce(
      (acc, p) => ({
        productive: acc.productive + p.productiveSeconds,
        neutral: acc.neutral + p.neutralSeconds,
        distraction: acc.distraction + p.distractionSeconds,
        idle: acc.idle + p.idleSeconds,
      }),
      { productive: 0, neutral: 0, distraction: 0, idle: 0 }
    );

    const total = totals.productive + totals.neutral + totals.distraction + totals.idle;

    return {
      ...totals,
      total,
      productivePercent: total > 0 ? (totals.productive / total) * 100 : 0,
      neutralPercent: total > 0 ? (totals.neutral / total) * 100 : 0,
      distractionPercent: total > 0 ? (totals.distraction / total) * 100 : 0,
    };
  }, [periods]);

  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
        <CardHeader className="pb-2 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="flex items-center justify-center h-[200px]">
            <div className="w-6 h-6 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl h-full flex flex-col">
      <CardHeader className="pb-2 pt-3 px-4 shrink-0">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
          {summary && (
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-1">
                <div className="w-2 h-2 rounded-sm bg-[rgba(5,223,114,0.7)]" />
                <span className="text-[9px] text-[rgba(245,247,251,0.4)]">
                  {Math.round(summary.productivePercent)}%
                </span>
              </div>
              <div className="flex items-center gap-1">
                <div className="w-2 h-2 rounded-sm bg-[rgba(139,92,246,0.6)]" />
                <span className="text-[9px] text-[rgba(245,247,251,0.4)]">
                  {Math.round(summary.neutralPercent)}%
                </span>
              </div>
              <div className="flex items-center gap-1">
                <div className="w-2 h-2 rounded-sm bg-[rgba(251,191,36,0.6)]" />
                <span className="text-[9px] text-[rgba(245,247,251,0.4)]">
                  {Math.round(summary.distractionPercent)}%
                </span>
              </div>
            </div>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4 flex-1 overflow-hidden">
        {periods.length === 0 ? (
          <div className="flex items-center justify-center h-[120px] text-[rgba(245,247,251,0.4)] text-[12px]">
            Sem dados para o período
          </div>
        ) : (
          <motion.div
            className="space-y-1 overflow-y-auto"
            style={{ maxHeight }}
            variants={staggerContainer(STAGGER.listItems)}
            initial="hidden"
            animate="visible"
          >
            {periods.map((period, index) => (
              <motion.div key={`${period.period}-${index}`} variants={fadeUp}>
                <TrendBar
                  period={period.period}
                  productive={period.productiveSeconds}
                  neutral={period.neutralSeconds}
                  distraction={period.distractionSeconds}
                  idle={period.idleSeconds}
                  maxSeconds={maxSeconds}
                />
              </motion.div>
            ))}
          </motion.div>
        )}
      </CardContent>
    </Card>
  );
}
