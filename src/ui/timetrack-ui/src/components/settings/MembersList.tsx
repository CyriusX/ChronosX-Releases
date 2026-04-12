import { useTranslation } from 'react-i18next';
import { Users } from 'lucide-react';
import { MemberCard } from './MemberCard';
import type { Member, UserRole } from '../../types/member';

interface MembersListProps {
  members: Member[];
  currentUserId?: string;
  isAdmin: boolean;
  onToggleStatus: (member: Member) => void;
  onChangeRole: (member: Member, role: UserRole) => void;
}

export function MembersList({
  members,
  currentUserId,
  isAdmin,
  onToggleStatus,
  onChangeRole,
}: MembersListProps) {
  const { t } = useTranslation();
  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl overflow-hidden">
      <div className="p-4 border-b border-[rgba(255,255,255,0.06)]">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#ff8904] to-[#f6339a] flex items-center justify-center">
            <Users className="w-4 h-4 text-white" />
          </div>
          <div>
            <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('settings.members.team')}</h3>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)]">
              {t('settings.members.memberCount', { count: members.length })}
            </p>
          </div>
        </div>
      </div>

      <div className="divide-y divide-[rgba(255,255,255,0.06)]">
        {members.length === 0 ? (
          <EmptyState />
        ) : (
          members.map((member) => (
            <MemberCard
              key={member.userId}
              member={member}
              currentUserId={currentUserId}
              isAdmin={isAdmin}
              onToggleStatus={onToggleStatus}
              onChangeRole={onChangeRole}
            />
          ))
        )}
      </div>
    </div>
  );
}

function EmptyState() {
  const { t } = useTranslation();
  return (
    <div className="p-8 text-center">
      <Users className="w-8 h-8 mx-auto text-[rgba(245,247,251,0.2)] mb-2" />
      <p className="text-[13px] text-[rgba(245,247,251,0.4)]">{t('settings.members.noMembers')}</p>
    </div>
  );
}
