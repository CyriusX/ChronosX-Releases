import { Building2, Clock, Ban, Focus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { OrgPolicies } from '../../types/settings';

interface OrgPoliciesCardProps {
  policies: OrgPolicies;
}

/**
 * OrgPoliciesCard - Exibe políticas da organização (read-only)
 *
 * O colaborador pode VER mas não pode EDITAR estas configurações.
 * Elas são definidas pelo Admin e sincronizadas via cloud.
 */
export function OrgPoliciesCard({ policies }: OrgPoliciesCardProps) {
  const { t } = useTranslation();
  const formatWorkHours = () => {
    const { startHour, endHour } = policies.workHours;
    return `${String(startHour).padStart(2, '0')}:00 - ${String(endHour).padStart(2, '0')}:00`;
  };

  const formatWorkDays = () => {
    const dayNames = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
    return policies.workHours.workDays
      .map((d) => dayNames[d])
      .join(', ');
  };

  const formatIdleThreshold = () => {
    const minutes = Math.floor(policies.idleThresholdSeconds / 60);
    const seconds = policies.idleThresholdSeconds % 60;
    if (minutes > 0 && seconds > 0) {
      return `${minutes}min ${seconds}s`;
    }
    return minutes > 0 ? `${minutes} ${t('policies.idleThreshold.minutes')}` : `${seconds} ${t('policies.idleThreshold.seconds')}`;
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      {/* Header */}
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8b7aff] to-[#6366f1] flex items-center justify-center">
          <Building2 className="w-4 h-4 text-white" />
        </div>
        <div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('policies.orgPolicies.title')}</h3>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)]">
            {t('policies.orgPolicies.subtitle')}
          </p>
        </div>
      </div>

      {/* Policies List */}
      <div className="space-y-3">
        {/* Work Hours */}
        <div className="flex items-start gap-3 py-2">
          <div className="w-7 h-7 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center shrink-0 mt-0.5">
            <Clock className="w-3.5 h-3.5 text-[#8B5CF6]" />
          </div>
          <div className="flex-1">
            <p className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('policies.workHours.title')}</p>
            <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">
              {formatWorkHours()} • {formatWorkDays()}
            </p>
          </div>
        </div>

        {/* Divider */}
        <div className="h-px bg-[rgba(255,255,255,0.04)]" />

        {/* Idle Threshold */}
        <div className="flex items-start gap-3 py-2">
          <div className="w-7 h-7 rounded-lg bg-[rgba(243,208,93,0.15)] flex items-center justify-center shrink-0 mt-0.5">
            <Ban className="w-3.5 h-3.5 text-[#f3d05d]" />
          </div>
          <div className="flex-1">
            <p className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('policies.idleThreshold.title')}</p>
            <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">
              {formatIdleThreshold()} sem atividade
            </p>
          </div>
        </div>

        {/* Divider */}
        <div className="h-px bg-[rgba(255,255,255,0.04)]" />

        {/* Focus Mode */}
        <div className="flex items-start gap-3 py-2">
          <div className="w-7 h-7 rounded-lg bg-[rgba(5,223,114,0.15)] flex items-center justify-center shrink-0 mt-0.5">
            <Focus className="w-3.5 h-3.5 text-[#05df72]" />
          </div>
          <div className="flex-1">
            <p className="text-[13px] text-[rgba(245,247,251,0.9)]">{t('policies.focusMode.title')}</p>
            <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">
              {policies.focusMode.enabled
                ? `${t('policies.focusMode.enabled')} (${policies.focusMode.mode === 'pomodoro' ? t('policies.focusMode.pomodoro') : t('policies.focusMode.ultradian')})`
                : t('policies.focusMode.disabled')}
            </p>
          </div>
        </div>
      </div>

      {/* Footer Note */}
      <div className="mt-4 pt-3 border-t border-[rgba(255,255,255,0.04)]">
        <p className="text-[11px] text-[rgba(245,247,251,0.35)] italic">
          {t('policies.orgPolicies.subtitle')}
        </p>
      </div>
    </div>
  );
}
