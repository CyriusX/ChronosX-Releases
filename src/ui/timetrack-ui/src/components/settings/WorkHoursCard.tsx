import { Clock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
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

const DAY_KEYS: { value: DayOfWeek; key: string }[] = [
  { value: 'monday', key: 'policies.workHours.days.monday' },
  { value: 'tuesday', key: 'policies.workHours.days.tuesday' },
  { value: 'wednesday', key: 'policies.workHours.days.wednesday' },
  { value: 'thursday', key: 'policies.workHours.days.thursday' },
  { value: 'friday', key: 'policies.workHours.days.friday' },
  { value: 'saturday', key: 'policies.workHours.days.saturday' },
  { value: 'sunday', key: 'policies.workHours.days.sunday' },
];

const DAY_NAME_KEYS: Record<string, string> = {
  monday: 'policies.workHours.days.monday',
  tuesday: 'policies.workHours.days.tuesday',
  wednesday: 'policies.workHours.days.wednesday',
  thursday: 'policies.workHours.days.thursday',
  friday: 'policies.workHours.days.friday',
  saturday: 'policies.workHours.days.saturday',
  sunday: 'policies.workHours.days.sunday',
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
  const { t } = useTranslation();
  const formatWorkHours = () => `${startTime} - ${endTime}`;

  const formatWorkDays = () => days.map((d) => DAY_NAME_KEYS[d] ? t(DAY_NAME_KEYS[d]) : d).join(', ');

  return (
    <PolicyCardShell
      title={t('policies.workHours.title')}
      icon={Clock}
      iconColor="text-[#8B5CF6]"
      iconBgColor="bg-[rgba(139,92,246,0.15)]"
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
              <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">{t('policies.workHours.start')}</label>
              <input
                type="time"
                value={startTime}
                onChange={(e) => onStartTimeChange(e.target.value)}
                className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#8B5CF6]"
              />
            </div>
            <div className="flex-1">
              <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">{t('policies.workHours.end')}</label>
              <input
                type="time"
                value={endTime}
                onChange={(e) => onEndTimeChange(e.target.value)}
                className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#8B5CF6]"
              />
            </div>
          </div>
          <div>
            <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-2 block">Dias de trabalho</label>
            <div className="flex flex-wrap gap-1.5">
              {DAY_KEYS.map((day) => (
                <button
                  key={day.value}
                  onClick={() => onToggleDay(day.value)}
                  className={`px-2.5 py-1 rounded-md text-[11px] font-medium transition-colors ${
                    days.includes(day.value)
                      ? 'bg-[#8B5CF6] text-[#0a0c10]'
                      : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)]'
                  }`}
                >
                  {t(day.key)}
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
          <p className="text-[11px] text-[rgba(245,247,251,0.35)]">{t('policies.workHours.timezone')}: {timezone}</p>
        </div>
      )}
    </PolicyCardShell>
  );
}
