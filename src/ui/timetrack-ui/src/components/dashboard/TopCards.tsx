/**
 * TopCards - Dashboard top cards component
 *
 * Displays three summary cards:
 * 1. Tempo Rastreado - Time tracked today with circular progress
 * 2. Foco - Focus percentage and sessions
 * 3. Timer - Timer card with optional Focus Mode support (CX-139)
 *
 * CX-139: Timer Card extended with Focus Mode (Pomodoro/Ultradian)
 */

import { MoreVertical } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse } from '../../types/ipc';
import type { FocusModePolicy } from '../../types/settings';
import { TimerFocusCard } from './TimerFocusCard';
import { useFocusMode } from '../../hooks/useFocusMode';

interface TopCardsProps {
  summary: TodaySummaryResponse | null;
  isPaused: boolean;
  isTracking: boolean;
  focusModePolicy: FocusModePolicy | null;
  onStartTracking: () => void;
  onPauseTracking: () => void;
  onStopTracking: () => void;
}

export function TopCards({
  summary,
  isPaused,
  isTracking,
  focusModePolicy,
  onStartTracking,
  onPauseTracking,
  onStopTracking,
}: TopCardsProps) {
  // Focus mode hook - only used when focus mode is enabled
  const focusMode = useFocusMode();

  // Debug logging
  console.log('[TopCards] focusModePolicy:', focusModePolicy);
  console.log('[TopCards] focusMode.focusState:', focusMode.focusState);
  console.log('[TopCards] focusMode.isLoading:', focusMode.isLoading);

  // Calculate values from summary
  const totalMinutes = summary?.totalDuration ?? 0;
  const focusTime = summary?.focusTime ?? 0;
  const sessionsCount = summary?.sessionsCount ?? 0;
  const productiveTime = summary?.productiveTime ?? 0;

  // Focus percentage (0-100)
  const focusPercentage = totalMinutes > 0 ? Math.round((focusTime / totalMinutes) * 100) : 0;

  // Progress towards 8-hour goal
  const progressPercentage = Math.min((totalMinutes / (8 * 60)) * 100, 100);

  // Current project from top applications
  const currentProject = summary?.topProjects?.[0]?.name ?? 'Sem projeto';

  return (
    <div className="grid grid-cols-3 gap-6">
      {/* Tempo Rastreado Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Tempo rastreado</span>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="flex flex-col items-center">
            <div className="relative w-[128px] h-[128px]">
              <svg className="w-full h-full -rotate-90" viewBox="0 0 128 128">
                <circle cx="64" cy="64" r="56" fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="12" />
                <circle cx="64" cy="64" r="56" fill="none" stroke="url(#gradient1)" strokeWidth="12" strokeDasharray={`${progressPercentage * 3.52} 352`} strokeLinecap="round" />
                <defs>
                  <linearGradient id="gradient1" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stopColor="#4ad9ff" />
                    <stop offset="100%" stopColor="#3c7bff" />
                  </linearGradient>
                </defs>
              </svg>
              <div className="absolute inset-0 flex items-center justify-center">
                <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatDuration(totalMinutes)}</span>
              </div>
            </div>
            <div className="mt-4 text-center">
              <p className="text-[24px] font-semibold text-[rgba(245,247,251,0.9)]">{Math.round(progressPercentage)}%</p>
              <div className="flex items-center justify-center gap-[6px] mt-1">
                <div className="w-1 h-1 rounded-full bg-[#00d3f3]" />
                <span className="text-[10px] text-[rgba(245,247,251,0.4)] tracking-[0.25px] uppercase">vs ontem</span>
              </div>
              <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-2">Meta de hoje</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Foco Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Foco</span>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="flex flex-col items-center">
            <div className="relative w-[128px] h-[128px]">
              <svg className="w-full h-full -rotate-90" viewBox="0 0 128 128">
                <circle cx="64" cy="64" r="56" fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="12" />
                <circle cx="64" cy="64" r="56" fill="none" stroke="#05df72" strokeWidth="12" strokeDasharray={`${focusPercentage * 3.52} 352`} strokeLinecap="round" />
              </svg>
              <div className="absolute inset-0 flex items-center justify-center">
                <span className="text-[36px] font-semibold text-[#f5f7fb]">{focusPercentage}</span>
              </div>
            </div>
            <div className="mt-4 text-center space-y-1">
              <p className="text-[12px] text-[rgba(245,247,251,0.5)]">
                Produtivo: <span className="text-[rgba(245,247,251,0.8)]">{formatDuration(productiveTime)}</span>
              </p>
              <p className="text-[12px] text-[rgba(245,247,251,0.5)]">
                Sessões: <span className="text-[rgba(245,247,251,0.8)]">{sessionsCount}</span>
              </p>
            </div>
            <div className="flex gap-2 mt-4">
              <span className="px-[10px] py-1 text-[10px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]">
                Foco: {focusPercentage}%
              </span>
              <span className="px-[10px] py-1 text-[10px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]">
                {sessionsCount} sessões
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Timer Card - with Focus Mode support */}
      <TimerFocusCard
        focusModePolicy={focusModePolicy}
        focusState={focusMode.focusState}
        displayRemainingMs={focusMode.displayRemainingMs}
        displayProgress={focusMode.displayProgress}
        isLoading={focusMode.isLoading}
        onStartFocus={focusMode.startFocus}
        onStopFocus={focusMode.stopFocus}
        onPauseFocus={focusMode.pauseFocus}
        onResumeFocus={focusMode.resumeFocus}
        onSkipBreak={focusMode.skipBreak}
        isPaused={isPaused}
        isTracking={isTracking}
        currentProject={currentProject}
        onStartTracking={onStartTracking}
        onPauseTracking={onPauseTracking}
        onStopTracking={onStopTracking}
      />
    </div>
  );
}
