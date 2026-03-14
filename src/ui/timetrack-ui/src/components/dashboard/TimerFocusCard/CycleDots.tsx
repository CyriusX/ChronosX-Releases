/**
 * CycleDots - Visual indicator for completed cycles
 *
 * Shows dots like ●●○○ for Pomodoro cycles
 *
 * CX-139: Timer Focus Card
 */

interface CycleDotsProps {
  currentCycle: number;
  totalCycles: number;
}

export function CycleDots({ currentCycle, totalCycles }: CycleDotsProps) {
  // Limit to reasonable display
  const maxDots = Math.min(totalCycles, 8);
  const dots = Array.from({ length: maxDots }, (_, i) => i + 1);

  return (
    <div className="flex items-center justify-center gap-1.5">
      {dots.map((cycleNumber) => (
        <div
          key={cycleNumber}
          className={`w-2 h-2 rounded-full transition-all duration-300 ${
            cycleNumber <= currentCycle
              ? 'bg-[#8b7aff] shadow-[0_0_6px_rgba(139,122,255,0.5)]'
              : 'bg-[rgba(255,255,255,0.15)]'
          }`}
          title={`Ciclo ${cycleNumber}`}
        />
      ))}
    </div>
  );
}
