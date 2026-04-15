"use client";

const days = ["Mon", "Tue", "Wed", "Thu", "Fri"];
const hours = ["9a", "10a", "11a", "12p", "1p", "2p", "3p", "4p", "5p"];

const data: number[][] = [
  [0.3, 0.8, 0.9, 0.4, 0.1, 0.6, 0.85, 0.7, 0.5],
  [0.5, 0.9, 0.95, 0.3, 0.2, 0.8, 0.9, 0.6, 0.4],
  [0.2, 0.7, 0.8, 0.5, 0.3, 0.7, 0.75, 0.8, 0.6],
  [0.6, 0.85, 0.9, 0.2, 0.1, 0.9, 0.95, 0.7, 0.3],
  [0.4, 0.6, 0.7, 0.3, 0.2, 0.5, 0.6, 0.4, 0.2],
];

function cellColor(v: number) {
  if (v > 0.8) return "rgba(46, 99, 255, 0.7)";
  if (v > 0.6) return "rgba(46, 99, 255, 0.45)";
  if (v > 0.3) return "rgba(46, 99, 255, 0.25)";
  return "rgba(46, 99, 255, 0.08)";
}

export function MockupHeatmap() {
  return (
    <div className="rounded-2xl border border-border-subtle bg-card/80 p-5">
      <div className="mb-3 text-xs font-medium tracking-wider text-accent-blue uppercase">
        Weekly Heatmap
      </div>
      <div className="space-y-1">
        {days.map((day, di) => (
          <div key={day} className="flex items-center gap-1.5">
            <span className="w-7 text-[9px] text-text-dim">{day}</span>
            <div className="flex gap-1">
              {hours.map((h, hi) => (
                <div
                  key={h}
                  className="h-4 w-5 rounded-[3px]"
                  style={{ backgroundColor: cellColor(data[di][hi]) }}
                />
              ))}
            </div>
          </div>
        ))}
        <div className="flex gap-1 pl-[34px] pt-0.5">
          {hours.map((h) => (
            <span key={h} className="w-5 text-center text-[7px] text-text-dim">
              {h}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
