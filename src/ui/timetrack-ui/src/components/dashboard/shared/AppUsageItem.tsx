interface AppUsageItemProps {
  icon: React.ReactNode;
  label: string;
  subtext: string;
  time: string;
  color: string;
}

export function AppUsageItem({ icon, label, subtext, time, color }: AppUsageItemProps) {
  return (
    <div className="flex items-center gap-2.5 min-w-0">
      <div className="w-7 h-7 rounded-lg bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center flex-shrink-0">
        <span className="text-[rgba(245,247,251,0.7)]">{icon}</span>
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-[11px] font-medium text-[rgba(245,247,251,0.9)] truncate">{label}</p>
        <div className="flex items-center gap-1.5">
          <div
            className="w-[5px] h-[5px] rounded-full flex-shrink-0"
            style={{ backgroundColor: color, boxShadow: `0px 0px 3px 0px ${color}` }}
          />
          <span className="text-[9px] text-[rgba(245,247,251,0.4)]">{subtext}</span>
        </div>
      </div>
      <span className="text-[11px] text-[rgba(245,247,251,0.4)] flex-shrink-0">{time}</span>
    </div>
  );
}
