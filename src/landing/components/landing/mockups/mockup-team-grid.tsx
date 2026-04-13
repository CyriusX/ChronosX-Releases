"use client";

const members = [
  { name: "Sarah M.", status: "tracking", focus: 92, app: "VS Code" },
  { name: "Daniel P.", status: "tracking", focus: 78, app: "Figma" },
  { name: "Elena R.", status: "break", focus: 65, app: "—" },
  { name: "James O.", status: "tracking", focus: 84, app: "Chrome" },
  { name: "Maria C.", status: "idle", focus: 0, app: "—" },
  { name: "Alex K.", status: "tracking", focus: 71, app: "Slack" },
];

const statusColors: Record<string, string> = {
  tracking: "#05df72",
  break: "#fbbf24",
  idle: "#6B7399",
};

export function MockupTeamGrid() {
  return (
    <div className="rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="mb-1 text-xs font-medium tracking-wider text-accent-cyan uppercase">
        Team Activity
      </div>
      <div className="mb-3 flex items-center gap-3 text-[10px] text-text-dim">
        <span className="flex items-center gap-1">
          <span className="inline-block h-1.5 w-1.5 rounded-full bg-green-400" />
          4 tracking
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-1.5 w-1.5 rounded-full bg-amber-400" />
          1 break
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-1.5 w-1.5 rounded-full bg-text-dim" />
          1 idle
        </span>
      </div>
      <div className="grid grid-cols-2 gap-2">
        {members.map((m) => (
          <div
            key={m.name}
            className="rounded-xl bg-bg-tertiary/60 px-3 py-2"
          >
            <div className="flex items-center gap-1.5">
              <div
                className="h-1.5 w-1.5 rounded-full"
                style={{ backgroundColor: statusColors[m.status] }}
              />
              <span className="text-[11px] font-medium text-text-primary">
                {m.name}
              </span>
            </div>
            <div className="mt-0.5 text-[9px] text-text-dim">
              {m.status === "tracking" ? m.app : m.status}
              {m.focus > 0 && ` · ${m.focus}%`}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
