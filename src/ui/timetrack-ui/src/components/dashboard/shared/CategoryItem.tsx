interface CategoryItemProps {
  percentage: number;
  icon: React.ReactNode;
  label: string;
  time: string;
  color: string;
}

export function CategoryItem({ percentage, icon, label, time, color }: CategoryItemProps) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-[12px] text-[rgba(245,247,251,0.4)] w-7 text-right">{Math.round(percentage)}%</span>
      <div
        className="w-5 h-5 rounded-full flex items-center justify-center"
        style={{ backgroundColor: `${color}20`, border: `1px solid ${color}50` }}
      >
        <span style={{ color }}>{icon}</span>
      </div>
      <span className="text-[12px] text-[rgba(245,247,251,0.8)] flex-1">{label}</span>
      <span className="text-[12px] text-[rgba(245,247,251,0.4)]">{time}</span>
    </div>
  );
}
