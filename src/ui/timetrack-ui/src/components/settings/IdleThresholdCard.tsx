import { Ban } from 'lucide-react';
import { PolicyCardShell } from './PolicyCardShell';

interface IdleThresholdCardProps {
  thresholdSeconds: number;
  thresholdMinutes: number;
  isEditing: boolean;
  canEdit?: boolean;
  onEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
  onThresholdChange: (value: number) => void;
}

/**
 * IdleThresholdCard - Card para configurar threshold de inatividade
 *
 * SRP: Apenas gerencia exibição e edição do threshold de inatividade
 */
export function IdleThresholdCard({
  thresholdSeconds,
  thresholdMinutes,
  isEditing,
  canEdit,
  onEdit,
  onSave,
  onCancel,
  isSaving,
  onThresholdChange,
}: IdleThresholdCardProps) {
  const formatIdleThreshold = () => {
    const minutes = Math.floor(thresholdSeconds / 60);
    const seconds = thresholdSeconds % 60;
    if (minutes > 0 && seconds > 0) {
      return `${minutes}min ${seconds}s`;
    }
    return minutes > 0 ? `${minutes} minutos` : `${seconds} segundos`;
  };

  return (
    <PolicyCardShell
      title="Threshold de Inatividade"
      icon={Ban}
      iconColor="text-[#f3d05d]"
      iconBgColor="bg-[rgba(243,208,93,0.15)]"
      canEdit={canEdit}
      isEditing={isEditing}
      onEdit={onEdit}
      onSave={onSave}
      onCancel={onCancel}
      isSaving={isSaving}
    >
      {isEditing ? (
        <div className="space-y-3">
          <div>
            <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Tempo em minutos</label>
            <input
              type="number"
              min="1"
              max="60"
              value={thresholdMinutes}
              onChange={(e) => onThresholdChange(parseInt(e.target.value) || 1)}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#f3d05d]"
            />
          </div>
          <p className="text-[11px] text-[rgba(245,247,251,0.35)]">Mín: 1 min • Máx: 60 min</p>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="flex items-baseline gap-2">
            <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatIdleThreshold()}</span>
          </div>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)]">Tempo sem atividade para pausa automática</p>
          <p className="text-[11px] text-[rgba(245,247,251,0.35)]">Tracking pausa automaticamente após este período</p>
        </div>
      )}
    </PolicyCardShell>
  );
}
