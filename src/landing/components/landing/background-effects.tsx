"use client";

export function BackgroundEffects() {
  return (
    <div className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
      {/* Atmospheric background aligned to product */}
      <div
        className="absolute inset-0"
        style={{
          background:
            "radial-gradient(ellipse 80% 60% at 15% 20%, rgba(139, 92, 246, 0.08), transparent 60%)," +
            "radial-gradient(ellipse 60% 50% at 85% 75%, rgba(59, 130, 246, 0.06), transparent 55%)," +
            "radial-gradient(ellipse 45% 35% at 50% 10%, rgba(245, 158, 11, 0.03), transparent 50%)," +
            "radial-gradient(ellipse 50% 40% at 70% 40%, rgba(236, 72, 153, 0.025), transparent 45%)," +
            "radial-gradient(ellipse 100% 100% at 50% 50%, rgba(10, 12, 18, 1), rgba(8, 10, 16, 1))",
        }}
      />

      {/* Noise texture overlay */}
      <div
        className="absolute inset-0 opacity-[0.025]"
        style={{
          backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='1'/%3E%3C/svg%3E")`,
          backgroundRepeat: "repeat",
          backgroundSize: "128px 128px",
        }}
      />
    </div>
  );
}
