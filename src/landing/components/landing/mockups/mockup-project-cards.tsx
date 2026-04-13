"use client";

const projects = [
  { name: "Website Redesign", color: "#2E63FF", time: "2h 15m", progress: 68 },
  { name: "Mobile App", color: "#7C5CFF", time: "1h 48m", progress: 42 },
  { name: "API Integration", color: "#22D3EE", time: "1h 05m", progress: 85 },
  { name: "Documentation", color: "#C15CFF", time: "0h 34m", progress: 25 },
];

export function MockupProjectCards() {
  return (
    <div className="rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="mb-3 text-xs font-medium tracking-wider text-accent-violet uppercase">
        Project Time
      </div>
      <div className="space-y-2.5">
        {projects.map((p) => (
          <div key={p.name} className="rounded-xl bg-bg-tertiary/60 px-3 py-2.5">
            <div className="mb-1.5 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div
                  className="h-2 w-2 rounded-full"
                  style={{ backgroundColor: p.color }}
                />
                <span className="text-xs font-medium text-text-primary">
                  {p.name}
                </span>
              </div>
              <span className="text-[11px] font-medium text-text-muted">{p.time}</span>
            </div>
            <div className="h-1 w-full overflow-hidden rounded-full bg-bg-primary/60">
              <div
                className="h-full rounded-full"
                style={{
                  width: `${p.progress}%`,
                  background: `linear-gradient(90deg, ${p.color}, ${p.color}88)`,
                }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
