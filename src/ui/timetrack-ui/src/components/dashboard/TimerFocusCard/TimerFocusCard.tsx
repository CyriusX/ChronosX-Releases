/**
 * TimerFocusCard - Pomodoro / Ultradian Timer
 *
 * Pomodoro: animated countdown ring with tick marks
 * Ultradian: sine wave showing focus peak → recovery trough with position marker
 */

import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { Play, Pause, Square, SkipForward, ChevronDown, Focus, Activity, Coffee } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { useIpc } from '../../../hooks/useIpc';
import type { TodaySummaryResponse } from '../../../types/ipc';

type TimerMode = 'pomodoro' | 'ultradian';
type TimerPhase = 'idle' | 'focus' | 'break';

interface TimerConfig {
  focusMs: number;
  shortBreakMs: number;
  longBreakMs: number;
  cyclesBeforeLong: number;
}

function getUltradianWaves(): number {
  try { return parseInt(localStorage.getItem('timetrack-ultradian-waves') ?? '1', 10) || 1; }
  catch { return 1; }
}

const CONFIGS: Record<TimerMode, TimerConfig> = {
  pomodoro:  { focusMs: 25 * 60000, shortBreakMs: 5 * 60000, longBreakMs: 15 * 60000, cyclesBeforeLong: 4 },
  ultradian: { focusMs: 90 * 60000, shortBreakMs: 20 * 60000, longBreakMs: 20 * 60000, cyclesBeforeLong: 1 },
};

interface TimerFocusCardProps {
  summary?: TodaySummaryResponse | null;
  [key: string]: unknown;
}

const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl";

export function TimerFocusCard({ summary }: TimerFocusCardProps) {
  const { sendQuery } = useIpc();

  const [mode, setMode] = useState<TimerMode>('pomodoro');
  const [phase, setPhase] = useState<TimerPhase>('idle');
  const [remainingMs, setRemainingMs] = useState(CONFIGS.pomodoro.focusMs);
  const [totalMs, setTotalMs] = useState(CONFIGS.pomodoro.focusMs);
  const [cycle, setCycle] = useState(0); // current wave index (0-based) for ultradian
  const [isPaused, setIsPaused] = useState(false);
  const [ultradianWaves, setUltradianWaves] = useState(getUltradianWaves);
  const [selectedProject, setSelectedProject] = useState<string>('');
  const [projects, setProjects] = useState<{ id: string; name: string }[]>([]);
  const [showProjectDropdown, setShowProjectDropdown] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const phaseRef = useRef(phase);
  const cycleRef = useRef(cycle);
  const configRef = useRef(CONFIGS.pomodoro);

  phaseRef.current = phase;
  cycleRef.current = cycle;
  configRef.current = CONFIGS[mode];
  const ultradianWavesRef = useRef(ultradianWaves);
  const modeRef = useRef(mode);
  ultradianWavesRef.current = ultradianWaves;
  modeRef.current = mode;

  useEffect(() => {
    sendQuery('getProjects').then((res) => {
      if (res.success && res.data) setProjects((res.data as { id: string; name: string }[]) ?? []);
    }).catch(() => {});
  }, [sendQuery]);

  const handlePhaseComplete = useCallback(() => {
    const cfg = configRef.current;
    if (phaseRef.current === 'focus') {
      const newCycle = cycleRef.current + 1;
      setCycle(newCycle);

      // For ultradian: check if all waves are done
      if (modeRef.current === 'ultradian' && newCycle >= ultradianWavesRef.current) {
        // All waves completed — stop
        setPhase('idle');
        setIsPaused(false);
        setCycle(0);
        setRemainingMs(cfg.focusMs);
        setTotalMs(cfg.focusMs);
        return;
      }

      const isLong = newCycle % cfg.cyclesBeforeLong === 0;
      const breakMs = isLong ? cfg.longBreakMs : cfg.shortBreakMs;
      setTotalMs(breakMs);
      setRemainingMs(breakMs);
      setPhase('break');
    } else if (phaseRef.current === 'break') {
      setTotalMs(cfg.focusMs);
      setRemainingMs(cfg.focusMs);
      setPhase('focus');
    }
  }, []);

  useEffect(() => {
    if (phase === 'idle' || isPaused) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      return;
    }
    intervalRef.current = setInterval(() => {
      setRemainingMs((prev) => {
        if (prev <= 100) { handlePhaseComplete(); return 0; }
        return prev - 100;
      });
    }, 100);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, [phase, isPaused, handlePhaseComplete]);

  const config = CONFIGS[mode];
  const startTimer = () => { setUltradianWaves(getUltradianWaves()); setCycle(0); setTotalMs(config.focusMs); setRemainingMs(config.focusMs); setPhase('focus'); setIsPaused(false); };
  const togglePause = () => setIsPaused((p) => !p);
  const skipPhase = () => handlePhaseComplete();
  const stopTimer = () => { setPhase('idle'); setIsPaused(false); setCycle(0); setRemainingMs(config.focusMs); setTotalMs(config.focusMs); };
  const switchMode = (m: TimerMode) => { if (phase !== 'idle') return; setMode(m); setRemainingMs(CONFIGS[m].focusMs); setTotalMs(CONFIGS[m].focusMs); setCycle(0); };

  const totalSec = Math.max(0, Math.ceil(remainingMs / 1000));
  const minutes = Math.floor(totalSec / 60);
  const seconds = totalSec % 60;
  const timeDisplay = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  const progress = totalMs > 0 ? 1 - (remainingMs / totalMs) : 0;

  const phaseColor = phase === 'break' ? '#8b7aff' : mode === 'ultradian' ? '#c27aff' : '#4ad9ff';
  const phaseGlow = phase === 'break' ? 'rgba(139,122,255,0.3)' : mode === 'ultradian' ? 'rgba(194,122,255,0.3)' : 'rgba(74,217,255,0.3)';
  const phaseLabel = phase === 'focus' ? 'Foco' : phase === 'break' ? 'Recovery' : mode === 'pomodoro' ? 'Pomodoro' : 'Ultradian';

  // Overall progress across ALL ultradian waves
  const singleWaveMs = config.focusMs + config.shortBreakMs; // 110min per wave
  const allWavesMs = singleWaveMs * ultradianWaves;
  const completedWavesMs = cycle * singleWaveMs; // fully completed waves
  const currentWaveElapsed = phase === 'focus'
    ? config.focusMs * progress
    : config.focusMs + config.shortBreakMs * progress;
  const totalElapsed = completedWavesMs + (phase === 'idle' ? 0 : currentWaveElapsed);
  const ultradianProgress = allWavesMs > 0 ? totalElapsed / allWavesMs : 0;

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <div className="flex items-center gap-1.5">
            {mode === 'pomodoro' ? <Focus className="w-3.5 h-3.5 text-[#4ad9ff]" /> : <Activity className="w-3.5 h-3.5 text-[#c27aff]" />}
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{phaseLabel}</span>
            {phase !== 'idle' && (
              <span className="text-[10px] text-[rgba(245,247,251,0.4)] ml-1">
                {phase === 'break'
                  ? <><Coffee className="w-3 h-3 inline -mt-0.5 mr-0.5" />Pausa</>
                  : mode === 'pomodoro'
                    ? <>Ciclo {cycle + 1}</>
                    : <>Onda {cycle + 1}/{ultradianWaves}</>
                }
              </span>
            )}
          </div>
          {phase === 'idle' && (
            <div className="flex bg-[rgba(255,255,255,0.04)] rounded-full p-0.5 border border-[rgba(255,255,255,0.06)]">
              <button onClick={() => switchMode('pomodoro')} className={`px-2 py-0.5 rounded-full text-[8px] font-medium transition-all ${mode === 'pomodoro' ? 'bg-[#4ad9ff] text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}>25/5</button>
              <button onClick={() => switchMode('ultradian')} className={`px-2 py-0.5 rounded-full text-[8px] font-medium transition-all ${mode === 'ultradian' ? 'bg-[#c27aff] text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}>90/20</button>
            </div>
          )}
        </CardTitle>
      </CardHeader>

      <CardContent className="pt-2 pb-3 px-4">
        <div className="flex flex-col items-center">

          {/* === POMODORO: Ring clock === */}
          {mode === 'pomodoro' && <PomodoroRing progress={progress} phase={phase} phaseColor={phaseColor} phaseGlow={phaseGlow} timeDisplay={timeDisplay} />}

          {/* === ULTRADIAN: Wave === */}
          {mode === 'ultradian' && <UltradianWave progress={ultradianProgress} phase={phase} timeDisplay={timeDisplay} isPaused={isPaused} totalWaves={ultradianWaves} currentWave={cycle} />}

          {/* Cycle dots (pomodoro only) */}
          {phase !== 'idle' && mode === 'pomodoro' && (
            <div className="flex items-center gap-1.5 mt-2">
              {Array.from({ length: config.cyclesBeforeLong }).map((_, i) => (
                <div key={i} className="w-2 h-2 rounded-full transition-all duration-300" style={{
                  backgroundColor: i < cycle ? phaseColor : i === cycle && phase === 'focus' ? phaseColor : 'rgba(255,255,255,0.1)',
                  boxShadow: i <= cycle ? `0 0 4px ${phaseGlow}` : 'none',
                  opacity: i < cycle ? 1 : i === cycle && phase === 'focus' ? 0.6 : 0.3,
                }} />
              ))}
            </div>
          )}

          {/* Project selector */}
          <div className="w-full mt-2.5 relative">
            <button onClick={() => setShowProjectDropdown(!showProjectDropdown)} className="w-full flex items-center justify-between px-2.5 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[10px] hover:bg-[rgba(255,255,255,0.05)] transition-colors">
              <span className="text-[rgba(245,247,251,0.5)] truncate">{selectedProject ? projects.find(p => p.id === selectedProject)?.name ?? selectedProject : 'Sem projeto'}</span>
              <ChevronDown className="w-3 h-3 text-[rgba(245,247,251,0.3)] flex-shrink-0" />
            </button>
            {showProjectDropdown && (
              <div className="absolute bottom-full left-0 right-0 mb-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-lg overflow-hidden z-10 shadow-xl max-h-[120px] overflow-y-auto">
                <button onClick={() => { setSelectedProject(''); setShowProjectDropdown(false); }} className={`w-full text-left px-2.5 py-1.5 text-[10px] hover:bg-[rgba(255,255,255,0.06)] transition-colors ${!selectedProject ? 'text-[#c27aff]' : 'text-[rgba(245,247,251,0.6)]'}`}>Sem projeto</button>
                {projects.map((p) => (
                  <button key={p.id} onClick={() => { setSelectedProject(p.id); setShowProjectDropdown(false); }} className={`w-full text-left px-2.5 py-1.5 text-[10px] hover:bg-[rgba(255,255,255,0.06)] transition-colors truncate ${selectedProject === p.id ? 'text-[#c27aff]' : 'text-[rgba(245,247,251,0.6)]'}`}>{p.name}</button>
                ))}
              </div>
            )}
          </div>

          {/* Controls */}
          <div className="flex gap-2 mt-2.5 w-full">
            {phase === 'idle' ? (
              <button onClick={startTimer} className="flex items-center justify-center gap-1.5 flex-1 py-2 rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[11px] font-medium text-white hover:opacity-90 transition-opacity shadow-[0_3px_10px_rgba(5,223,114,0.2)]">
                <Play className="w-3.5 h-3.5" /> Iniciar
              </button>
            ) : (
              <>
                <button onClick={togglePause} className="flex items-center justify-center gap-1 flex-1 py-2 rounded-full bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-[11px] font-medium text-white hover:opacity-90 transition-opacity">
                  {isPaused ? <Play className="w-3 h-3" /> : <Pause className="w-3 h-3" />} {isPaused ? 'Retomar' : 'Pausar'}
                </button>
                <button onClick={skipPhase} className="flex items-center justify-center py-2 px-3 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] transition-colors" title="Pular fase">
                  <SkipForward className="w-3 h-3" />
                </button>
                <button onClick={stopTimer} className="flex items-center justify-center py-2 px-3 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] transition-colors" title="Parar">
                  <Square className="w-3 h-3" />
                </button>
              </>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================================================
// POMODORO RING — animated clock with tick marks
// ============================================================================

function PomodoroRing({ progress, phase, phaseColor, phaseGlow, timeDisplay }: {
  progress: number; phase: TimerPhase; phaseColor: string; phaseGlow: string; timeDisplay: string;
}) {
  const size = 110;
  const cx = size / 2;
  const cy = size / 2;
  const radius = 44;
  const circumference = 2 * Math.PI * radius;
  const numTicks = 60;

  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90">
        {Array.from({ length: numTicks }).map((_, i) => {
          const angle = (i / numTicks) * 360;
          const rad = (angle * Math.PI) / 180;
          const isActive = (i / numTicks) <= progress;
          const innerR = radius - 6;
          const outerR = radius - (i % 5 === 0 ? 2 : 4);
          return (
            <line key={i}
              x1={cx + innerR * Math.cos(rad)} y1={cy + innerR * Math.sin(rad)}
              x2={cx + outerR * Math.cos(rad)} y2={cy + outerR * Math.sin(rad)}
              stroke={isActive ? phaseColor : 'rgba(255,255,255,0.08)'}
              strokeWidth={i % 5 === 0 ? 1.5 : 0.8} strokeLinecap="round"
              style={isActive ? { filter: `drop-shadow(0 0 2px ${phaseGlow})` } : undefined}
            />
          );
        })}
        <circle cx={cx} cy={cy} r={radius} fill="none" stroke="rgba(255,255,255,0.04)" strokeWidth="2.5" />
        <circle cx={cx} cy={cy} r={radius} fill="none" stroke={phaseColor} strokeWidth="2.5"
          strokeDasharray={circumference} strokeDashoffset={circumference * (1 - progress)}
          strokeLinecap="round" opacity="0.6"
          style={{ filter: `drop-shadow(0 0 4px ${phaseGlow})`, transition: 'stroke-dashoffset 0.1s linear' }}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-[24px] font-bold font-mono tracking-wider" style={{ color: phase === 'idle' ? 'rgba(245,247,251,0.6)' : '#f5f7fb' }}>{timeDisplay}</span>
        {phase !== 'idle' && <span className="text-[8px] uppercase tracking-widest mt-0.5" style={{ color: phaseColor }}>{phase === 'focus' ? 'focando' : 'descansando'}</span>}
      </div>
    </div>
  );
}

// ============================================================================
// ULTRADIAN WAVE — sine wave showing focus peak → recovery trough
// ============================================================================

function UltradianWave({ progress, phase, timeDisplay, isPaused, totalWaves, currentWave }: {
  progress: number; phase: TimerPhase; timeDisplay: string; isPaused: boolean; totalWaves: number; currentWave: number;
}) {
  const w = 220;
  const h = 100;
  const padX = 8;
  const padTop = 16;
  const padBot = 20;
  const graphW = w - padX * 2;
  const graphH = h - padTop - padBot;
  const midY = padTop + graphH / 2;

  const focusFraction = 90 / 110; // focus portion of one wave

  // Build wave points for ALL waves
  const points = useMemo(() => {
    const pts: { x: number; y: number; waveIdx: number; isFocus: boolean }[] = [];
    const stepsPerWave = 60;
    const totalSteps = stepsPerWave * totalWaves;
    for (let i = 0; i <= totalSteps; i++) {
      const t = i / totalSteps; // 0 → 1 across all waves
      const x = padX + t * graphW;
      const waveIdx = Math.min(Math.floor((i / totalSteps) * totalWaves), totalWaves - 1);
      const withinWaveT = ((i / totalSteps) * totalWaves) % 1; // 0→1 within a single wave
      let y: number;
      let isFocus: boolean;
      if (withinWaveT <= focusFraction) {
        const angle = (withinWaveT / focusFraction) * Math.PI;
        y = midY - Math.sin(angle) * (graphH * 0.42);
        isFocus = true;
      } else {
        const breakT = (withinWaveT - focusFraction) / (1 - focusFraction);
        const angle = breakT * Math.PI;
        y = midY + Math.sin(angle) * (graphH * 0.28);
        isFocus = false;
      }
      pts.push({ x, y, waveIdx, isFocus });
    }
    return pts;
  }, [graphW, graphH, midY, totalWaves, padX]);

  const linePath = useMemo(() => {
    return points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');
  }, [points]);

  const markerIdx = Math.min(Math.floor(progress * (points.length - 1)), points.length - 1);
  const marker = points[markerIdx] ?? points[0];

  const fillPath = useMemo(() => {
    const filledPts = points.slice(0, markerIdx + 1);
    if (filledPts.length < 2) return '';
    const d = filledPts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');
    const lastPt = filledPts[filledPts.length - 1];
    const firstPt = filledPts[0];
    const bottom = midY + graphH / 2 + 2;
    return `${d} L ${lastPt.x.toFixed(1)} ${bottom} L ${firstPt.x.toFixed(1)} ${bottom} Z`;
  }, [points, markerIdx, midY, graphH]);

  // Wave boundary lines
  const waveBoundaries = useMemo(() => {
    if (totalWaves <= 1) return [];
    const boundaries: number[] = [];
    for (let i = 1; i < totalWaves; i++) {
      boundaries.push(padX + (i / totalWaves) * graphW);
    }
    return boundaries;
  }, [totalWaves, graphW, padX]);

  const focusColor = '#c27aff';
  const breakColor = '#8b7aff';
  const activeColor = phase === 'break' ? breakColor : focusColor;
  const isIdle = phase === 'idle';

  return (
    <div className="w-full flex flex-col items-center">
      {/* Timer display */}
      <div className="mb-1 text-center">
        <span className="text-[22px] font-bold font-mono tracking-wider" style={{ color: isIdle ? 'rgba(245,247,251,0.5)' : '#f5f7fb' }}>{timeDisplay}</span>
        {!isIdle && (
          <span className="text-[8px] uppercase tracking-widest ml-2" style={{ color: activeColor }}>
            {phase === 'focus' ? 'peak' : 'recovery'}
          </span>
        )}
      </div>

      {/* Wave SVG */}
      <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} className="w-full">
        <defs>
          <linearGradient id="ultFillGrad" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={activeColor} stopOpacity="0.25" />
            <stop offset="100%" stopColor={activeColor} stopOpacity="0.02" />
          </linearGradient>
        </defs>

        {/* Baseline */}
        <line x1={padX} y1={midY} x2={padX + graphW} y2={midY} stroke="rgba(255,255,255,0.05)" strokeWidth="1" strokeDasharray="3 3" />

        {/* Wave boundary lines */}
        {waveBoundaries.map((bx, i) => (
          <line key={i} x1={bx} y1={padTop - 2} x2={bx} y2={h - padBot + 2} stroke="rgba(255,255,255,0.06)" strokeWidth="1" strokeDasharray="2 2" />
        ))}

        {/* Wave number labels */}
        {Array.from({ length: totalWaves }).map((_, i) => {
          const cx = padX + ((i + 0.5) / totalWaves) * graphW;
          return (
            <text key={i} x={cx} y={h - 4} textAnchor="middle" fontSize="7" fill={i === currentWave && !isIdle ? 'rgba(245,247,251,0.6)' : 'rgba(245,247,251,0.2)'} fontFamily="sans-serif">
              {totalWaves === 1 ? 'FOCUS · REST' : `Onda ${i + 1}`}
            </text>
          );
        })}

        {/* Full wave (dim) */}
        <path d={linePath} fill="none" stroke={focusColor} strokeWidth="1.5" opacity="0.15" />

        {/* Filled area under progress */}
        {!isIdle && fillPath && <path d={fillPath} fill="url(#ultFillGrad)" />}

        {/* Active wave (bright, up to progress) */}
        {!isIdle && (
          <path
            d={points.slice(0, markerIdx + 1).map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')}
            fill="none" stroke={activeColor} strokeWidth="2.5" strokeLinecap="round"
            style={{ filter: `drop-shadow(0 0 3px ${activeColor}50)` }}
          />
        )}

        {/* Marker dot */}
        {!isIdle && (
          <>
            <circle cx={marker.x} cy={marker.y} r="5" fill={activeColor} opacity="0.15">
              <animate attributeName="r" values="5;9;5" dur="2s" repeatCount="indefinite" />
              <animate attributeName="opacity" values="0.15;0.03;0.15" dur="2s" repeatCount="indefinite" />
            </circle>
            <circle cx={marker.x} cy={marker.y} r="3.5" fill={activeColor} style={{ filter: `drop-shadow(0 0 4px ${activeColor})` }} />
            <circle cx={marker.x} cy={marker.y} r="1.5" fill="#fff" />
          </>
        )}

        {/* Idle marker at start */}
        {isIdle && (
          <circle cx={points[0].x} cy={points[0].y} r="3" fill="rgba(245,247,251,0.2)" stroke="rgba(245,247,251,0.1)" strokeWidth="1" />
        )}
      </svg>

      {/* Wave dots indicator */}
      {totalWaves > 1 && !isIdle && (
        <div className="flex items-center gap-1.5 mt-1">
          {Array.from({ length: totalWaves }).map((_, i) => (
            <div key={i} className="w-2 h-2 rounded-full transition-all duration-300" style={{
              backgroundColor: i < currentWave ? focusColor : i === currentWave ? activeColor : 'rgba(255,255,255,0.1)',
              boxShadow: i <= currentWave ? `0 0 4px ${focusColor}40` : 'none',
              opacity: i < currentWave ? 1 : i === currentWave ? 0.7 : 0.3,
            }} />
          ))}
        </div>
      )}
    </div>
  );
}
