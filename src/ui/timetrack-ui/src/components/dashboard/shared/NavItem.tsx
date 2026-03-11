interface NavItemProps {
  icon: React.ReactNode;
  label: string;
  active?: boolean;
}

export function NavItem({ icon, label, active = false }: NavItemProps) {
  return (
    <button
      className={`w-full flex items-center gap-3 px-3 py-[10px] rounded-[10px] transition-colors ${
        active
          ? 'bg-[#1c1f2e] text-[#f5f7fb]'
          : 'text-[rgba(245,247,251,0.4)] hover:bg-[rgba(28,31,46,0.5)] hover:text-[rgba(245,247,251,0.7)]'
      }`}
    >
      {icon}
      <span className="text-[13px] font-medium">{label}</span>
    </button>
  );
}
