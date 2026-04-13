"use client";

const topApps = [
  { name: "VS Code", time: "3h 12m", pct: 48, color: "#2E63FF" },
  { name: "Chrome", time: "1h 24m", pct: 21, color: "#7C5CFF" },
  { name: "Figma", time: "0h 58m", pct: 14, color: "#22D3EE" },
  { name: "Slack", time: "0h 36m", pct: 9, color: "#C15CFF" },
  { name: "Other", time: "0h 32m", pct: 8, color: "#6B7399" },
];

export function MockupReports() {
  return (
    <div className="rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="mb-1 text-xs font-medium tracking-wider text-accent-magenta uppercase">
        App Usage Report
      </div>
      <div className="mb-3 text-[10px] text-text-dim">This week · 33h 42m total</div>
      <div className="space-y-2">
        {topApps.map((app) => (
          <div key={app.name}>
            <div className="mb-0.5 flex items-center justify-between">
              <span className="text-[11px] font-medium text-text-primary">{app.name}</span>
              <span className="text-[10px] text-text-muted">{app.time}</span>
            </div>
            <div className="h-1.5 w-full overflow-hidden rounded-full bg-bg-primary/60">
              <div
                className="h-full rounded-full"
                style={{
                  width: `${app.pct}%`,
                  backgroundColor: app.color,
                }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
