import { motion } from 'motion/react';
import { SPRING } from '../../../lib/animation';

interface NavItemProps {
  icon: React.ReactNode;
  label: string;
  active?: boolean;
  onClick?: () => void;
}

export function NavItem({ icon, label, active = false, onClick }: NavItemProps) {
  return (
    <motion.button
      onClick={onClick}
      whileHover={{ x: active ? 0 : 3 }}
      transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
      className={`w-full flex items-center gap-3 px-3 py-[10px] rounded-[12px] transition-colors relative ${
        active
          ? 'text-[#f5f7fb]'
          : 'text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)]'
      }`}
    >
      {active && (
        <motion.div
          layoutId="sidebar-active"
          className="absolute inset-0 bg-[rgba(139,92,246,0.10)] border border-[rgba(139,92,246,0.15)] rounded-[12px]"
          transition={{ type: 'spring', stiffness: SPRING.snappy.stiffness, damping: SPRING.snappy.damping }}
        />
      )}
      <span className="relative z-10">{icon}</span>
      <span className="relative z-10 text-[13px] font-medium">{label}</span>
    </motion.button>
  );
}
