import { useState } from 'react';
import { Building2, Clock, Ban, Database, AppWindow, Pencil, Check, X } from 'lucide-react';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest, DayOfWeek } from '../../types/settings';

interface PolicyCardsProps {
  policy: OrgPolicyResponse;
  onUpdate?: (request: UpdateOrgPolicyRequest) => Promise<void>;
  canEdit?: boolean;
}

const DAYS_OF_WEEK: { value: DayOfWeek; label: string }[] = [
  { value: 'monday', label: 'Seg' },
  { value: 'tuesday', label: 'Ter' },
  { value: 'wednesday', label: 'Qua' },
  { value: 'thursday', label: 'Qui' },
  { value: 'friday', label: 'Sex' },
  { value: 'saturday', label: 'Sáb' },
  { value: 'sunday', label: 'Dom' },
];

/**
 * PolicyCards - Cards individuais para cada política da organização
 *
 * Layout responsivo em grid para ocupar toda a tela.
 * Cada política tem seu próprio card com visual consistente.
 * Suporta modo de edição para administradores.
 */
export function PolicyCards({ policy, onUpdate, canEdit = false }: PolicyCardsProps) {
  const [editingCard, setEditingCard] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Form states
  const [workHours, setWorkHours] = useState({
    startTime: policy.workHours.startTime,
    endTime: policy.workHours.endTime,
    days: policy.workHours.days,
  });
  const [idleThresholdMinutes, setIdleThresholdMinutes] = useState(
    Math.floor(policy.idleThresholdSeconds / 60)
  );
  const [retentionDays, setRetentionDays] = useState(policy.retentionDays);

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

  const handleToggleDay = (day: DayOfWeek) => {
    setWorkHours((prev) => ({
      ...prev,
      days: prev.days.includes(day)
        ? prev.days.filter((d) => d !== day)
        : [...prev.days, day],
    }));
  };

  const handleSave = async (cardType: string) => {
    if (!onUpdate) return;

    setIsSaving(true);
    try {
      let request: UpdateOrgPolicyRequest = {};

      switch (cardType) {
        case 'workHours':
          request = {
            workHours: {
              startTime: workHours.startTime,
              endTime: workHours.endTime,
              days: workHours.days,
            },
          };
          break;
        case 'idleThreshold':
          request = {
            idleThresholdSeconds: idleThresholdMinutes * 60,
          };
          break;
        case 'retention':
          request = {
            retentionDays: retentionDays,
          };
          break;
      }

      await onUpdate(request);
      setEditingCard(null);
    } catch (error) {
      console.error('Failed to update policy:', error);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = (cardType: string) => {
    // Reset form state to original values
    switch (cardType) {
      case 'workHours':
        setWorkHours({
          startTime: policy.workHours.startTime,
          endTime: policy.workHours.endTime,
          days: policy.workHours.days,
        });
        break;
      case 'idleThreshold':
        setIdleThresholdMinutes(Math.floor(policy.idleThresholdSeconds / 60));
        break;
      case 'retention':
        setRetentionDays(policy.retentionDays);
        break;
    }
    setEditingCard(null);
  };

  const EditButton = ({ cardType }: { cardType: string }) => (
    <button
      onClick={() => setEditingCard(cardType)}
      className="w-7 h-7 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
      title="Editar"
    >
      <Pencil className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
    </button>
  );

  const SaveCancelButton = ({ cardType }: { cardType: string }) => (
    <div className="flex items-center gap-2">
      <button
        onClick={() => handleSave(cardType)}
        disabled={isSaving}
        className="w-7 h-7 rounded-lg bg-[rgba(5,223,114,0.15)] border border-[rgba(5,223,114,0.3)] flex items-center justify-center hover:bg-[rgba(5,223,114,0.25)] transition-colors disabled:opacity-50"
        title="Salvar"
      >
        <Check className="w-3.5 h-3.5 text-[#05df72]" />
      </button>
      <button
        onClick={() => handleCancel(cardType)}
        disabled={isSaving}
        className="w-7 h-7 rounded-lg bg-[rgba(255,107,107,0.15)] border border-[rgba(255,107,107,0.3)] flex items-center justify-center hover:bg-[rgba(255,107,107,0.25)] transition-colors disabled:opacity-50"
        title="Cancelar"
      >
        <X className="w-3.5 h-3.5 text-[#ff6b6b]" />
      </button>
    </div>
  );

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
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-[rgba(74,217,255,0.15)] flex items-center justify-center">
                <Clock className="w-5 h-5 text-[#4ad9ff]" />
              </div>
              <h3 className="text-[14px] font-medium text-[#f5f7fb]">Horário de Trabalho</h3>
            </div>
            {canEdit && (
              editingCard === 'workHours' ? (
                <SaveCancelButton cardType="workHours" />
              ) : (
                <EditButton cardType="workHours" />
              )
            )}
          </div>

          {editingCard === 'workHours' ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2">
                <div className="flex-1">
                  <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Início</label>
                  <input
                    type="time"
                    value={workHours.startTime}
                    onChange={(e) => setWorkHours((prev) => ({ ...prev, startTime: e.target.value }))}
                    className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
                  />
                </div>
                <div className="flex-1">
                  <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Fim</label>
                  <input
                    type="time"
                    value={workHours.endTime}
                    onChange={(e) => setWorkHours((prev) => ({ ...prev, endTime: e.target.value }))}
                    className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
                  />
                </div>
              </div>
              <div>
                <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-2 block">Dias de trabalho</label>
                <div className="flex flex-wrap gap-1.5">
                  {DAYS_OF_WEEK.map((day) => (
                    <button
                      key={day.value}
                      onClick={() => handleToggleDay(day.value)}
                      className={`px-2.5 py-1 rounded-md text-[11px] font-medium transition-colors ${
                        workHours.days.includes(day.value)
                          ? 'bg-[#4ad9ff] text-[#0a0c10]'
                          : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)]'
                      }`}
                    >
                      {day.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          ) : (
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
          )}
        </div>

        {/* Idle Threshold Card */}
        <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-[rgba(243,208,93,0.15)] flex items-center justify-center">
                <Ban className="w-5 h-5 text-[#f3d05d]" />
              </div>
              <h3 className="text-[14px] font-medium text-[#f5f7fb]">Threshold de Inatividade</h3>
            </div>
            {canEdit && (
              editingCard === 'idleThreshold' ? (
                <SaveCancelButton cardType="idleThreshold" />
              ) : (
                <EditButton cardType="idleThreshold" />
              )
            )}
          </div>

          {editingCard === 'idleThreshold' ? (
            <div className="space-y-3">
              <div>
                <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Tempo em minutos</label>
                <input
                  type="number"
                  min="1"
                  max="60"
                  value={idleThresholdMinutes}
                  onChange={(e) => setIdleThresholdMinutes(parseInt(e.target.value) || 1)}
                  className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#f3d05d]"
                />
              </div>
              <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
                Mín: 1 min • Máx: 60 min
              </p>
            </div>
          ) : (
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
          )}
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
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-[rgba(5,223,114,0.15)] flex items-center justify-center">
                <Database className="w-5 h-5 text-[#05df72]" />
              </div>
              <h3 className="text-[14px] font-medium text-[#f5f7fb]">Retenção de Dados</h3>
            </div>
            {canEdit && (
              editingCard === 'retention' ? (
                <SaveCancelButton cardType="retention" />
              ) : (
                <EditButton cardType="retention" />
              )
            )}
          </div>

          {editingCard === 'retention' ? (
            <div className="space-y-3">
              <div>
                <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-1 block">Dias de retenção</label>
                <input
                  type="number"
                  min="7"
                  max="365"
                  value={retentionDays}
                  onChange={(e) => setRetentionDays(parseInt(e.target.value) || 7)}
                  className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#05df72]"
                />
              </div>
              <p className="text-[11px] text-[rgba(245,247,251,0.35)]">
                Mín: 7 dias • Máx: 365 dias
              </p>
            </div>
          ) : (
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
          )}
        </div>
      </div>
    </div>
  );
}
