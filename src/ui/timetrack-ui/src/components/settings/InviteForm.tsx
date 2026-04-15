import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Mail, Loader2 } from 'lucide-react';
import type { UserRole } from '../../types/member';

interface InviteFormProps {
  onInvite: (email: string, displayName: string, role: UserRole) => Promise<void>;
  onSuccess: () => void;
  onCancel: () => void;
}

export function InviteForm({ onInvite, onSuccess, onCancel }: InviteFormProps) {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [role, setRole] = useState<UserRole>('Colaborador');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      await onInvite(email, displayName, role);
      onSuccess();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('settings.members.inviteError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center">
          <Mail className="w-4 h-4 text-white" />
        </div>
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.members.newInvite')}</h3>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-[11px] text-[rgba(245,247,251,0.5)] mb-1.5">
              {t('settings.members.name')}
            </label>
            <input
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder={t('settings.members.namePlaceholder')}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] placeholder:text-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#8B5CF6]"
              required
            />
          </div>
          <div>
            <label className="block text-[11px] text-[rgba(245,247,251,0.5)] mb-1.5">
              {t('settings.members.email')}
            </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder={t('settings.members.emailPlaceholder')}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] placeholder:text-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#8B5CF6]"
              required
            />
          </div>
        </div>

        <div>
          <label className="block text-[11px] text-[rgba(245,247,251,0.5)] mb-1.5">
            {t('settings.members.role')}
          </label>
          <div className="flex gap-2">
            {(['Colaborador', 'Gestor', 'Admin'] as UserRole[]).map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => setRole(r)}
                className={`flex-1 py-2 px-4 rounded-lg text-[13px] font-medium transition-all ${
                  role === r
                    ? 'bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] text-white'
                    : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)]'
                }`}
              >
                {r}
              </button>
            ))}
          </div>
        </div>

        {error && (
          <p className="text-[12px] text-[#ff6464]">{error}</p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            onClick={onCancel}
            className="px-4 py-2 rounded-lg text-[13px] font-medium text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.9)] transition-colors"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={isSubmitting}
            className="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-lg text-[13px] font-medium text-white hover:opacity-90 transition-opacity disabled:opacity-50"
          >
            {isSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
            {t('settings.members.sendInvite')}
          </button>
        </div>
      </form>
    </div>
  );
}
