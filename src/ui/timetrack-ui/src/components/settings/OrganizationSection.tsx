import { Building2, Clock, Brain, Database, AppWindow } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest } from '../../types/settings';
import { usePolicyCards } from './usePolicyCards';
import { WorkHoursCard } from './WorkHoursCard';
import { IdleThresholdCard } from './IdleThresholdCard';
import { AppExclusionsCard } from './AppExclusionsCard';
import { RetentionCard } from './RetentionCard';
import { FocusModeCard } from './FocusModeCard';
import { AppCategoriesSection } from './AppCategoriesSection';

interface OrganizationSectionProps {
  policy: OrgPolicyResponse;
  onUpdate: (request: UpdateOrgPolicyRequest) => Promise<void>;
  canEdit: boolean;
  orgId: string;
}

export function OrganizationSection({ policy, onUpdate, canEdit, orgId }: OrganizationSectionProps) {
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
    <div className="space-y-8">
      {/* Header */}
      <div>
        <div className="flex items-center gap-3 mb-1">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8b7aff] to-[#6366f1] flex items-center justify-center">
            <Building2 className="w-4 h-4 text-white" />
          </div>
          <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.organization.title')}</h2>
        </div>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1 ml-11">
          {t('settings.organization.subtitle')} &bull; Versão {policy.version}
        </p>
      </div>

      {/* App Categories Sub-section — first because it's the primary admin action */}
      <div className="space-y-4">
        <SubSectionHeader icon={AppWindow} label={t('policies.appCategories.title')} />
        <p className="text-[12px] text-[rgba(245,247,251,0.35)] -mt-2 ml-6">
          Aplicativos usados pelos colaboradores são adicionados automaticamente.
          Classifique-os como Produtivo, Neutro ou Distração para refletir em toda a equipe.
        </p>
        <AppCategoriesSection orgId={orgId} />
      </div>

      {/* Work Schedule Sub-section */}
      <div className="space-y-4">
        <SubSectionHeader icon={Clock} label={t('policies.workHours.title')} />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
        </div>
      </div>

      {/* Focus Mode Policy Sub-section */}
      <div className="space-y-4">
        <SubSectionHeader icon={Brain} label={t('policies.focusMode.title')} />
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

      {/* Data & Privacy Sub-section */}
      <div className="space-y-4">
        <SubSectionHeader icon={Database} label={t('policies.retention.title')} />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
          <AppExclusionsCard exclusionsCount={policy.appExclusions.length} />
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Sub-section Header
// ============================================================================

function SubSectionHeader({ icon: Icon, label }: { icon: typeof Clock; label: string }) {
  return (
    <div className="flex items-center gap-2">
      <Icon className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
      <h3 className="text-[14px] font-medium text-[rgba(245,247,251,0.7)]">{label}</h3>
    </div>
  );
}
