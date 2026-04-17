"use client";

import { cn } from "@/lib/cn";

export function SoftFrame({
  children,
  className,
  innerClassName,
  fade = "soft",
}: {
  children: React.ReactNode;
  className?: string;
  innerClassName?: string;
  fade?: "soft" | "none";
}) {
  return (
    <div
      className={cn(
        "relative rounded-[28px] p-[1px]",
        "bg-gradient-to-br from-white/10 via-white/5 to-transparent",
        "shadow-[0_24px_120px_rgba(0,0,0,0.55)]",
        className
      )}
    >
      <div
        className={cn(
          "relative overflow-hidden rounded-[27px]",
          "border border-white/5 bg-bg-secondary/40 backdrop-blur-[18px]",
          innerClassName
        )}
      >
        {fade === "soft" && (
          <div className="pointer-events-none absolute inset-0">
            <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(10,12,18,0.18)_0%,transparent_20%,transparent_82%,rgba(10,12,18,0.26)_100%)]" />
            <div className="absolute inset-0 bg-[linear-gradient(90deg,rgba(10,12,18,0.22)_0%,transparent_14%,transparent_86%,rgba(10,12,18,0.24)_100%)]" />
          </div>
        )}

        <div className="relative h-full w-full">{children}</div>
      </div>
    </div>
  );
}
