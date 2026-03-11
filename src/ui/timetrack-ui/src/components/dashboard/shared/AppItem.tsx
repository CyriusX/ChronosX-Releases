interface AppItemProps {
  percentage: number;
  icon: React.ReactNode;
  label: string;
  time: string;
  color: string;
  borderColor?: string;
}

export function AppItem({ percentage, icon, label, time, color, borderColor }: AppItemProps) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-[12px] text-[rgba(245,247,251,0.4)] w-7 text-right">{percentage}%</span>
      <div
        className="w-6 h-6 rounded-lg flex items-center justify-center"
        style={{ backgroundColor: color, border: borderColor ? `1px solid ${borderColor}` : 'none' }}
      >
        <span className="text-[rgba(245,247,251,0.8)]">{icon}</span>
      </div>
      <span className="text-[12px] text-[rgba(245,247,251,0.8)] flex-1">{label}</span>
      <span className="text-[12px] text-[rgba(245,247,251,0.4)]">{time}</span>
    </div>
  );
}
