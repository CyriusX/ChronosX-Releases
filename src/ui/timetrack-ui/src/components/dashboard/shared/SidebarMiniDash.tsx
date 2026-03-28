/**
 * SidebarMiniDash - Compact at-a-glance metrics widget for sidebar bottom.
 *
 * Designed to fit within a 180px sidebar. Shows user info, daily progress,
 * key metrics in a tight 2-column grid, and top app.
 */

import { motion } from 'motion/react';
import { formatDuration } from '../../../lib/utils';

interface SidebarMiniDashProps {
  userName: string;
  userInitial: string;
  userRole: string;
  totalDuration: number;
  productiveTime: number;
  productivityScore: number;
  topAppName: string | null;
  topAppDuration: number;
  dailyGoalSeconds: number;
}

export function SidebarMiniDash({
  userName,
  userInitial,
  userRole,
  totalDuration,
  productiveTime,
  productivityScore,
  topAppName,
  topAppDuration: _topAppDuration,
  dailyGoalSeconds,
}: SidebarMiniDashProps) {
  const goalProgress = dailyGoalSeconds > 0
    ? Math.min((totalDuration / dailyGoalSeconds) * 100, 100)
    : 0;

  const productiveRatio = totalDuration > 0
    ? Math.round((productiveTime / totalDuration) * 100)
    : 0;

  const scoreColor =
    productivityScore >= 80 ? '#05df72'
    : productivityScore >= 60 ? '#4ade80'
    : productivityScore >= 40 ? '#fbbf24'
    : productivityScore >= 20 ? '#fb923c'
    : '#f87171';

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.3, duration: 0.3 }}
      className="px-2.5 pb-2.5 flex-shrink-0"
    >
      <div className="rounded-xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.05)] px-2.5 py-2.5 space-y-2">
        {/* User row */}
        <div className="flex items-center gap-2">
          <div className="w-6 h-6 rounded-full bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center text-white text-[10px] font-semibold flex-shrink-0">
            {userInitial}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-[10px] font-medium text-[#f5f7fb] truncate leading-tight">{userName}</p>
            <p className="text-[8px] text-[rgba(245,247,251,0.3)] leading-tight">{userRole}</p>
          </div>
        </div>

        {/* Daily goal progress */}
        <div>
          <div className="flex items-center justify-between mb-0.5">
            <span className="text-[8px] text-[rgba(245,247,251,0.45)] tabular-nums">{formatDuration(totalDuration)}</span>
            <span className="text-[8px] text-[rgba(245,247,251,0.25)] tabular-nums">Meta {formatDuration(dailyGoalSeconds)}</span>
          </div>
          <div className="h-1 rounded-full bg-[rgba(255,255,255,0.06)] overflow-hidden">
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${goalProgress}%` }}
              transition={{ duration: 0.8, ease: [0.4, 0, 0.2, 1] }}
              className="h-full rounded-full bg-gradient-to-r from-[#4ad9ff] to-[#3c7bff]"
            />
          </div>
        </div>

        {/* Metrics — 2x2 grid */}
        <div className="grid grid-cols-2 gap-1.5">
          <div className="rounded-md bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.04)] py-1 text-center">
            <p className="text-[12px] font-bold leading-none tabular-nums" style={{ color: scoreColor }}>
              {productivityScore}
            </p>
            <p className="text-[6px] text-[rgba(245,247,251,0.3)] mt-0.5 uppercase tracking-widest">Score</p>
          </div>
          <div className="rounded-md bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.04)] py-1 text-center">
            <p className="text-[12px] font-bold text-[#4ade80] leading-none tabular-nums">
              {productiveRatio}%
            </p>
            <p className="text-[6px] text-[rgba(245,247,251,0.3)] mt-0.5 uppercase tracking-widest">Produtivo</p>
          </div>
        </div>

        {/* Top app today */}
        {topAppName && (
          <div className="flex items-center gap-1.5">
            <div className="w-1 h-1 rounded-full bg-[#4ad9ff] flex-shrink-0" />
            <span className="text-[8px] text-[rgba(245,247,251,0.35)] truncate">
              Top: <span className="text-[rgba(245,247,251,0.55)] font-medium">{topAppName}</span>
            </span>
          </div>
        )}
      </div>
    </motion.div>
  );
}
