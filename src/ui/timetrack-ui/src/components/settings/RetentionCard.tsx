import { Database } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { PolicyCardShell } from './PolicyCardShell';

interface RetentionCardProps {
  retentionDays: number;
  isEditing: boolean;
  canEdit?: boolean;
  onEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
  onRetentionChange: (value: number) => void;
}

/**
 * RetentionCard - Card para configurar retenção de dados
 *
 * SRP: Apenas gerencia exibição e edição da retenção de dados
 */
export function RetentionCard({
  retentionDays,
  isEditing,
  canEdit,
  onEdit,
  onSave,
  onCancel,
  isSaving,
  onRetentionChange,
}: RetentionCardProps) {
  const { t } = useTranslation();
  const formatRetention = () => {
    if (retentionDays >= 365) {
      const years = Math.floor(retentionDays / 365);
      return `${years} ano${years > 1 ? 's' : ''}`;
    }
    if (retentionDays >= 30) {
      const months = Math.floor(retentionDays / 30);
      return `${months} ${months > 1 ? 'meses' : 'mês'}`;
    }
    return `${retentionDays} ${t('policies.retention.days')}`;
  };

  return (
    <PolicyCardShell
      title={t('policies.retention.title')}
      icon={Database}
      iconColor="text-[#05df72]"
      iconBgColor="bg-[rgba(5,223,114,0.15)]"
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
            <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">{t('policies.retention.days')}</label>
            <input
              type="number"
              min="7"
              max="365"
              value={retentionDays}
              onChange={(e) => onRetentionChange(parseInt(e.target.value) || 7)}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#05df72]"
            />
          </div>
          <p className="text-[11px] text-[rgba(245,247,251,0.35)]">Mín: 7 dias • Máx: 365 dias</p>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="flex items-baseline gap-2">
            <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatRetention()}</span>
          </div>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)]">{t('policies.retention.subtitle')}</p>
        </div>
      )}
    </PolicyCardShell>
  );
}
