import { User, Mail, Shield, Building2, LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';

const ROLE_STYLES: Record<string, string> = {
  Admin: 'bg-[rgba(255,107,107,0.15)] text-[#ff6b6b]',
  Gestor: 'bg-[rgba(243,208,93,0.15)] text-[#f3d05d]',
  Colaborador: 'bg-[rgba(139,92,246,0.15)] text-[#8B5CF6]',
};

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Administrador',
  Gestor: 'Gestor',
  Colaborador: 'Colaborador',
};

export function ProfileSection() {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  if (!user) return null;

  const initial = user.displayName?.charAt(0)?.toUpperCase() || '?';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Perfil</h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          Suas informações de conta e organização
        </p>
      </div>

      {/* Profile Card */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-6">
        {/* Avatar + Name */}
        <div className="flex items-center gap-4 mb-6">
          <div className="w-16 h-16 rounded-full bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center text-white text-[24px] font-semibold flex-shrink-0">
            {initial}
          </div>
          <div className="flex-1 min-w-0">
            <h3 className="text-[18px] font-semibold text-[#f5f7fb] truncate">
              {user.displayName}
            </h3>
            <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-medium mt-1 ${ROLE_STYLES[user.role] || ROLE_STYLES.Colaborador}`}>
              <Shield className="w-3 h-3" />
              {ROLE_LABELS[user.role] || user.role}
            </span>
          </div>
        </div>

        {/* Info Rows */}
        <div className="space-y-3">
          <div className="flex items-center gap-3 py-2.5 border-b border-[rgba(255,255,255,0.04)]">
            <Mail className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            <div className="flex-1 min-w-0">
              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">E-mail</p>
              <p className="text-[13px] text-[rgba(245,247,251,0.9)] truncate">{user.email}</p>
            </div>
          </div>

          <div className="flex items-center gap-3 py-2.5 border-b border-[rgba(255,255,255,0.04)]">
            <User className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            <div className="flex-1 min-w-0">
              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Nome de exibição</p>
              <p className="text-[13px] text-[rgba(245,247,251,0.9)] truncate">{user.displayName}</p>
            </div>
          </div>

          <div className="flex items-center gap-3 py-2.5">
            <Building2 className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            <div className="flex-1 min-w-0">
              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">Organização</p>
              <p className="text-[13px] text-[rgba(245,247,251,0.9)] truncate">{user.orgName}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Logout */}
      <button
        onClick={handleLogout}
        className="w-full flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-[rgba(248,113,113,0.06)] border border-[rgba(248,113,113,0.15)] text-[#f87171] text-[13px] font-medium hover:bg-[rgba(248,113,113,0.12)] transition-colors"
      >
        <LogOut className="w-4 h-4" />
        Sair da conta
      </button>
    </div>
  );
}
