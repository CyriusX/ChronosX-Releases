/**
 * FocusCountdown - Countdown display with progress bar
 *
 * CX-139: Timer Focus Card
 */

import { Progress } from '../../ui/progress';
import type { FocusModeType } from '../../../types/ipc';
import { formatRemaining } from './formatRemaining';

interface FocusCountdownProps {
  remainingMs: number;
  plannedMs: number;
  mode: FocusModeType;
}

export function FocusCountdown({
  remainingMs,
  plannedMs,
  mode,
}: FocusCountdownProps) {
  const display = formatRemaining(remainingMs, mode);
  const progress = plannedMs > 0 ? Math.max(0, Math.min(100, ((plannedMs - remainingMs) / plannedMs) * 100)) : 0;

  return (
    <div className="flex flex-col items-center">
      {/* Countdown display */}
      <div className="mb-4">
        <div className="text-[32px] font-semibold text-[#f5f7fb] tracking-wide font-mono">
          {display}
        </div>
        <div className="text-[11px] text-[rgba(245,247,251,0.4)] text-center mt-1">
          restantes
        </div>
      </div>

      {/* Progress bar */}
      <div className="w-full">
        <Progress value={progress} className="h-1.5" />
      </div>
    </div>
  );
}

/**
 * BreakCountdown - Countdown for break mode
 */
interface BreakCountdownProps {
  remainingMs: number;
  plannedMs: number;
  breakType: 'Short' | 'Long';
}

export function BreakCountdown({
  remainingMs,
  plannedMs,
  breakType,
}: BreakCountdownProps) {
  // Always show MM:SS for breaks
  const totalSec = Math.max(0, Math.ceil(remainingMs / 1000));
  const m = Math.floor(totalSec / 60);
  const s = totalSec % 60;
  const display = `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;

  const progress = plannedMs > 0 ? Math.max(0, Math.min(100, ((plannedMs - remainingMs) / plannedMs) * 100)) : 0;
  const label = breakType === 'Long' ? 'de descanso longo' : 'de descanso';

  return (
    <div className="flex flex-col items-center">
      {/* Countdown display */}
      <div className="mb-4">
        <div className="text-[32px] font-semibold text-[#f5f7fb] tracking-wide font-mono">
          {display}
        </div>
        <div className="text-[11px] text-[rgba(245,247,251,0.4)] text-center mt-1">
          {label}
        </div>
      </div>

      {/* Progress bar */}
      <div className="w-full">
        <Progress value={progress} className="h-1.5" />
      </div>
    </div>
  );
}
