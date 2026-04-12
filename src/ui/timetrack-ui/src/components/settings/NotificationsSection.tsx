import { Bell, Timer } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Switch } from '../ui/switch';
import type { LocalSettings, UpdateLocalSettingsRequest } from '../../types/settings';

const IDLE_OPTIONS = [
  { value: 60, label: '1 min' },
  { value: 120, label: '2 min' },
  { value: 180, label: '3 min' },
  { value: 300, label: '5 min' },
  { value: 600, label: '10 min' },
  { value: 900, label: '15 min' },
  { value: 1800, label: '30 min' },
  { value: 3600, label: '60 min' },
];

interface NotificationsSectionProps {
  settings: LocalSettings;
  onUpdate: (updates: UpdateLocalSettingsRequest) => void;
}

export function NotificationsSection({ settings, onUpdate }: NotificationsSectionProps) {
  const { t } = useTranslation();
  const currentIdle = settings.idleThresholdSeconds ?? 300;

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

          <div className="flex flex-wrap gap-2 mt-2">
            {IDLE_OPTIONS.map((opt) => (
              <button
                key={opt.value}
                onClick={() => onUpdate({ idleThresholdSeconds: opt.value })}
                className={`px-3 py-1.5 rounded-lg text-[12px] font-medium transition-all ${
                  currentIdle === opt.value
                    ? 'bg-[rgba(139,92,246,0.2)] border border-[rgba(139,92,246,0.5)] text-[#8B5CF6]'
                    : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
                }`}
              >
                {opt.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
