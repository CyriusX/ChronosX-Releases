"use client";

export function BackgroundEffects() {
  return (
    <div className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
      {/* Grid pattern */}
      <div className="bg-grid-pattern absolute inset-0" />

      {/* Gradient orbs */}
      <div
        className="absolute -top-40 right-1/4 h-[600px] w-[600px] rounded-full opacity-15"
        style={{
          background:
            "radial-gradient(circle, rgba(46, 99, 255, 0.4) 0%, transparent 70%)",
          filter: "blur(80px)",
        }}
      />
      <div
        className="absolute top-1/3 -left-40 h-[500px] w-[500px] rounded-full opacity-12"
        style={{
          background:
            "radial-gradient(circle, rgba(124, 92, 255, 0.35) 0%, transparent 70%)",
          filter: "blur(100px)",
        }}
      />
      <div
        className="absolute bottom-1/4 right-0 h-[400px] w-[400px] rounded-full opacity-10"
        style={{
          background:
            "radial-gradient(circle, rgba(34, 211, 238, 0.3) 0%, transparent 70%)",
          filter: "blur(90px)",
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
