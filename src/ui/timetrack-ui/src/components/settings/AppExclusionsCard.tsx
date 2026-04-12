import { AppWindow } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { PolicyCardShell } from './PolicyCardShell';

interface AppExclusionsCardProps {
  exclusionsCount: number;
}

/**
 * AppExclusionsCard - Card para exibir apps excluídos
 *
 * SRP: Apenas exibe contagem de apps excluídos (sem edição)
 */
export function AppExclusionsCard({ exclusionsCount }: AppExclusionsCardProps) {
  const { t } = useTranslation();
  return (
    <PolicyCardShell
      title={t('policies.appExclusions.title')}
      icon={AppWindow}
      iconColor="text-[#ff6b6b]"
      iconBgColor="bg-[rgba(255,107,107,0.15)]"
      canEdit={false}
      isEditing={false}
      onEdit={() => {}}
      onSave={() => {}}
      onCancel={() => {}}
    >
      <div className="space-y-2">
        <div className="flex items-baseline gap-2">
          <span className="text-[24px] font-semibold text-[#f5f7fb]">{exclusionsCount}</span>
          <span className="text-[14px] text-[rgba(245,247,251,0.5)]">
            {exclusionsCount === 1 ? 'aplicativo' : 'aplicativos'}
          </span>
        </div>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)]">{t('policies.appExclusions.subtitle')}</p>
        <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
          {exclusionsCount > 0 ? t('policies.appExclusions.subtitle') : t('policies.appExclusions.empty')}
        </p>
      </div>
    </PolicyCardShell>
  );
}
