/**
 * FocusModeHeader - Header with mode icon and cycle info
 *
 * CX-139: Timer Focus Card
 */

import { useTranslation } from 'react-i18next';
import { Focus, Activity, Coffee } from 'lucide-react';
import type { FocusModeType, FocusModeState } from '../../../types/ipc';

interface FocusModeHeaderProps {
  mode: FocusModeType;
  state: FocusModeState;
  cycleNumber: number;
  totalCycles: number;
}

export function FocusModeHeader({
  mode,
  state,
  cycleNumber,
  totalCycles,
}: FocusModeHeaderProps) {
  const { t } = useTranslation();
  const modeLabel = mode === 'Pomodoro' ? t('timer.pomodoro') : t('timer.ultradian');
  const ModeIcon = mode === 'Pomodoro' ? Focus : Activity;

  // Don't show if off or no mode
  if (state === 'Off' || mode === 'None') {
    return null;
  }

  return (
    <div className="flex items-center justify-center gap-2 mb-3">
      <ModeIcon className="w-4 h-4 text-[#8b7aff]" />
      <span className="text-[14px] font-medium text-[#f5f7fb]">{modeLabel}</span>
      {mode === 'Pomodoro' && (
        <>
          <span className="text-[rgba(245,247,251,0.3)]">{'\u2022'}</span>
          <span className="text-[12px] text-[rgba(245,247,251,0.6)]">
            {t('timer.cycleOf', { current: cycleNumber, total: totalCycles })}
          </span>
        </>
      )}
    </div>
  );
}

/**
 * BreakHeader - Header for break mode
 */
interface BreakHeaderProps {
  breakType: 'Short' | 'Long';
  durationMinutes: number;
}

export function BreakHeader({ breakType, durationMinutes }: BreakHeaderProps) {
  const { t } = useTranslation();
  const label = breakType === 'Long' ? t('timer.longBreak') : t('timer.shortBreak');

  return (
    <div className="flex items-center justify-center gap-2 mb-3">
      <Coffee className="w-4 h-4 text-[#8b7aff]" />
      <span className="text-[14px] font-medium text-[#f5f7fb]">{label}</span>
      <span className="text-[12px] text-[rgba(245,247,251,0.6)]">
        {durationMinutes} min
      </span>
    </div>
  );
}
