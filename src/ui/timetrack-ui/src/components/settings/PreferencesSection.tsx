import { Bell, Globe } from 'lucide-react';
import { Switch } from '../ui/switch';
import type { LocalSettings, UpdateLocalSettingsRequest } from '../../types/settings';

interface PreferencesSectionProps {
  settings: LocalSettings;
  onUpdate: (updates: UpdateLocalSettingsRequest) => void;
}

/**
 * PreferencesSection - Aba de preferências pessoais do colaborador
 *
 * Segue identidade visual:
 * - Background: #0b0d14
 * - Cards: gradient from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)]
 * - Border: border-[rgba(255,255,255,0.06)]
 * - Accent: gradient from-[#4ad9ff] to-[#3c7bff]
 */
export function PreferencesSection({ settings, onUpdate }: PreferencesSectionProps) {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Preferências</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          Configurações pessoais que afetam apenas sua experiência
        </p>
      </div>

      {/* Notifications Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center">
            <Bell className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">Notificações</h3>
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

      {/* Language Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
            <Globe className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">Idioma</h3>
        </div>

        <div className="flex gap-2">
          <button
            onClick={() => onUpdate({ language: 'pt-BR' })}
            className={`flex-1 py-2 px-4 rounded-lg text-[13px] font-medium transition-all ${
              settings.language === 'pt-BR'
                ? 'bg-gradient-to-r from-[#4ad9ff] to-[#3c7bff] text-white'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            Português (BR)
          </button>
          <button
            onClick={() => onUpdate({ language: 'en-US' })}
            className={`flex-1 py-2 px-4 rounded-lg text-[13px] font-medium transition-all ${
              settings.language === 'en-US'
                ? 'bg-gradient-to-r from-[#4ad9ff] to-[#3c7bff] text-white'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            English (US)
          </button>
        </div>
      </div>
    </div>
  );
}
