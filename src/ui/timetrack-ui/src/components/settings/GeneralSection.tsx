import { Globe, Target } from 'lucide-react';
import type { LocalSettings, UpdateLocalSettingsRequest } from '../../types/settings';

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
  const currentGoal = settings.workGoalSeconds ?? 28800;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Geral</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          Configurações gerais da interface
        </p>
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
                ? 'bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] text-white'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            Português (BR)
          </button>
          <button
            onClick={() => onUpdate({ language: 'en-US' })}
            className={`flex-1 py-2 px-4 rounded-lg text-[13px] font-medium transition-all ${
              settings.language === 'en-US'
                ? 'bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] text-white'
                : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            English (US)
          </button>
        </div>
      </div>

      {/* Work Goal Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#05df72] to-[#22D3EE] flex items-center justify-center">
            <Target className="w-4 h-4 text-white" />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">Meta diária</h3>
        </div>

        <div className="space-y-3">
          <p className="text-[11px] text-[rgba(245,247,251,0.4)]">
            O anel de progresso no Dashboard usa essa meta como referência
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
