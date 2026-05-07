import { Bell, Timer } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Switch } from '../ui/switch';
import type { LocalSettings, UpdateLocalSettingsRequest } from '../../types/settings';

interface NotificationsSectionProps {
  settings: LocalSettings;
  onUpdate: (updates: UpdateLocalSettingsRequest) => void;
  /** Organization idle threshold (authoritative). When null/undefined, we show a fallback. */
  orgIdleThresholdSeconds?: number | null;
  /** Whether the current user can edit org policies (Admin/Gestor) */
  canEditOrgPolicies?: boolean;
  /** Navigate user to Organization policies section */
  onEditOrgPolicies?: () => void;
}

export function NotificationsSection({
  settings,
  onUpdate,
  orgIdleThresholdSeconds,
  canEditOrgPolicies = false,
  onEditOrgPolicies,
}: NotificationsSectionProps) {
  const { t } = useTranslation();

  const formatIdle = (sec: number | null | undefined) => {
    if (!sec || sec <= 0) return '—';
    if (sec < 60) return `${sec}s`;
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    if (m < 60) return s > 0 ? `${m}m ${s}s` : `${m}m`;
    const h = Math.floor(m / 60);
    const rm = m % 60;
    return rm > 0 ? `${h}h ${rm}m` : `${h}h`;
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.notifications.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.notifications.subtitle')}
        </p>
      </div>

      {/* Notifications Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center">
            <Bell className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.notifications.alerts')}</h3>
        </div>

        <div className="space-y-4">
          {/* Auto-resume notification toggle */}
          <div className="flex items-center justify-between py-2">
            <div className="flex-1 pr-4">
              <p className="text-[13px] text-[rgba(245,247,251,0.9)]">
                {t('settings.notifications.autoResume')}
              </p>
              <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
                {t('settings.notifications.autoResumeDesc')}
              </p>
            </div>
            <Switch
              checked={settings.autoResumeNotificationEnabled}
              onCheckedChange={(checked) =>
                onUpdate({ autoResumeNotificationEnabled: checked })
              }
            />
          </div>

          {/* Divider */}
          <div className="h-px bg-[rgba(255,255,255,0.06)]" />

          {/* Notification sounds toggle */}
          <div className="flex items-center justify-between py-2">
            <div className="flex-1 pr-4">
              <p className="text-[13px] text-[rgba(245,247,251,0.9)]">
                {t('settings.notifications.sounds')}
              </p>
              <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
                {t('settings.notifications.soundsDesc')}
              </p>
            </div>
            <Switch
              checked={settings.notificationSoundsEnabled}
              onCheckedChange={(checked) =>
                onUpdate({ notificationSoundsEnabled: checked })
              }
            />
          </div>
        </div>
      </div>

      {/* Idle Threshold Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#f59e0b] to-[#ef4444] flex items-center justify-center">
            <Timer className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.notifications.idleDetection')}</h3>
        </div>

        <div className="space-y-3">
          <div>
            <p className="text-[13px] text-[rgba(245,247,251,0.9)]">
              {t('settings.notifications.idleThreshold')}
            </p>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
              {t('settings.notifications.idleThresholdDesc')}
            </p>
          </div>

          <div className="flex items-center justify-between gap-3 rounded-xl bg-[rgba(0,0,0,0.22)] border border-[rgba(255,255,255,0.06)] px-4 py-3">
            <div className="min-w-0">
              <p className="text-[11px] uppercase tracking-wider text-[rgba(245,247,251,0.35)]">{t('settings.organization.title')}</p>
              <p className="text-[13px] text-[rgba(245,247,251,0.85)] mt-0.5 truncate">
                {formatIdle(orgIdleThresholdSeconds)}
              </p>
              <p className="text-[10px] text-[rgba(245,247,251,0.35)] mt-0.5">
                {t('policies.idleThreshold.subtitle')}
              </p>
            </div>

            {canEditOrgPolicies && onEditOrgPolicies && (
              <button
                onClick={onEditOrgPolicies}
                className="shrink-0 px-3 py-2 rounded-lg text-[11px] font-semibold bg-[rgba(139,92,246,0.15)] border border-[rgba(139,92,246,0.25)] text-[#c4b5fd] hover:bg-[rgba(139,92,246,0.22)] transition-colors"
              >
                {t('common.edit')}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
