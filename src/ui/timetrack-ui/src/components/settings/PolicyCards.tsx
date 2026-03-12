import { Building2, Clock, Ban, Focus } from 'lucide-react';
import type { OrgPolicies } from '../../types/settings';

interface PolicyCardsProps {
  policies: OrgPolicies;
}

/**
 * PolicyCards - Cards individuais para cada política da organização
 *
 * Layout responsivo em grid para ocupar toda a tela.
 * Cada política tem seu próprio card com visual consistente.
 */
export function PolicyCards({ policies }: PolicyCardsProps) {
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
    return minutes > 0 ? `${minutes} minutos` : `${seconds} segundos`;
  };

  return (
    <div className="space-y-6">
      {/* Section Header */}
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8b7aff] to-[#6366f1] flex items-center justify-center">
          <Building2 className="w-4 h-4 text-white" />
        </div>
        <div>
          <h2 className="text-[18px] font-semibold text-[#f5f7fb]">Políticas da Organização</h2>
          <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
            Configuradas pelo administrador • Apenas leitura
          </p>
        </div>
      </div>

      {/* Policy Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {/* Work Hours Card */}
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-[rgba(74,217,255,0.15)] flex items-center justify-center">
              <Clock className="w-5 h-5 text-[#4ad9ff]" />
            </div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Horário de Trabalho</h3>
          </div>
          <div className="space-y-2">
            <div className="flex items-baseline gap-2">
              <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatWorkHours()}</span>
            </div>
            <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
              {formatWorkDays()}
            </p>
            <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
              Fuso: {policies.workHours.timezone}
            </p>
          </div>
        </div>

        {/* Idle Threshold Card */}
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-[rgba(243,208,93,0.15)] flex items-center justify-center">
              <Ban className="w-5 h-5 text-[#f3d05d]" />
            </div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Threshold de Inatividade</h3>
          </div>
          <div className="space-y-2">
            <div className="flex items-baseline gap-2">
              <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatIdleThreshold()}</span>
            </div>
            <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
              Tempo sem atividade para pausa automática
            </p>
            <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
              Tracking pausa automaticamente após este período
            </p>
          </div>
        </div>

        {/* Focus Mode Card */}
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-[rgba(5,223,114,0.15)] flex items-center justify-center">
              <Focus className="w-5 h-5 text-[#05df72]" />
            </div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Modo de Foco</h3>
          </div>
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <div className={`w-2 h-2 rounded-full ${policies.focusMode.enabled ? 'bg-[#05df72]' : 'bg-[rgba(245,247,251,0.2)]'}`} />
              <span className="text-[24px] font-semibold text-[#f5f7fb]">
                {policies.focusMode.enabled ? 'Ativo' : 'Desativado'}
              </span>
            </div>
            {policies.focusMode.enabled && policies.focusMode.mode && (
              <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
                {policies.focusMode.mode === 'pomodoro' ? 'Técnica Pomodoro' : 'Ciclo Ultradiano'}
              </p>
            )}
            <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
              Sessões focadas com pausas programadas
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
