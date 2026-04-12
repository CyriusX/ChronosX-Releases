import { useTranslation } from 'react-i18next';
import { Building2 } from 'lucide-react';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest } from '../../types/settings';
import { usePolicyCards } from './usePolicyCards';
import { WorkHoursCard } from './WorkHoursCard';
import { IdleThresholdCard } from './IdleThresholdCard';
import { AppExclusionsCard } from './AppExclusionsCard';
import { RetentionCard } from './RetentionCard';
import { FocusModeCard } from './FocusModeCard';

interface PolicyCardsProps {
  policy: OrgPolicyResponse;
  onUpdate?: (request: UpdateOrgPolicyRequest) => Promise<void>;
  canEdit?: boolean;
}

/**
 * PolicyCards - Container para cards de políticas da organização
 *
 * SRP: Apenas orquestra a exibição dos cards de política
 * OCP: Novos cards podem ser adicionados sem modificar este componente
 *
 * Composition:
 * - Composição com cards individuais via children
 * - Delega estado para hook customizado
 */
export function PolicyCards({ policy, onUpdate, canEdit = false }: PolicyCardsProps) {
  const { t } = useTranslation();
  const {
    editingCard,
    isSaving,
    workHours,
    idleThresholdMinutes,
    retentionDays,
    focusMode,
    setEditingCard,
    setWorkHours,
    setIdleThresholdMinutes,
    setRetentionDays,
    setFocusMode,
    handleToggleDay,
    handleSave,
    handleCancel,
  } = usePolicyCards(policy);

  return (
    <div className="space-y-6">
      {/* Section Header */}
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8b7aff] to-[#6366f1] flex items-center justify-center">
          <Building2 className="w-4 h-4 text-white" />
        </div>
        <div>
          <h2 className="text-[18px] font-semibold text-[#f5f7fb]">{t('policies.orgPolicies.title')}</h2>
          <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
            {t('policies.appCategories.configuredByAdmin')} • {t('policies.appCategories.orgPoliciesVersion', { version: policy.version })}
          </p>
        </div>
      </div>

      {/* Policy Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <WorkHoursCard
          startTime={workHours.startTime}
          endTime={workHours.endTime}
          days={workHours.days}
          timezone={policy.workHours.timezone}
          isEditing={editingCard === 'workHours'}
          canEdit={canEdit}
          onEdit={() => setEditingCard('workHours')}
          onSave={() => handleSave('workHours', onUpdate)}
          onCancel={() => handleCancel('workHours')}
          isSaving={isSaving}
          onStartTimeChange={(value) => setWorkHours((prev) => ({ ...prev, startTime: value }))}
          onEndTimeChange={(value) => setWorkHours((prev) => ({ ...prev, endTime: value }))}
          onToggleDay={handleToggleDay}
        />

        <IdleThresholdCard
          thresholdSeconds={policy.idleThresholdSeconds}
          thresholdMinutes={idleThresholdMinutes}
          isEditing={editingCard === 'idleThreshold'}
          canEdit={canEdit}
          onEdit={() => setEditingCard('idleThreshold')}
          onSave={() => handleSave('idleThreshold', onUpdate)}
          onCancel={() => handleCancel('idleThreshold')}
          isSaving={isSaving}
          onThresholdChange={setIdleThresholdMinutes}
        />

        <AppExclusionsCard exclusionsCount={policy.appExclusions.length} />

        <RetentionCard
          retentionDays={retentionDays}
          isEditing={editingCard === 'retention'}
          canEdit={canEdit}
          onEdit={() => setEditingCard('retention')}
          onSave={() => handleSave('retention', onUpdate)}
          onCancel={() => handleCancel('retention')}
          isSaving={isSaving}
          onRetentionChange={setRetentionDays}
        />
      </div>

      {/* Focus Mode Section - Full width below the grid */}
      <FocusModeCard
        focusMode={focusMode}
        isEditing={editingCard === 'focusMode'}
        canEdit={canEdit}
        onEdit={() => setEditingCard('focusMode')}
        onSave={() => handleSave('focusMode', onUpdate)}
        onCancel={() => handleCancel('focusMode')}
        isSaving={isSaving}
        onFocusModeChange={setFocusMode}
      />
    </div>
  );
}
