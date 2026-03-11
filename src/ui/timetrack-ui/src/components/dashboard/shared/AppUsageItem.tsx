interface AppUsageItemProps {
  icon: React.ReactNode;
  label: string;
  subtext: string;
  time: string;
  color: string;
}

export function AppUsageItem({ icon, label, subtext, time, color }: AppUsageItemProps) {
  return (
    <div className="flex items-center gap-3">
      <div className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center">
        <span className="text-[rgba(245,247,251,0.8)]">{icon}</span>
      </div>
      <div className="flex-1">
        <p className="text-[12px] font-medium text-[rgba(245,247,251,0.9)]">{label}</p>
        <div className="flex items-center gap-[6px]">
          <div
            className="w-[6px] h-[6px] rounded-full"
            style={{ backgroundColor: color, boxShadow: `0px 0px 4px 0px ${color}` }}
          />
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">{subtext}</span>
        </div>
      </div>
      <span className="text-[12px] text-[rgba(245,247,251,0.4)]">{time}</span>
    </div>
  );
}
