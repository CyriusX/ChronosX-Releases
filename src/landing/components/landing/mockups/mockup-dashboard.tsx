"use client";

import { MockupTimer } from "./mockup-timer";
import { MockupFocusScore } from "./mockup-focus-score";
import { MockupActivityChart } from "./mockup-activity-chart";
import { MockupProjectCards } from "./mockup-project-cards";

export function MockupDashboard() {
  return (
    <div className="relative mx-auto w-full max-w-4xl">
      {/* Outer glow */}
      <div
        className="absolute -inset-4 rounded-3xl opacity-30"
        style={{
          background:
            "radial-gradient(ellipse at center, rgba(46, 99, 255, 0.2) 0%, transparent 70%)",
          filter: "blur(40px)",
        }}
      />
      {/* Dashboard frame */}
      <div className="relative overflow-hidden rounded-2xl border border-border-subtle bg-bg-secondary/90 p-3 shadow-2xl backdrop-blur-sm md:p-5">
        {/* Top bar */}
        <div className="mb-4 flex items-center justify-between rounded-xl bg-bg-tertiary/60 px-4 py-2.5">
          <div className="flex items-center gap-3">
            <div className="flex gap-1.5">
              <div className="h-2.5 w-2.5 rounded-full bg-red-400/60" />
              <div className="h-2.5 w-2.5 rounded-full bg-amber-400/60" />
              <div className="h-2.5 w-2.5 rounded-full bg-green-400/60" />
            </div>
            <span className="text-[11px] font-medium text-text-muted">
              ChronosX Dashboard
            </span>
          </div>
          <div className="flex items-center gap-2">
            <div className="flex items-center gap-1.5 rounded-lg bg-green-400/10 px-2 py-0.5">
              <div className="h-1.5 w-1.5 rounded-full bg-green-400 animate-pulse" />
              <span className="text-[10px] font-medium text-green-400">Tracking</span>
            </div>
            <span className="text-[10px] text-text-dim">Today · Wed, Apr 9</span>
          </div>
        </div>

        {/* Stat pills row */}
        <div className="mb-4 grid grid-cols-2 gap-2 md:grid-cols-4">
          {[
            { label: "Time Tracked", value: "6h 42m", color: "#2E63FF" },
            { label: "Focus Score", value: "87", color: "#7C5CFF" },
            { label: "Distractions", value: "4", color: "#fbbf24" },
            { label: "Breaks", value: "3", color: "#22D3EE" },
          ].map((s) => (
            <div
              key={s.label}
              className="rounded-xl border border-border-subtle bg-card/60 px-3 py-2.5 text-center"
            >
              <div
                className="font-heading text-lg font-bold"
                style={{ color: s.color }}
              >
                {s.value}
              </div>
              <div className="text-[10px] text-text-dim">{s.label}</div>
            </div>
          ))}
        </div>

        {/* Main grid */}
        <div className="grid gap-3 md:grid-cols-2">
          <MockupActivityChart />
          <MockupFocusScore />
          <MockupProjectCards />
          <MockupTimer />
        </div>
      </div>
    </div>
  );
}
