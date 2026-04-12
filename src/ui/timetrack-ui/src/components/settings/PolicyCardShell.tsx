import { useTranslation } from 'react-i18next';
import { Pencil, Check, X, LucideIcon } from 'lucide-react';

interface PolicyCardShellProps {
  title: string;
  icon: LucideIcon;
  iconColor: string;
  iconBgColor: string;
  canEdit?: boolean;
  isEditing: boolean;
  onEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
  children: React.ReactNode;
}

/**
 * PolicyCardShell - Componente base para cards de política
 *
 * SRP: Apenas fornece estrutura visual consistente para cards de política
 */
export function PolicyCardShell({
  title,
  icon: Icon,
  iconColor,
  iconBgColor,
  canEdit = false,
  isEditing,
  onEdit,
  onSave,
  onCancel,
  isSaving = false,
  children,
}: PolicyCardShellProps) {
  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className={`w-10 h-10 rounded-xl ${iconBgColor} flex items-center justify-center`}>
            <Icon className={`w-5 h-5 ${iconColor}`} />
          </div>
          <h3 className="text-[14px] font-medium text-[#f5f7fb]">{title}</h3>
        </div>
        {canEdit && (
          isEditing ? (
            <SaveCancelButton onSave={onSave} onCancel={onCancel} isSaving={isSaving} />
          ) : (
            <EditButton onEdit={onEdit} />
          )
        )}
      </div>
      {children}
    </div>
  );
}

interface EditButtonProps {
  onEdit: () => void;
}

function EditButton({ onEdit }: EditButtonProps) {
  const { t } = useTranslation();
  return (
    <button
      onClick={onEdit}
      className="w-7 h-7 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
      title={t('common.edit')}
    >
      <Pencil className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
    </button>
  );
}

interface SaveCancelButtonProps {
  onSave: () => void;
  onCancel: () => void;
  isSaving?: boolean;
}

function SaveCancelButton({ onSave, onCancel, isSaving }: SaveCancelButtonProps) {
  const { t } = useTranslation();
  return (
    <div className="flex items-center gap-2">
      <button
        onClick={onSave}
        disabled={isSaving}
        className="w-7 h-7 rounded-lg bg-[rgba(5,223,114,0.15)] border border-[rgba(5,223,114,0.3)] flex items-center justify-center hover:bg-[rgba(5,223,114,0.25)] transition-colors disabled:opacity-50"
        title={isSaving ? t('common.saving') : t('common.save')}
      >
        <Check className="w-3.5 h-3.5 text-[#05df72]" />
      </button>
      <button
        onClick={onCancel}
        disabled={isSaving}
        className="w-7 h-7 rounded-lg bg-[rgba(255,107,107,0.15)] border border-[rgba(255,107,107,0.3)] flex items-center justify-center hover:bg-[rgba(255,107,107,0.25)] transition-colors disabled:opacity-50"
        title={t('common.cancel')}
      >
        <X className="w-3.5 h-3.5 text-[#ff6b6b]" />
      </button>
    </div>
  );
}
