/**
 * TimerFocusCard - Timer Card with Focus Mode support
 *
 * This component extends the Timer Card to support Focus Mode
 * (Pomodoro / Ultradian) when the org policy has focus mode enabled.
 *
 * States:
 * - State 0: Focus mode disabled in policy -> Show default Timer Card
 * - State 1: Focus mode active, waiting to start (allowUserOverride = true)
 * - State 2: Focus running (FocusRunning)
 * - State 3: Break running (BreakRunning)
 * - State 4: Focus paused (FocusPaused)
 *
 * CX-139: Frontend Card Timer com Focus Mode
 */

import { MoreVertical, Pause, Play, Square, SkipForward } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import type { FocusModePolicy } from '../../../types/settings';
import type { FocusModeSnapshot } from '../../../types/ipc';
import { FocusModeHeader, BreakHeader } from './FocusModeHeader';
import { FocusCountdown, BreakCountdown } from './FocusCountdown';
import { FocusModeStarter } from './FocusModeStarter';
import { CycleDots } from './CycleDots';

// ============================================================================
// TYPES
// ============================================================================

interface TimerFocusCardProps {
  // Policy from org
  focusModePolicy: FocusModePolicy | null;

  // Focus mode state from IPC
  focusState: FocusModeSnapshot | null;

  // Local countdown values (smooth updates)
  displayRemainingMs?: number;
  displayProgress?: number;

  // Loading state
  isLoading?: boolean;

  // Actions
  onStartFocus: () => void;
  onStopFocus: () => void;
  onPauseFocus: () => void;
  onResumeFocus: () => void;
  onSkipBreak: () => void;

  // Default timer controls (when focus mode is off)
  isPaused: boolean;
  isTracking: boolean;
  currentProject: string;
  onStartTracking: () => void;
  onPauseTracking: () => void;
  onStopTracking: () => void;
}

// ============================================================================
// COMPONENT
// ============================================================================

export function TimerFocusCard({
  focusModePolicy,
  focusState,
  displayRemainingMs,
  displayProgress,
  isLoading,
  onStartFocus,
  onStopFocus,
  onPauseFocus,
  onResumeFocus,
  onSkipBreak,
  isPaused,
  isTracking,
  currentProject,
  onStartTracking,
  onPauseTracking,
  onStopTracking,
}: TimerFocusCardProps) {
  // Check if focus mode is enabled in policy
  const isFocusModeEnabled = focusModePolicy?.enabled === true && focusModePolicy?.mode !== 'none';

  // Debug logging
  console.log('[TimerFocusCard] focusModePolicy:', focusModePolicy);
  console.log('[TimerFocusCard] focusState:', focusState);
  console.log('[TimerFocusCard] displayRemainingMs:', displayRemainingMs);
  console.log('[TimerFocusCard] isFocusModeEnabled:', isFocusModeEnabled);
  console.log('[TimerFocusCard] state:', focusState?.state);
  console.log('[TimerFocusCard] allowUserOverride:', focusState?.allowUserOverride);

  // If focus mode is disabled, render default Timer Card
  if (!isFocusModeEnabled) {
    return (
      <DefaultTimerCard
        isPaused={isPaused}
        isTracking={isTracking}
        currentProject={currentProject}
        onStartTracking={onStartTracking}
        onPauseTracking={onPauseTracking}
        onStopTracking={onStopTracking}
      />
    );
  }

  // Focus mode state
  const state = focusState?.state ?? 'Off';
  const mode = focusState?.mode ?? 'None';
  // Use display value for countdown (smooth local updates)
  const remainingMs = displayRemainingMs ?? focusState?.remainingMs ?? 0;
  const plannedMs = focusState?.plannedDurationMs ?? 0;
  const cycleNumber = focusState?.cycleNumber ?? 0;
  // Use policy's allowUserOverride when available (it's the source of truth), fallback to state
  const allowUserOverride = focusModePolicy?.allowUserOverride ?? focusState?.allowUserOverride ?? true;
  const nextBreakType = focusState?.nextBreakType ?? 'Short';

  // Get total cycles from policy
  const totalCycles = focusModePolicy?.pomodoro?.cyclesBeforeLongBreak ?? 4;

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
      <CardHeader className="pb-0 pt-[17px] px-[17px]">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Timer</span>
          <button className="w-4 h-4 flex items-center justify-center">
            <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
          </button>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-4 px-[17px]">
        {/* State: Off - Waiting to start */}
        {state === 'Off' && allowUserOverride && (
          <div className="flex flex-col items-center py-4">
            <FocusModeStarter
              mode={mode === 'None' ? (focusModePolicy?.mode === 'ultradian' ? 'Ultradian' : 'Pomodoro') : mode}
              availableModes={focusModePolicy?.mode === 'pomodoro' ? ['Pomodoro'] : focusModePolicy?.mode === 'ultradian' ? ['Ultradian'] : ['Pomodoro', 'Ultradian']}
              onStart={() => {
                console.log('[TimerFocusCard] onStartFocus called');
                onStartFocus();
              }}
            />
          </div>
        )}

        {/* State: Focus Running */}
        {state === 'FocusRunning' && (
          <div className="flex flex-col items-center py-2">
            <FocusModeHeader
              mode={mode}
              state={state}
              cycleNumber={cycleNumber}
              totalCycles={totalCycles}
            />
            <FocusCountdown
              remainingMs={remainingMs}
              plannedMs={plannedMs}
              mode={mode}
            />
            {mode === 'Pomodoro' && (
              <div className="mt-3">
                <CycleDots currentCycle={cycleNumber} totalCycles={totalCycles} />
              </div>
            )}
            <div className="flex gap-2 mt-4 w-full">
              <button
                onClick={onPauseFocus}
                className="flex items-center justify-center gap-1.5 flex-1 py-[10px] rounded-full bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
              >
                <Pause className="w-3.5 h-3.5" />
                <span>Pausar ciclo</span>
              </button>
              <button
                onClick={onStopFocus}
                className="flex items-center justify-center gap-1.5 flex-1 py-[10px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[12px] font-medium text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                <Square className="w-3.5 h-3.5" />
                <span>Encerrar</span>
              </button>
            </div>
          </div>
        )}

        {/* State: Focus Paused */}
        {state === 'FocusPaused' && (
          <div className="flex flex-col items-center py-2">
            <FocusModeHeader
              mode={mode}
              state={state}
              cycleNumber={cycleNumber}
              totalCycles={totalCycles}
            />
            <FocusCountdown
              remainingMs={remainingMs}
              plannedMs={plannedMs}
              mode={mode}
            />
            {mode === 'Pomodoro' && (
              <div className="mt-3">
                <CycleDots currentCycle={cycleNumber} totalCycles={totalCycles} />
              </div>
            )}
            <div className="flex gap-2 mt-4 w-full">
              <button
                onClick={onResumeFocus}
                className="flex items-center justify-center gap-1.5 flex-1 py-[10px] rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
              >
                <Play className="w-3.5 h-3.5" />
                <span>Retomar</span>
              </button>
              <button
                onClick={onStopFocus}
                className="flex items-center justify-center gap-1.5 flex-1 py-[10px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[12px] font-medium text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                <Square className="w-3.5 h-3.5" />
                <span>Encerrar</span>
              </button>
            </div>
          </div>
        )}

        {/* State: Break Running */}
        {state === 'BreakRunning' && (
          <div className="flex flex-col items-center py-2">
            <BreakHeader
              breakType={nextBreakType}
              durationMinutes={Math.ceil(remainingMs / 60000)}
            />
            <BreakCountdown
              remainingMs={remainingMs}
              plannedMs={plannedMs}
              breakType={nextBreakType}
            />
            <div className="flex gap-2 mt-4 w-full">
              <button
                onClick={onSkipBreak}
                className="flex items-center justify-center gap-1.5 flex-1 py-[10px] rounded-full bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
              >
                <SkipForward className="w-3.5 h-3.5" />
                <span>Pular pausa</span>
              </button>
            </div>
          </div>
        )}

        {/* Loading state */}
        {isLoading && (
          <div className="flex items-center justify-center py-8">
            <div className="w-6 h-6 border-2 border-[#8b7aff] border-t-transparent rounded-full animate-spin" />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ============================================================================
// DEFAULT TIMER CARD (State 0 - Focus Mode Disabled)
// ============================================================================

function DefaultTimerCard({
  isPaused,
  isTracking,
  currentProject,
  onStartTracking,
  onPauseTracking,
  onStopTracking,
}: {
  isPaused: boolean;
  isTracking: boolean;
  currentProject: string;
  onStartTracking: () => void;
  onPauseTracking: () => void;
  onStopTracking: () => void;
}) {
  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
      <CardHeader className="pb-0 pt-[17px] px-[17px]">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Timer</span>
          <button className="w-4 h-4 flex items-center justify-center">
            <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
          </button>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-4 px-[17px]">
        <div className="flex items-center gap-2 pl-2 mb-3">
          <div
            className={`w-2 h-2 rounded-full ${
              !isTracking
                ? 'bg-[#ff5f5f] opacity-70'
                : isPaused
                  ? 'bg-[#f3d05d] opacity-70'
                  : 'bg-[#c8db68] opacity-50 shadow-[0px_10px_15px_0px_rgba(200,219,104,0.6),0px_4px_6px_0px_rgba(200,219,104,0.6)]'
            }`}
          />
          <span className="text-[12px] text-[rgba(245,247,251,0.6)]">
            {!isTracking ? 'Parado' : isPaused ? 'Pausado' : 'Em andamento'}
          </span>
        </div>
        <div className="bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] rounded-[10px] px-[9px] py-2 flex items-center justify-between mb-3">
          <div>
            <p className="text-[10px] text-[rgba(245,247,251,0.4)]">Projetos</p>
            <p className="text-[14px] text-[rgba(245,247,251,0.9)]">{currentProject}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {!isTracking && !isPaused ? (
            <button
              onClick={onStartTracking}
              className="flex-1 py-[8px] rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
            >
              Iniciar
            </button>
          ) : (
            <>
              <button
                onClick={onPauseTracking}
                className="flex-1 py-[8px] rounded-full bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
              >
                {isPaused ? 'Retomar' : 'Pausar'}
              </button>
              <button
                onClick={onStopTracking}
                className="flex-1 py-[9px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[12px] font-medium text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                Finalizar
              </button>
            </>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
