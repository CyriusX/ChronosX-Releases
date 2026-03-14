/**
 * FocusModeStarter - Mode selector and start button
 *
 * Shows when focus mode is enabled but not running
 * and allowUserOverride is true
 *
 * CX-139: Timer Focus Card
 */

import { Play, Focus, Activity } from 'lucide-react';
import type { FocusModeType } from '../../../types/ipc';

interface FocusModeStarterProps {
  mode: FocusModeType;
  availableModes: FocusModeType[];
  onStart: () => void;
  onModeChange?: (mode: FocusModeType) => void;
}

export function FocusModeStarter({
  mode,
  availableModes,
  onStart,
  onModeChange,
}: FocusModeStarterProps) {
  const showModeSelector = availableModes.length > 1 && availableModes.includes('Pomodoro') && availableModes.includes('Ultradian');

  return (
    <div className="flex flex-col items-center gap-3">
      {/* Mode selector - only show if both modes available */}
      {showModeSelector && onModeChange && (
        <div className="flex items-center gap-3">
          <button
            onClick={() => onModeChange('Pomodoro')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] transition-all ${
              mode === 'Pomodoro'
                ? 'bg-[rgba(139,122,255,0.2)] border border-[rgba(139,122,255,0.4)] text-[#8b7aff]'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]'
            }`}
          >
            <Focus className="w-3.5 h-3.5" />
            <span>Pomodoro</span>
          </button>
          <button
            onClick={() => onModeChange('Ultradian')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] transition-all ${
              mode === 'Ultradian'
                ? 'bg-[rgba(139,122,255,0.2)] border border-[rgba(139,122,255,0.4)] text-[#8b7aff]'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]'
            }`}
          >
            <Activity className="w-3.5 h-3.5" />
            <span>Ultradian</span>
          </button>
        </div>
      )}

      {/* Start button */}
      <button
        onClick={() => {
          console.log('[FocusModeStarter] Start button clicked');
          onStart();
        }}
        className="flex items-center justify-center gap-2 px-6 py-2.5 rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[13px] font-medium text-white hover:opacity-90 transition-opacity shadow-[0_4px_12px_rgba(5,223,114,0.25)]"
      >
        <Play className="w-4 h-4" />
        <span>Iniciar ciclo de foco</span>
      </button>
    </div>
  );
}
