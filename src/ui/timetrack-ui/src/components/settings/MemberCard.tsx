import { Crown, Shield, User, Ban, CheckCircle } from 'lucide-react';
import type { Member, UserRole } from '../../types/member';

interface MemberCardProps {
  member: Member;
  currentUserId?: string;
  isAdmin: boolean;
  onToggleStatus: (member: Member) => void;
  onChangeRole: (member: Member, role: UserRole) => void;
}

const getRoleIcon = (role: UserRole) => {
  switch (role) {
    case 'Admin':
      return <Crown className="w-3.5 h-3.5 text-[#f6339a]" />;
    case 'Gestor':
      return <Shield className="w-3.5 h-3.5 text-[#4ad9ff]" />;
    default:
      return <User className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />;
  }
};

const getRoleBadgeStyle = (role: UserRole) => {
  switch (role) {
    case 'Admin':
      return 'bg-[rgba(246,51,154,0.15)] text-[#f6339a]';
    case 'Gestor':
      return 'bg-[rgba(74,217,255,0.15)] text-[#4ad9ff]';
    default:
      return 'bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)]';
  }
};

export function MemberCard({
  member,
  currentUserId,
  isAdmin,
  onToggleStatus,
  onChangeRole,
}: MemberCardProps) {
  const isCurrentUser = member.userId === currentUserId;
  const canManage = isAdmin && !isCurrentUser;

  return (
    <div className="flex items-center justify-between p-4 hover:bg-[rgba(255,255,255,0.02)] transition-colors">
      <div className="flex items-center gap-3">
        <div className="w-9 h-9 rounded-full bg-gradient-to-br from-[#4ad9ff] to-[#3c7bff] flex items-center justify-center text-white text-[13px] font-medium">
          {member.displayName.charAt(0).toUpperCase()}
        </div>
        <div>
          <div className="flex items-center gap-2">
            <p className="text-[13px] font-medium text-[#f5f7fb]">
              {member.displayName}
            </p>
            {isCurrentUser && (
              <span className="text-[10px] px-1.5 py-0.5 bg-[rgba(74,217,255,0.15)] text-[#4ad9ff] rounded">
                Você
              </span>
            )}
            {member.status === 'Inactive' && (
              <span className="text-[10px] px-1.5 py-0.5 bg-[rgba(255,100,100,0.15)] text-[#ff6464] rounded">
                Inativo
              </span>
            )}
          </div>
          <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{member.email}</p>
        </div>
      </div>

      <div className="flex items-center gap-3">
        {canManage ? (
          <select
            value={member.role}
            onChange={(e) => onChangeRole(member, e.target.value as UserRole)}
            className={`px-2 py-1 rounded-lg text-[11px] font-medium ${getRoleBadgeStyle(member.role)} bg-transparent border-none cursor-pointer focus:outline-none`}
          >
            <option value="Colaborador">Colaborador</option>
            <option value="Gestor">Gestor</option>
            <option value="Admin">Admin</option>
          </select>
        ) : (
          <span
            className={`flex items-center gap-1.5 px-2 py-1 rounded-lg text-[11px] font-medium ${getRoleBadgeStyle(member.role)}`}
          >
            {getRoleIcon(member.role)}
            {member.role}
          </span>
        )}

        {canManage && (
          <button
            onClick={() => onToggleStatus(member)}
            className={`p-2 rounded-lg transition-colors ${
              member.status === 'Active'
                ? 'text-[rgba(255,100,100,0.6)] hover:bg-[rgba(255,100,100,0.1)]'
                : 'text-[rgba(100,255,100,0.6)] hover:bg-[rgba(100,255,100,0.1)]'
            }`}
            title={member.status === 'Active' ? 'Desativar' : 'Ativar'}
          >
            {member.status === 'Active' ? (
              <Ban className="w-4 h-4" />
            ) : (
              <CheckCircle className="w-4 h-4" />
            )}
          </button>
        )}
      </div>
    </div>
  );
}
