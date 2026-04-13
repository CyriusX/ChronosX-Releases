"use client";

const hours = [
  { label: "9a", value: 0.3 },
  { label: "10a", value: 0.85 },
  { label: "11a", value: 0.95 },
  { label: "12p", value: 0.4 },
  { label: "1p", value: 0.2 },
  { label: "2p", value: 0.7 },
  { label: "3p", value: 0.9 },
  { label: "4p", value: 0.75 },
  { label: "5p", value: 0.5 },
];

export function MockupActivityChart() {
  const maxH = 60;

  return (
    <div className="rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="mb-1 text-xs font-medium tracking-wider text-accent-blue uppercase">
        Today&apos;s Activity
      </div>
      <div className="mb-3 flex items-baseline gap-2">
        <span className="font-heading text-lg font-bold text-text-primary">6h 42m</span>
        <span className="text-[10px] text-green-400">+12% vs avg</span>
      </div>
      <div className="flex items-end justify-between gap-1.5">
        {hours.map((h) => (
          <div key={h.label} className="flex flex-col items-center gap-1">
            <div
              className="w-5 rounded-sm"
              style={{
                height: `${h.value * maxH}px`,
                background:
                  h.value > 0.7
                    ? "linear-gradient(to top, #2E63FF, #7C5CFF)"
                    : h.value > 0.4
                      ? "rgba(46, 99, 255, 0.5)"
                      : "rgba(46, 99, 255, 0.25)",
              }}
            />
            <span className="text-[9px] text-text-dim">{h.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
