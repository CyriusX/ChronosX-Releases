"use client";

export function MockupFocusScore() {
  const score = 87;
  const radius = 44;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference * (1 - score / 100);

  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="text-xs font-medium tracking-wider text-accent-blue uppercase">
        Focus Score
      </div>
      <div className="relative">
        <svg width="100" height="100" viewBox="0 0 100 100">
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            stroke="rgba(120, 140, 255, 0.08)"
            strokeWidth="5"
          />
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            stroke="url(#scoreGradient)"
            strokeWidth="5"
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            transform="rotate(-90 50 50)"
          />
          <defs>
            <linearGradient id="scoreGradient" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="#2E63FF" />
              <stop offset="50%" stopColor="#7C5CFF" />
              <stop offset="100%" stopColor="#22D3EE" />
            </linearGradient>
          </defs>
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="font-heading text-3xl font-bold text-text-primary">{score}</span>
          <span className="text-[10px] text-text-muted">Excellent</span>
        </div>
      </div>
      <div className="flex w-full gap-2 text-[10px]">
        <div className="flex-1 rounded-lg bg-bg-tertiary px-2 py-1.5 text-center">
          <div className="font-semibold text-green-400">72%</div>
          <div className="text-text-dim">Productive</div>
        </div>
        <div className="flex-1 rounded-lg bg-bg-tertiary px-2 py-1.5 text-center">
          <div className="font-semibold text-text-muted">18%</div>
          <div className="text-text-dim">Neutral</div>
        </div>
        <div className="flex-1 rounded-lg bg-bg-tertiary px-2 py-1.5 text-center">
          <div className="font-semibold text-amber-400">10%</div>
          <div className="text-text-dim">Distracted</div>
        </div>
      </div>
    </div>
  );
}
