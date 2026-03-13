import { Building2, Clock, Ban, Database, AppWindow } from 'lucide-react';
import type { OrgPolicyResponse } from '../../types/settings';

interface PolicyCardsProps {
  policy: OrgPolicyResponse;
}

/**
 * PolicyCards - Cards individuais para cada política da organização
 *
 * Layout responsivo em grid para ocupar toda a tela.
 * Cada política tem seu próprio card com visual consistente.
 */
export function PolicyCards({ policy }: PolicyCardsProps) {
  const formatWorkHours = () => {
    const { startTime, endTime } = policy.workHours;
    return `${startTime} - ${endTime}`;
  };

  const formatWorkDays = () => {
    const dayNames: Record<string, string> = {
      monday: 'Seg',
      tuesday: 'Ter',
      wednesday: 'Qua',
      thursday: 'Qui',
      friday: 'Sex',
      saturday: 'Sáb',
      sunday: 'Dom',
    };
    return policy.workHours.days
      .map((d) => dayNames[d] || d)
      .join(', ');
  };

  const formatIdleThreshold = () => {
    const minutes = Math.floor(policy.idleThresholdSeconds / 60);
    const seconds = policy.idleThresholdSeconds % 60;
    if (minutes > 0 && seconds > 0) {
      return `${minutes}min ${seconds}s`;
    }
    return minutes > 0 ? `${minutes} minutos` : `${seconds} segundos`;
  };

  const formatRetention = () => {
    const days = policy.retentionDays;
    if (days >= 365) {
      const years = Math.floor(days / 365);
      return `${years} ano${years > 1 ? 's' : ''}`;
    }
    if (days >= 30) {
      const months = Math.floor(days / 30);
      return `${months} ${months > 1 ? 'meses' : 'mês'}`;
    }
    return `${days} dias`;
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
            Configuradas pelo administrador • Versão {policy.version}
          </p>
        </div>
      </div>

      {/* Policy Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
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
              Fuso: {policy.workHours.timezone}
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

        {/* App Exclusions Card */}
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-[rgba(255,107,107,0.15)] flex items-center justify-center">
              <AppWindow className="w-5 h-5 text-[#ff6b6b]" />
            </div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Apps Excluídos</h3>
          </div>
          <div className="space-y-2">
            <div className="flex items-baseline gap-2">
              <span className="text-[24px] font-semibold text-[#f5f7fb]">
                {policy.appExclusions.length}
              </span>
              <span className="text-[14px] text-[rgba(245,247,251,0.5)]">
                {policy.appExclusions.length === 1 ? 'aplicativo' : 'aplicativos'}
              </span>
            </div>
            <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
              Não rastreados pelo sistema
            </p>
            <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
              {policy.appExclusions.length > 0
                ? 'Coleta ignorada para estes apps'
                : 'Nenhum app excluído'}
            </p>
          </div>
        </div>

        {/* Retention Card */}
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl bg-[rgba(5,223,114,0.15)] flex items-center justify-center">
              <Database className="w-5 h-5 text-[#05df72]" />
            </div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">Retenção de Dados</h3>
          </div>
          <div className="space-y-2">
            <div className="flex items-baseline gap-2">
              <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatRetention()}</span>
            </div>
            <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
              Período de armazenamento dos dados
            </p>
            <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
              Dados mais antigos são removidos automaticamente
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
