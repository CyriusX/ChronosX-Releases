import { useState } from 'react';
import { Timer, Activity, RotateCcw } from 'lucide-react';
import { getUserTimerConfig, saveUserTimerConfig, DEFAULT_CONFIGS, type TimerConfig, useTimerStore } from '../../stores/timerStore';

const ULTRADIAN_STORAGE_KEY = 'timetrack-ultradian-waves';

export function FocusTimerSection() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Foco & Timer</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          Personalize os tempos de foco, pausa e ciclos dos seus timers
        </p>
      </div>

      {/* Pomodoro Config */}
      <PomodoroConfigCard />

      {/* Ultradian Config */}
      <UltradianConfigCard />

      {/* Ultradian Waves */}
      <UltradianWavesSetting />
    </div>
  );
}

// ============================================================================
// Pomodoro Configuration Card
// ============================================================================

function PomodoroConfigCard() {
  const [config, setConfig] = useState<TimerConfig>(() => getUserTimerConfig('pomodoro'));
  const defaults = DEFAULT_CONFIGS.pomodoro;

  const isDefault =
    config.focusMs === defaults.focusMs &&
    config.shortBreakMs === defaults.shortBreakMs &&
    config.longBreakMs === defaults.longBreakMs &&
    config.cyclesBeforeLong === defaults.cyclesBeforeLong;

  const updateField = (field: keyof TimerConfig, minutes: number) => {
    const ms = Math.max(1, minutes) * 60000;
    const updated = { ...config, [field]: ms };
    setConfig(updated);
    saveUserTimerConfig('pomodoro', updated);
  };

  const updateCycles = (cycles: number) => {
    const clamped = Math.max(1, Math.min(10, cycles));
    const updated = { ...config, cyclesBeforeLong: clamped };
    setConfig(updated);
    saveUserTimerConfig('pomodoro', updated);
  };

  const resetDefaults = () => {
    setConfig(defaults);
    saveUserTimerConfig('pomodoro', defaults);
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center">
            <Timer className="w-4 h-4 text-white" />
          </div>
          <div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Pomodoro</h3>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Ciclos de foco curtos com pausas regulares</p>
          </div>
        </div>
        {!isDefault && (
          <button
            onClick={resetDefaults}
            className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
          >
            <RotateCcw className="w-3 h-3" />
            Restaurar padrão
          </button>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <TimeInput
          label="Foco"
          value={config.focusMs / 60000}
          onChange={(v) => updateField('focusMs', v)}
          min={5}
          max={120}
          unit="min"
        />
        <TimeInput
          label="Pausa curta"
          value={config.shortBreakMs / 60000}
          onChange={(v) => updateField('shortBreakMs', v)}
          min={1}
          max={30}
          unit="min"
        />
        <TimeInput
          label="Pausa longa"
          value={config.longBreakMs / 60000}
          onChange={(v) => updateField('longBreakMs', v)}
          min={5}
          max={60}
          unit="min"
        />
        <TimeInput
          label="Ciclos até pausa longa"
          value={config.cyclesBeforeLong}
          onChange={updateCycles}
          min={1}
          max={10}
          unit="ciclos"
        />
      </div>

      <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-3">
        {config.cyclesBeforeLong} ciclos de {config.focusMs / 60000}min foco + {config.shortBreakMs / 60000}min pausa, depois {config.longBreakMs / 60000}min pausa longa
      </p>
    </div>
  );
}

// ============================================================================
// Ultradian Configuration Card
// ============================================================================

function UltradianConfigCard() {
  const [config, setConfig] = useState<TimerConfig>(() => getUserTimerConfig('ultradian'));
  const defaults = DEFAULT_CONFIGS.ultradian;

  const isDefault =
    config.focusMs === defaults.focusMs &&
    config.shortBreakMs === defaults.shortBreakMs;

  const updateField = (field: keyof TimerConfig, minutes: number) => {
    const ms = Math.max(1, minutes) * 60000;
    const updated = { ...config, [field]: ms };
    // Keep short/long break in sync for ultradian
    if (field === 'shortBreakMs') {
      updated.longBreakMs = ms;
    }
    setConfig(updated);
    saveUserTimerConfig('ultradian', updated);
  };

  const resetDefaults = () => {
    setConfig(defaults);
    saveUserTimerConfig('ultradian', defaults);
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#c27aff] to-[#8b7aff] flex items-center justify-center">
            <Activity className="w-4 h-4 text-white" />
          </div>
          <div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Ultradian</h3>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Ritmo ultradiano com blocos longos de foco profundo</p>
          </div>
        </div>
        {!isDefault && (
          <button
            onClick={resetDefaults}
            className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
          >
            <RotateCcw className="w-3 h-3" />
            Restaurar padrão
          </button>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <TimeInput
          label="Foco"
          value={config.focusMs / 60000}
          onChange={(v) => updateField('focusMs', v)}
          min={30}
          max={180}
          unit="min"
        />
        <TimeInput
          label="Descanso"
          value={config.shortBreakMs / 60000}
          onChange={(v) => updateField('shortBreakMs', v)}
          min={5}
          max={60}
          unit="min"
        />
      </div>

      <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-3">
        {config.focusMs / 60000}min de foco profundo + {config.shortBreakMs / 60000}min de descanso por onda
      </p>
    </div>
  );
}

// ============================================================================
// Ultradian Waves Setting
// ============================================================================

function UltradianWavesSetting() {
  const [waves, setWaves] = useState<number>(() => {
    try { return parseInt(localStorage.getItem(ULTRADIAN_STORAGE_KEY) ?? '1', 10) || 1; }
    catch { return 1; }
  });

  const ultradianConfig = getUserTimerConfig('ultradian');
  const focusMin = ultradianConfig.focusMs / 60000;
  const breakMin = ultradianConfig.shortBreakMs / 60000;
  const waveMin = focusMin + breakMin;

  const update = (value: number) => {
    const clamped = Math.max(1, Math.min(5, value));
    setWaves(clamped);
    localStorage.setItem(ULTRADIAN_STORAGE_KEY, String(clamped));
    // Sync to Zustand store so the timer page and active sessions see the change
    useTimerStore.setState({ ultradianWaves: clamped });
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#c27aff] to-[#8b7aff] flex items-center justify-center">
          <Activity className="w-4 h-4 text-white" />
        </div>
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">Ondas Ultradian</h3>
      </div>

      <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-4">
        Número de ciclos de ondas (Foco + Descanso) por sessão Ultradian
      </p>

      <div className="flex items-center gap-3">
        {[1, 2, 3, 4, 5].map((n) => (
          <button
            key={n}
            onClick={() => update(n)}
            className={`w-10 h-10 rounded-lg text-[14px] font-medium transition-all ${
              waves === n
                ? 'bg-gradient-to-r from-[#c27aff] to-[#8b7aff] text-white shadow-[0_3px_10px_rgba(139,122,255,0.3)]'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            {n}
          </button>
        ))}
      </div>

      <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-2">
        {waves} {waves === 1 ? 'onda' : 'ondas'} = {waves * focusMin}min foco + {waves * breakMin}min descanso = {waves * waveMin}min total
      </p>
    </div>
  );
}

// ============================================================================
// Shared TimeInput component
// ============================================================================

interface TimeInputProps {
  label: string;
  value: number;
  onChange: (value: number) => void;
  min: number;
  max: number;
  unit: string;
}

function TimeInput({ label, value, onChange, min, max, unit }: TimeInputProps) {
  // Use local string state so the user can freely type intermediate values
  // (e.g., clearing the field or typing "12" digit-by-digit).
  // Commit the validated value on blur or Enter.
  const [draft, setDraft] = useState<string>(String(value));
  const [isFocused, setIsFocused] = useState(false);

  const commit = () => {
    const n = parseInt(draft, 10);
    if (!isNaN(n)) {
      const clamped = Math.max(min, Math.min(max, n));
      onChange(clamped);
      setDraft(String(clamped));
    } else {
      // Reset to current value if input is invalid
      setDraft(String(value));
    }
  };

  // Sync draft with external value changes (e.g., reset to defaults)
  // but only when the input is not focused
  const displayValue = isFocused ? draft : String(value);

  return (
    <div>
      <label className="text-[11px] text-[rgba(245,247,251,0.4)] mb-1.5 block">{label}</label>
      <div className="flex items-center gap-2">
        <input
          type="number"
          value={displayValue}
          onChange={(e) => setDraft(e.target.value)}
          onFocus={() => {
            setIsFocused(true);
            setDraft(String(value));
          }}
          onBlur={() => {
            setIsFocused(false);
            commit();
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              commit();
              (e.target as HTMLInputElement).blur();
            }
          }}
          min={min}
          max={max}
          className="w-full px-3 py-2 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] rounded-lg text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(74,217,255,0.3)] transition-colors [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
        />
        <span className="text-[11px] text-[rgba(245,247,251,0.3)] flex-shrink-0 w-10">{unit}</span>
      </div>
    </div>
  );
}
