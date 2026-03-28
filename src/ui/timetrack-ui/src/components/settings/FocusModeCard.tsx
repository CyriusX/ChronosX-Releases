import { Focus, Timer, Pencil, Check, X } from 'lucide-react';
import type { FocusModePolicy, PomodoroConfig, UltradianConfig } from '../../types/settings';

interface FocusModeCardProps {
  focusMode: FocusModePolicy;
  isEditing: boolean;
  canEdit?: boolean;
  onEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
  onFocusModeChange: (value: FocusModePolicy) => void;
}

/**
 * FocusModeCard - Card para configurar modo de foco
 *
 * SRP: Apenas gerencia exibição e edição do modo de foco
 */
export function FocusModeCard({
  focusMode,
  isEditing,
  canEdit,
  onEdit,
  onSave,
  onCancel,
  isSaving,
  onFocusModeChange,
}: FocusModeCardProps) {
  const updateFocusMode = (updates: Partial<FocusModePolicy>) => {
    onFocusModeChange({ ...focusMode, ...updates });
  };

  const updatePomodoro = (updates: Partial<PomodoroConfig>) => {
    onFocusModeChange({
      ...focusMode,
      pomodoro: { ...focusMode.pomodoro!, ...updates },
    });
  };

  const updateUltradian = (updates: Partial<UltradianConfig>) => {
    onFocusModeChange({
      ...focusMode,
      ultradian: { ...focusMode.ultradian!, ...updates },
    });
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[rgba(139,122,255,0.15)] flex items-center justify-center">
            <Focus className="w-5 h-5 text-[#8b7aff]" />
          </div>
          <div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Modo de Foco</h3>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Pomodoro ou Ciclo Ultradian para produtividade</p>
          </div>
        </div>
        {canEdit && (
          isEditing ? (
            <div className="flex items-center gap-2">
              <button
                onClick={onSave}
                disabled={isSaving}
                className="w-7 h-7 rounded-lg bg-[rgba(5,223,114,0.15)] border border-[rgba(5,223,114,0.3)] flex items-center justify-center hover:bg-[rgba(5,223,114,0.25)] transition-colors disabled:opacity-50"
                title="Salvar"
              >
                <Check className="w-3.5 h-3.5 text-[#05df72]" />
              </button>
              <button
                onClick={onCancel}
                disabled={isSaving}
                className="w-7 h-7 rounded-lg bg-[rgba(255,107,107,0.15)] border border-[rgba(255,107,107,0.3)] flex items-center justify-center hover:bg-[rgba(255,107,107,0.25)] transition-colors disabled:opacity-50"
                title="Cancelar"
              >
                <X className="w-3.5 h-3.5 text-[#ff6b6b]" />
              </button>
            </div>
          ) : (
            <button
              onClick={onEdit}
              className="w-7 h-7 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              title="Editar"
            >
              <Pencil className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
            </button>
          )
        )}
      </div>

      {isEditing ? (
        <FocusModeEditForm
          focusMode={focusMode}
          onUpdateFocusMode={updateFocusMode}
          onUpdatePomodoro={updatePomodoro}
          onUpdateUltradian={updateUltradian}
        />
      ) : (
        <FocusModeDisplay focusMode={focusMode} />
      )}
    </div>
  );
}

interface FocusModeEditFormProps {
  focusMode: FocusModePolicy;
  onUpdateFocusMode: (updates: Partial<FocusModePolicy>) => void;
  onUpdatePomodoro: (updates: Partial<PomodoroConfig>) => void;
  onUpdateUltradian: (updates: Partial<UltradianConfig>) => void;
}

function FocusModeEditForm({
  focusMode,
  onUpdateFocusMode,
  onUpdatePomodoro,
  onUpdateUltradian,
}: FocusModeEditFormProps) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      {/* Enable/Mode Selection */}
      <div className="space-y-3">
        <div>
          <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Status</label>
          <button
            onClick={() => onUpdateFocusMode({ enabled: !focusMode.enabled })}
            className={`w-full px-3 py-2 rounded-lg text-[13px] font-medium transition-colors ${
              focusMode.enabled
                ? 'bg-[rgba(5,223,114,0.15)] text-[#05df72] border border-[rgba(5,223,114,0.3)]'
                : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)]'
            }`}
          >
            {focusMode.enabled ? 'Ativado' : 'Desativado'}
          </button>
        </div>
        <div>
          <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Modo</label>
          <select
            value={focusMode.mode}
            onChange={(e) => onUpdateFocusMode({ mode: e.target.value as 'pomodoro' | 'ultradian' | 'none' })}
            disabled={!focusMode.enabled}
            className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#8b7aff] disabled:opacity-50"
          >
            <option value="none">Nenhum</option>
            <option value="pomodoro">Pomodoro</option>
            <option value="ultradian">Ciclo Ultradian</option>
          </select>
        </div>
        <div>
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={focusMode.allowUserOverride}
              onChange={(e) => onUpdateFocusMode({ allowUserOverride: e.target.checked })}
              className="w-4 h-4 rounded border-[rgba(255,255,255,0.08)] bg-[rgba(255,255,255,0.04)]"
            />
            <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Permitir override do usuário</span>
          </label>
        </div>
      </div>

      {/* Pomodoro Config */}
      <div className={`space-y-3 ${focusMode.mode !== 'pomodoro' ? 'opacity-50' : ''}`}>
        <h4 className="text-[12px] font-medium text-[#f5f7fb] flex items-center gap-2">
          <Timer className="w-4 h-4 text-[#ff6b6b]" />
          Pomodoro
        </h4>
        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1 block">Foco (min)</label>
            <input
              type="number"
              min="10"
              max="180"
              value={focusMode.pomodoro?.focusMinutes ?? 25}
              onChange={(e) => onUpdatePomodoro({ focusMinutes: parseInt(e.target.value) || 25 })}
              disabled={focusMode.mode !== 'pomodoro'}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-2 py-1.5 text-[12px] text-[#f5f7fb] focus:outline-none disabled:opacity-50"
            />
          </div>
          <div>
            <label className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1 block">Pausa curta</label>
            <input
              type="number"
              min="5"
              max="60"
              value={focusMode.pomodoro?.shortBreakMinutes ?? 5}
              onChange={(e) => onUpdatePomodoro({ shortBreakMinutes: parseInt(e.target.value) || 5 })}
              disabled={focusMode.mode !== 'pomodoro'}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-2 py-1.5 text-[12px] text-[#f5f7fb] focus:outline-none disabled:opacity-50"
            />
          </div>
          <div>
            <label className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1 block">Pausa longa</label>
            <input
              type="number"
              min="5"
              max="60"
              value={focusMode.pomodoro?.longBreakMinutes ?? 15}
              onChange={(e) => onUpdatePomodoro({ longBreakMinutes: parseInt(e.target.value) || 15 })}
              disabled={focusMode.mode !== 'pomodoro'}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-2 py-1.5 text-[12px] text-[#f5f7fb] focus:outline-none disabled:opacity-50"
            />
          </div>
          <div>
            <label className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1 block">Ciclos</label>
            <input
              type="number"
              min="2"
              max="8"
              value={focusMode.pomodoro?.cyclesBeforeLongBreak ?? 4}
              onChange={(e) => onUpdatePomodoro({ cyclesBeforeLongBreak: parseInt(e.target.value) || 4 })}
              disabled={focusMode.mode !== 'pomodoro'}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-2 py-1.5 text-[12px] text-[#f5f7fb] focus:outline-none disabled:opacity-50"
            />
          </div>
        </div>
      </div>

      {/* Ultradian Config */}
      <div className={`space-y-3 ${focusMode.mode !== 'ultradian' ? 'opacity-50' : ''}`}>
        <h4 className="text-[12px] font-medium text-[#f5f7fb] flex items-center gap-2">
          <Timer className="w-4 h-4 text-[#8B5CF6]" />
          Ultradian
        </h4>
        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1 block">Foco (min)</label>
            <input
              type="number"
              min="10"
              max="180"
              value={focusMode.ultradian?.focusMinutes ?? 90}
              onChange={(e) => onUpdateUltradian({ focusMinutes: parseInt(e.target.value) || 90 })}
              disabled={focusMode.mode !== 'ultradian'}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-2 py-1.5 text-[12px] text-[#f5f7fb] focus:outline-none disabled:opacity-50"
            />
          </div>
          <div>
            <label className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1 block">Pausa (min)</label>
            <input
              type="number"
              min="5"
              max="60"
              value={focusMode.ultradian?.breakMinutes ?? 20}
              onChange={(e) => onUpdateUltradian({ breakMinutes: parseInt(e.target.value) || 20 })}
              disabled={focusMode.mode !== 'ultradian'}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-2 py-1.5 text-[12px] text-[#f5f7fb] focus:outline-none disabled:opacity-50"
            />
          </div>
        </div>
        <p className="text-[10px] text-[rgba(245,247,251,0.35)]">Ciclo natural de ~90 min de foco + pausa de recuperação</p>
      </div>
    </div>
  );
}

interface FocusModeDisplayProps {
  focusMode: FocusModePolicy;
}

function FocusModeDisplay({ focusMode }: FocusModeDisplayProps) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      {/* Status */}
      <div className="space-y-2">
        <div className="flex items-center gap-2">
          <span className={`w-2 h-2 rounded-full ${focusMode.enabled ? 'bg-[#05df72]' : 'bg-[rgba(245,247,251,0.3)]'}`} />
          <span className="text-[13px] text-[rgba(245,247,251,0.5)]">
            {focusMode.enabled ? 'Ativado' : 'Desativado'}
          </span>
        </div>
        {focusMode.enabled && focusMode.mode !== 'none' && (
          <p className="text-[24px] font-semibold text-[#f5f7fb] capitalize">{focusMode.mode}</p>
        )}
        <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
          {focusMode.allowUserOverride ? 'Usuários podem iniciar/parar manualmente' : 'Ciclos automáticos'}
        </p>
      </div>

      {/* Pomodoro Info */}
      <div className={focusMode.mode !== 'pomodoro' ? 'opacity-40' : ''}>
        <h4 className="text-[12px] font-medium text-[#f5f7fb] flex items-center gap-2 mb-2">
          <Timer className="w-4 h-4 text-[#ff6b6b]" />
          Pomodoro
        </h4>
        <div className="space-y-1">
          <p className="text-[13px] text-[rgba(245,247,251,0.7)]">
            {focusMode.pomodoro?.focusMinutes ?? 25}min foco • {focusMode.pomodoro?.shortBreakMinutes ?? 5}min pausa
          </p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)]">
            {focusMode.pomodoro?.cyclesBeforeLongBreak ?? 4} ciclos → {focusMode.pomodoro?.longBreakMinutes ?? 15}min pausa longa
          </p>
        </div>
      </div>

      {/* Ultradian Info */}
      <div className={focusMode.mode !== 'ultradian' ? 'opacity-40' : ''}>
        <h4 className="text-[12px] font-medium text-[#f5f7fb] flex items-center gap-2 mb-2">
          <Timer className="w-4 h-4 text-[#8B5CF6]" />
          Ultradian
        </h4>
        <div className="space-y-1">
          <p className="text-[13px] text-[rgba(245,247,251,0.7)]">
            {focusMode.ultradian?.focusMinutes ?? 90}min foco • {focusMode.ultradian?.breakMinutes ?? 20}min pausa
          </p>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Ciclo natural de produtividade</p>
        </div>
      </div>
    </div>
  );
}
