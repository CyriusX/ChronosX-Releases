"use client";

export function MockupTimer() {
  const radius = 52;
  const circumference = 2 * Math.PI * radius;
  const progress = 0.6;
  const offset = circumference * (1 - progress);

  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="text-xs font-medium tracking-wider text-accent-violet uppercase">
        Focus Mode
      </div>
      <div className="relative">
        <svg width="120" height="120" viewBox="0 0 120 120">
          <circle
            cx="60"
            cy="60"
            r={radius}
            fill="none"
            stroke="rgba(120, 140, 255, 0.08)"
            strokeWidth="6"
          />
          <circle
            cx="60"
            cy="60"
            r={radius}
            fill="none"
            stroke="url(#timerGradient)"
            strokeWidth="6"
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            transform="rotate(-90 60 60)"
          />
          <defs>
            <linearGradient id="timerGradient" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#2E63FF" />
              <stop offset="100%" stopColor="#7C5CFF" />
            </linearGradient>
          </defs>
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="font-heading text-2xl font-bold text-text-primary">15:00</span>
          <span className="text-[10px] text-text-muted">remaining</span>
        </div>
      </div>
      <div className="flex items-center gap-2 text-xs text-text-muted">
        <div className="h-1.5 w-1.5 rounded-full bg-green-400 animate-pulse" />
        Pomodoro · Cycle 2/4
      </div>
    </div>
  );
}
