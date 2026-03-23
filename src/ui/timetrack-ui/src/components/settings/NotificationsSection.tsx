import { Bell } from 'lucide-react';
import { Switch } from '../ui/switch';
import type { LocalSettings, UpdateLocalSettingsRequest } from '../../types/settings';

interface NotificationsSectionProps {
  settings: LocalSettings;
  onUpdate: (updates: UpdateLocalSettingsRequest) => void;
}

export function NotificationsSection({ settings, onUpdate }: NotificationsSectionProps) {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Notificações</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          Configure alertas e sons de notificação
        </p>
      </div>

      {/* Notifications Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center">
            <Bell className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">Alertas</h3>
        </div>

        <div className="space-y-4">
          {/* Auto-resume notification toggle */}
          <div className="flex items-center justify-between py-2">
            <div className="flex-1 pr-4">
              <p className="text-[13px] text-[rgba(245,247,251,0.9)]">
                Aviso de retomada automática
              </p>
              <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
                Notificar quando o tracking estiver pausado por muito tempo
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
                Sons de notificação
              </p>
              <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
                Reproduzir sons ao receber notificações
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
    </div>
  );
}
