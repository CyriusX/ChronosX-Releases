import { Globe, Target } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { LocalSettings, UpdateLocalSettingsRequest, AppLanguage } from '../../types/settings';
import { useLanguage } from '../../hooks/useLanguage';

const LANGUAGE_OPTIONS: { code: AppLanguage; label: string }[] = [
  { code: 'pt-BR', label: 'settings.general.ptBR' },
  { code: 'en-US', label: 'settings.general.enUS' },
  { code: 'fr-FR', label: 'settings.general.frFR' },
  { code: 'es-ES', label: 'settings.general.esES' },
];

const GOAL_OPTIONS = [
  { value: 3600, label: '1h' },
  { value: 7200, label: '2h' },
  { value: 14400, label: '4h' },
  { value: 21600, label: '6h' },
  { value: 28800, label: '8h' },
  { value: 36000, label: '10h' },
  { value: 43200, label: '12h' },
];

interface GeneralSectionProps {
  settings: LocalSettings;
  onUpdate: (updates: UpdateLocalSettingsRequest) => void;
}

export function GeneralSection({ settings, onUpdate }: GeneralSectionProps) {
  const { t } = useTranslation();
  const { changeLanguage } = useLanguage();
  const currentGoal = settings.workGoalSeconds ?? 28800;

  const handleLanguageChange = async (lang: AppLanguage) => {
    onUpdate({ language: lang });
    await changeLanguage(lang);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.general.title')}</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.general.subtitle')}
        </p>
      </div>

      {/* Language Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
            <Globe className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.general.language')}</h3>
        </div>

        <div className="grid grid-cols-2 gap-2">
          {LANGUAGE_OPTIONS.map((opt) => (
            <button
              key={opt.code}
              onClick={() => handleLanguageChange(opt.code)}
              className={`py-2 px-4 rounded-lg text-[13px] font-medium transition-all ${
                settings.language === opt.code
                  ? 'bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] text-white'
                  : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
              }`}
            >
              {t(opt.label)}
            </button>
          ))}
        </div>
      </div>

      {/* Work Goal Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#05df72] to-[#22D3EE] flex items-center justify-center">
            <Target className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.general.workGoal')}</h3>
        </div>

        <div className="space-y-3">
          <p className="text-[11px] text-[rgba(245,247,251,0.4)]">
            {t('settings.general.workGoalDescription')}
          </p>

          <div className="flex flex-wrap gap-2">
            {GOAL_OPTIONS.map((opt) => (
              <button
                key={opt.value}
                onClick={() => onUpdate({ workGoalSeconds: opt.value })}
                className={`px-3 py-1.5 rounded-lg text-[12px] font-medium transition-all ${
                  currentGoal === opt.value
                    ? 'bg-[rgba(5,223,114,0.2)] border border-[rgba(5,223,114,0.5)] text-[#05df72]'
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
