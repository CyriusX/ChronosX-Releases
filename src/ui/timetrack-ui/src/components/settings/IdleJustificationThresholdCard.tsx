import { MessageSquareQuote } from 'lucide-react';
import { PolicyCardShell } from './PolicyCardShell';

interface IdleJustificationThresholdCardProps {
  promptThresholdSeconds: number | null;
  promptThresholdMinutes: number | null;
  minMinutes: number;
  isEditing: boolean;
  canEdit?: boolean;
  onEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
  onPromptThresholdChange: (value: number | null) => void;
}

function formatPromptThreshold(seconds: number | null): string {
  if (seconds == null) return 'Disabled';

  const minutes = Math.floor(seconds / 60);
  if (minutes <= 0) return 'Off';
  if (minutes === 1) return '1 minute';
  return `${minutes} minutes`;
}

export function IdleJustificationThresholdCard({
  promptThresholdSeconds,
  promptThresholdMinutes,
  minMinutes,
  isEditing,
  canEdit,
  onEdit,
  onSave,
  onCancel,
  isSaving,
  onPromptThresholdChange,
}: IdleJustificationThresholdCardProps) {
  return (
    <PolicyCardShell
      title="Idle justification"
      icon={MessageSquareQuote}
      iconColor="text-[#7dd3fc]"
      iconBgColor="bg-[rgba(125,211,252,0.15)]"
      canEdit={canEdit}
      isEditing={isEditing}
      onEdit={onEdit}
      onSave={onSave}
      onCancel={onCancel}
      isSaving={isSaving}
    >
      {isEditing ? (
        <div className="space-y-3">
          <label className="flex items-center gap-2 text-[12px] text-[rgba(245,247,251,0.72)]">
            <input
              type="checkbox"
              checked={promptThresholdMinutes !== null}
              onChange={(e) => onPromptThresholdChange(e.target.checked ? minMinutes : null)}
              className="rounded border-[rgba(255,255,255,0.12)] bg-[rgba(255,255,255,0.04)]"
            />
            Ask for a reason after a long idle period
          </label>

          <div className={promptThresholdMinutes === null ? 'opacity-50' : ''}>
            <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">
              Prompt after this many idle minutes
            </label>
            <input
              type="number"
              min={minMinutes}
              max="240"
              disabled={promptThresholdMinutes === null}
              value={promptThresholdMinutes ?? minMinutes}
              onChange={(e) => onPromptThresholdChange(Math.max(minMinutes, parseInt(e.target.value, 10) || minMinutes))}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#7dd3fc] disabled:cursor-not-allowed"
            />
          </div>

          <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
            Optional and non-blocking. Users can skip it for any idle period.
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="flex items-baseline gap-2">
            <span className="text-[24px] font-semibold text-[#f5f7fb]">
              {formatPromptThreshold(promptThresholdSeconds)}
            </span>
          </div>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
            When enabled, users are asked for an optional reason after they return from a long idle period.
          </p>
        </div>
      )}
    </PolicyCardShell>
  );
}
