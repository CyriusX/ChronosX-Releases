interface ProjectItemProps {
  percentage: number;
  label: string;
  time: string;
  barColor: string;
  barWidth: number;
}

export function ProjectItem({ percentage, label, time, barColor, barWidth }: ProjectItemProps) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-[12px] text-[rgba(245,247,251,0.4)] w-7 text-right">{percentage}%</span>
      <div className="flex-1">
        <div className="flex items-center justify-between mb-1">
          <span className="text-[12px] text-[rgba(245,247,251,0.8)]">{label}</span>
          <span className="text-[12px] text-[rgba(245,247,251,0.4)]">{time}</span>
        </div>
        <div className="h-[6px] bg-[rgba(255,255,255,0.04)] rounded-full overflow-hidden">
          <div
            className="h-full rounded-full"
            style={{ width: `${barWidth}%`, backgroundColor: barColor, boxShadow: `0px 0px 6px 0px ${barColor}50` }}
          />
        </div>
      </div>
    </div>
  );
}
