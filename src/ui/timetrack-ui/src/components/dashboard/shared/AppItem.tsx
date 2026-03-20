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
    <div className="flex items-center gap-2 min-w-0">
      <span className="text-[11px] text-[rgba(245,247,251,0.4)] w-7 text-right flex-shrink-0">{Math.round(percentage)}%</span>
      <div
        className="w-5 h-5 rounded-md flex items-center justify-center flex-shrink-0"
        style={{ backgroundColor: color, border: borderColor ? `1px solid ${borderColor}` : 'none' }}
      >
        <span className="text-[rgba(245,247,251,0.8)]">{icon}</span>
      </div>
      <span className="text-[11px] text-[rgba(245,247,251,0.8)] flex-1 truncate min-w-0">{label}</span>
      <span className="text-[11px] text-[rgba(245,247,251,0.4)] flex-shrink-0">{time}</span>
    </div>
  );
}
