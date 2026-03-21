interface ProjectItemProps {
  percentage: number;
  label: string;
  time: string;
  barColor: string;
  barWidth: number;
  barRef?: (el: HTMLDivElement | null) => void;
}

export function ProjectItem({ percentage, label, time, barColor, barWidth, barRef }: ProjectItemProps) {
  return (
    <div className="flex items-center gap-2 min-w-0">
      <span className="text-[11px] text-[rgba(245,247,251,0.4)] w-7 text-right flex-shrink-0">{Math.round(percentage)}%</span>
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between mb-0.5">
          <span className="text-[11px] text-[rgba(245,247,251,0.8)] truncate">{label}</span>
          <span className="text-[11px] text-[rgba(245,247,251,0.4)] flex-shrink-0 ml-2">{time}</span>
        </div>
        <div className="h-[4px] bg-[rgba(255,255,255,0.04)] rounded-full overflow-hidden">
          <div
            ref={barRef}
            className="h-full rounded-full"
            style={{ width: barRef ? '0%' : `${barWidth}%`, backgroundColor: barColor, boxShadow: `0px 0px 4px 0px ${barColor}40` }}
          />
        </div>
      </div>
    </div>
  );
}
