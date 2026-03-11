interface TeamMemberProps {
  initial: string;
  name: string;
  time: string;
  gradient: string;
}

export function TeamMember({ initial, name, time, gradient }: TeamMemberProps) {
  return (
    <div className="flex items-center gap-[10px] py-1">
      <div
        className={`w-7 h-7 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center shadow-[0px_4px_6px_0px_rgba(0,0,0,0.1)]`}
      >
        <span className="text-[12px] font-semibold text-white">{initial}</span>
      </div>
      <span className="text-[12px] text-[rgba(245,247,251,0.8)] flex-1">{name}</span>
      <span className="text-[12px] text-[rgba(245,247,251,0.4)]">{time}</span>
    </div>
  );
}
