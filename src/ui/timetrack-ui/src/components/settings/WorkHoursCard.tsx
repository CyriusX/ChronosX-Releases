import { Clock } from 'lucide-react';
import type { DayOfWeek } from '../../types/settings';
import { PolicyCardShell } from './PolicyCardShell';

interface WorkHoursCardProps {
  startTime: string;
  endTime: string;
  days: DayOfWeek[];
  timezone: string;
  isEditing: boolean;
  canEdit?: boolean;
  onEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
  onStartTimeChange: (value: string) => void;
  onEndTimeChange: (value: string) => void;
  onToggleDay: (day: DayOfWeek) => void;
}

const DAYS_OF_WEEK: { value: DayOfWeek; label: string }[] = [
  { value: 'monday', label: 'Seg' },
  { value: 'tuesday', label: 'Ter' },
  { value: 'wednesday', label: 'Qua' },
  { value: 'thursday', label: 'Qui' },
  { value: 'friday', label: 'Sex' },
  { value: 'saturday', label: 'Sáb' },
  { value: 'sunday', label: 'Dom' },
];

const DAY_NAMES: Record<string, string> = {
  monday: 'Seg',
  tuesday: 'Ter',
  wednesday: 'Qua',
  thursday: 'Qui',
  friday: 'Sex',
  saturday: 'Sáb',
  sunday: 'Dom',
};

/**
 * WorkHoursCard - Card para configurar horário de trabalho
 *
 * SRP: Apenas gerencia exibição e edição de horário de trabalho
 */
export function WorkHoursCard({
  startTime,
  endTime,
  days,
  timezone,
  isEditing,
  canEdit,
  onEdit,
  onSave,
  onCancel,
  isSaving,
  onStartTimeChange,
  onEndTimeChange,
  onToggleDay,
}: WorkHoursCardProps) {
  const formatWorkHours = () => `${startTime} - ${endTime}`;

  const formatWorkDays = () => days.map((d) => DAY_NAMES[d] || d).join(', ');

  return (
    <PolicyCardShell
      title="Horário de Trabalho"
      icon={Clock}
      iconColor="text-[#4ad9ff]"
      iconBgColor="bg-[rgba(74,217,255,0.15)]"
      canEdit={canEdit}
      isEditing={isEditing}
      onEdit={onEdit}
      onSave={onSave}
      onCancel={onCancel}
      isSaving={isSaving}
    >
      {isEditing ? (
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <div className="flex-1">
              <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Início</label>
              <input
                type="time"
                value={startTime}
                onChange={(e) => onStartTimeChange(e.target.value)}
                className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
              />
            </div>
            <div className="flex-1">
              <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Fim</label>
              <input
                type="time"
                value={endTime}
                onChange={(e) => onEndTimeChange(e.target.value)}
                className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
              />
            </div>
          </div>
          <div>
            <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-2 block">Dias de trabalho</label>
            <div className="flex flex-wrap gap-1.5">
              {DAYS_OF_WEEK.map((day) => (
                <button
                  key={day.value}
                  onClick={() => onToggleDay(day.value)}
                  className={`px-2.5 py-1 rounded-md text-[11px] font-medium transition-colors ${
                    days.includes(day.value)
                      ? 'bg-[#4ad9ff] text-[#0a0c10]'
                      : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)]'
                  }`}
                >
                  {day.label}
                </button>
              ))}
            </div>
          </div>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="flex items-baseline gap-2">
            <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatWorkHours()}</span>
          </div>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)]">{formatWorkDays()}</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.35)]">Fuso: {timezone}</p>
        </div>
      )}
    </PolicyCardShell>
  );
}
