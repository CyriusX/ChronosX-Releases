/**
 * Timer Page — Full-screen Pomodoro / Ultradian focus experience
 *
 * Left: Sidebar navigation
 * Center: Large animated timer (ring for Pomodoro, wave for Ultradian)
 * Right: Session history with productivity scores
 */

import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { Play, Pause, Square, SkipForward, ChevronDown, Focus, Activity, Coffee, Check, Clock } from 'lucide-react';
import { Sidebar } from '../components/dashboard';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { useIpc } from '../hooks/useIpc';

type TimerMode = 'pomodoro' | 'ultradian';
type TimerPhase = 'idle' | 'focus' | 'break';

interface SessionRecord {
  id: number;
  mode: TimerMode;
  phase: 'focus' | 'break';
  cycle: number;
  durationMs: number;
  completedAt: Date;
  productivity: number; // 0-100
}

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

function fmtDuration(ms: number) {
  const sec = Math.ceil(ms / 1000);
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function fmtShort(ms: number) {
  const m = Math.round(ms / 60000);
  if (m < 60) return `${m}m`;
  return `${Math.floor(m / 60)}h ${m % 60}m`;
}

export default function Timer() {
  const { sendQuery } = useIpc();

  const [mode, setMode] = useState<TimerMode>('pomodoro');
  const [phase, setPhase] = useState<TimerPhase>('idle');
  const [remainingMs, setRemainingMs] = useState(CONFIGS.pomodoro.focusMs);
  const [totalMs, setTotalMs] = useState(CONFIGS.pomodoro.focusMs);
  const [cycle, setCycle] = useState(0);
  const [isPaused, setIsPaused] = useState(false);
  const [ultradianWaves, setUltradianWaves] = useState(getUltradianWaves);
  const [sessions, setSessions] = useState<SessionRecord[]>([]);
  const [projects, setProjects] = useState<{ id: string; name: string }[]>([]);
  const [selectedProject, setSelectedProject] = useState('');
  const [showProjectDropdown, setShowProjectDropdown] = useState(false);
  const sessionIdRef = useRef(0);
  const phaseStartRef = useRef(Date.now());

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const phaseRef = useRef(phase);
  const cycleRef = useRef(cycle);
  const configRef = useRef(CONFIGS.pomodoro);
  const modeRef = useRef(mode);
  const ultradianWavesRef = useRef(ultradianWaves);

  phaseRef.current = phase;
  cycleRef.current = cycle;
  configRef.current = CONFIGS[mode];
  modeRef.current = mode;
  ultradianWavesRef.current = ultradianWaves;

  useEffect(() => {
    sendQuery('getProjects').then(r => {
      if (r.success && r.data) setProjects((r.data as { id: string; name: string }[]) ?? []);
    }).catch(() => {});
  }, [sendQuery]);

  const recordSession = useCallback((ph: 'focus' | 'break', durationMs: number) => {
    // Productivity score: focus sessions get 70-100 based on duration ratio, breaks get 50
    const score = ph === 'focus'
      ? Math.round(70 + (durationMs / configRef.current.focusMs) * 30)
      : 50;
    setSessions(prev => [{
      id: ++sessionIdRef.current,
      mode: modeRef.current,
      phase: ph,
      cycle: cycleRef.current + (ph === 'focus' ? 1 : 0),
      durationMs,
      completedAt: new Date(),
      productivity: Math.min(100, score),
    }, ...prev]);
  }, []);

  const handlePhaseComplete = useCallback(() => {
    const elapsed = Date.now() - phaseStartRef.current;
    const cfg = configRef.current;

    if (phaseRef.current === 'focus') {
      recordSession('focus', elapsed);
      const newCycle = cycleRef.current + 1;
      setCycle(newCycle);

      if (modeRef.current === 'ultradian' && newCycle >= ultradianWavesRef.current) {
        setPhase('idle'); setIsPaused(false); setCycle(0);
        setRemainingMs(cfg.focusMs); setTotalMs(cfg.focusMs);
        return;
      }

      const isLong = newCycle % cfg.cyclesBeforeLong === 0;
      const breakMs = isLong ? cfg.longBreakMs : cfg.shortBreakMs;
      setTotalMs(breakMs); setRemainingMs(breakMs); setPhase('break');
      phaseStartRef.current = Date.now();
    } else if (phaseRef.current === 'break') {
      recordSession('break', elapsed);
      setTotalMs(cfg.focusMs); setRemainingMs(cfg.focusMs); setPhase('focus');
      phaseStartRef.current = Date.now();
    }
  }, [recordSession]);

  useEffect(() => {
    if (phase === 'idle' || isPaused) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      return;
    }
    intervalRef.current = setInterval(() => {
      setRemainingMs(prev => {
        if (prev <= 100) { handlePhaseComplete(); return 0; }
        return prev - 100;
      });
    }, 100);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, [phase, isPaused, handlePhaseComplete]);

  const config = CONFIGS[mode];
  const startTimer = () => {
    setUltradianWaves(getUltradianWaves());
    setCycle(0); setTotalMs(config.focusMs); setRemainingMs(config.focusMs);
    setPhase('focus'); setIsPaused(false);
    phaseStartRef.current = Date.now();
  };
  const togglePause = () => setIsPaused(p => !p);
  const skipPhase = () => handlePhaseComplete();
  const stopTimer = () => {
    if (phase !== 'idle') {
      const elapsed = Date.now() - phaseStartRef.current;
      if (elapsed > 5000) recordSession(phase as 'focus' | 'break', elapsed);
    }
    setPhase('idle'); setIsPaused(false); setCycle(0);
    setRemainingMs(config.focusMs); setTotalMs(config.focusMs);
  };
  const switchMode = (m: TimerMode) => {
    if (phase !== 'idle') return;
    setMode(m); setRemainingMs(CONFIGS[m].focusMs); setTotalMs(CONFIGS[m].focusMs); setCycle(0);
  };

  const progress = totalMs > 0 ? 1 - (remainingMs / totalMs) : 0;
  const timeDisplay = fmtDuration(remainingMs);

  const phaseColor = phase === 'break' ? '#8b7aff' : mode === 'ultradian' ? '#c27aff' : '#4ad9ff';
  const phaseGlow = phase === 'break' ? 'rgba(139,122,255,0.4)' : mode === 'ultradian' ? 'rgba(194,122,255,0.4)' : 'rgba(74,217,255,0.4)';

  // Ultradian overall progress
  const singleWaveMs = config.focusMs + config.shortBreakMs;
  const allWavesMs = singleWaveMs * ultradianWaves;
  const completedWavesMs = cycle * singleWaveMs;
  const currentWaveElapsed = phase === 'focus' ? config.focusMs * progress : config.focusMs + config.shortBreakMs * progress;
  const totalElapsed = completedWavesMs + (phase === 'idle' ? 0 : currentWaveElapsed);
  const ultradianProgress = allWavesMs > 0 ? totalElapsed / allWavesMs : 0;

  // Session stats
  const totalFocusMs = sessions.filter(s => s.phase === 'focus').reduce((a, s) => a + s.durationMs, 0);
  const avgScore = sessions.filter(s => s.phase === 'focus').length > 0
    ? Math.round(sessions.filter(s => s.phase === 'focus').reduce((a, s) => a + s.productivity, 0) / sessions.filter(s => s.phase === 'focus').length)
    : 0;

  const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl";

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden">
      <Sidebar />

      <main className="flex-1 flex min-w-0 min-h-0">
        {/* Center — Timer */}
        <div className="flex-1 flex flex-col items-center justify-center p-8 min-w-0">
          {/* Mode toggle */}
          <div className="flex bg-[rgba(255,255,255,0.04)] rounded-full p-1 border border-[rgba(255,255,255,0.06)] mb-6">
            <button
              onClick={() => switchMode('pomodoro')}
              className={`px-5 py-1.5 rounded-full text-[12px] font-medium transition-all ${mode === 'pomodoro' ? 'bg-[#4ad9ff] text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}
            >
              <Focus className="w-3.5 h-3.5 inline -mt-0.5 mr-1.5" />Pomodoro
            </button>
            <button
              onClick={() => switchMode('ultradian')}
              className={`px-5 py-1.5 rounded-full text-[12px] font-medium transition-all ${mode === 'ultradian' ? 'bg-[#c27aff] text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}
            >
              <Activity className="w-3.5 h-3.5 inline -mt-0.5 mr-1.5" />Ultradian
            </button>
          </div>

          {/* Phase label */}
          <div className="flex items-center gap-2 mb-4">
            {phase === 'break' && <Coffee className="w-4 h-4" style={{ color: phaseColor }} />}
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.7)]">
              {phase === 'idle' ? (mode === 'pomodoro' ? 'Pronto para focar' : `${ultradianWaves} ${ultradianWaves === 1 ? 'onda' : 'ondas'} · ${ultradianWaves * 110}min`)
                : phase === 'focus' ? (mode === 'pomodoro' ? `Foco · Ciclo ${cycle + 1} de ${config.cyclesBeforeLong}` : `Peak · Onda ${cycle + 1} de ${ultradianWaves}`)
                : 'Pausa · Descanse'}
            </span>
          </div>

          {/* Timer visualization */}
          {mode === 'pomodoro' ? (
            <LargePomodoroRing progress={progress} phase={phase} phaseColor={phaseColor} phaseGlow={phaseGlow} timeDisplay={timeDisplay} />
          ) : (
            <LargeUltradianWave progress={ultradianProgress} phase={phase} timeDisplay={timeDisplay} totalWaves={ultradianWaves} currentWave={cycle} />
          )}

          {/* Cycle dots (pomodoro) */}
          {mode === 'pomodoro' && (
            <div className="flex items-center gap-2.5 mt-6">
              {Array.from({ length: config.cyclesBeforeLong }).map((_, i) => (
                <div key={i} className="w-3 h-3 rounded-full transition-all duration-300" style={{
                  backgroundColor: i < cycle ? phaseColor : i === cycle && phase === 'focus' ? phaseColor : 'rgba(255,255,255,0.08)',
                  boxShadow: i <= cycle ? `0 0 6px ${phaseGlow}` : 'none',
                  opacity: i < cycle ? 1 : i === cycle && phase === 'focus' ? 0.6 : 0.3,
                }} />
              ))}
            </div>
          )}

          {/* Project selector */}
          <div className="relative mt-6 w-[200px]">
            <button onClick={() => setShowProjectDropdown(!showProjectDropdown)} className="w-full flex items-center justify-between px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors">
              <span className="text-[rgba(245,247,251,0.5)] truncate">{selectedProject ? projects.find(p => p.id === selectedProject)?.name : 'Sem projeto'}</span>
              <ChevronDown className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
            </button>
            {showProjectDropdown && (
              <div className="absolute bottom-full left-0 right-0 mb-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-lg overflow-hidden z-10 shadow-xl max-h-[150px] overflow-y-auto">
                <button onClick={() => { setSelectedProject(''); setShowProjectDropdown(false); }} className={`w-full text-left px-3 py-2 text-[11px] hover:bg-[rgba(255,255,255,0.06)] ${!selectedProject ? 'text-[#4ad9ff]' : 'text-[rgba(245,247,251,0.6)]'}`}>Sem projeto</button>
                {projects.map(p => (
                  <button key={p.id} onClick={() => { setSelectedProject(p.id); setShowProjectDropdown(false); }} className={`w-full text-left px-3 py-2 text-[11px] hover:bg-[rgba(255,255,255,0.06)] truncate ${selectedProject === p.id ? 'text-[#4ad9ff]' : 'text-[rgba(245,247,251,0.6)]'}`}>{p.name}</button>
                ))}
              </div>
            )}
          </div>

          {/* Controls */}
          <div className="flex gap-3 mt-6">
            {phase === 'idle' ? (
              <button onClick={startTimer} className="flex items-center gap-2 px-8 py-3 rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[14px] font-medium text-white hover:opacity-90 transition-opacity shadow-[0_4px_15px_rgba(5,223,114,0.25)]">
                <Play className="w-5 h-5" /> Iniciar Foco
              </button>
            ) : (
              <>
                <button onClick={togglePause} className="flex items-center gap-2 px-6 py-3 rounded-full bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-[13px] font-medium text-white hover:opacity-90 transition-opacity">
                  {isPaused ? <Play className="w-4 h-4" /> : <Pause className="w-4 h-4" />} {isPaused ? 'Retomar' : 'Pausar'}
                </button>
                <button onClick={skipPhase} className="flex items-center gap-1.5 px-4 py-3 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[13px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] transition-colors">
                  <SkipForward className="w-4 h-4" /> Pular
                </button>
                <button onClick={stopTimer} className="flex items-center gap-1.5 px-4 py-3 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[13px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] transition-colors">
                  <Square className="w-4 h-4" /> Parar
                </button>
              </>
            )}
          </div>
        </div>

        {/* Right — Session History */}
        <div className="w-[300px] flex-shrink-0 p-4 overflow-y-auto border-l border-[rgba(255,255,255,0.04)]">
          {/* Stats summary */}
          <Card className={cardBase + ' mb-4'}>
            <CardContent className="p-4">
              <p className="text-[12px] text-[rgba(245,247,251,0.4)] mb-3 uppercase tracking-wider">Resumo da sessão</p>
              <div className="grid grid-cols-3 gap-3">
                <div className="text-center">
                  <p className="text-[20px] font-bold text-[#f5f7fb]">{sessions.filter(s => s.phase === 'focus').length}</p>
                  <p className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase">Ciclos</p>
                </div>
                <div className="text-center">
                  <p className="text-[20px] font-bold text-[#f5f7fb]">{fmtShort(totalFocusMs)}</p>
                  <p className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase">Foco</p>
                </div>
                <div className="text-center">
                  <p className="text-[20px] font-bold" style={{ color: avgScore >= 80 ? '#4ade80' : avgScore >= 50 ? '#fbbf24' : '#f87171' }}>{avgScore || '—'}</p>
                  <p className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase">Score</p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Session list */}
          <Card className={cardBase}>
            <CardHeader className="pb-0 pt-3 px-4">
              <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Histórico</CardTitle>
            </CardHeader>
            <CardContent className="pt-3 pb-3 px-4">
              {sessions.length === 0 ? (
                <div className="text-center py-8">
                  <Clock className="w-8 h-8 mx-auto mb-2 text-[rgba(245,247,251,0.15)]" />
                  <p className="text-[11px] text-[rgba(245,247,251,0.3)]">Nenhuma sessão ainda</p>
                  <p className="text-[9px] text-[rgba(245,247,251,0.2)] mt-1">Inicie o timer para começar</p>
                </div>
              ) : (
                <div className="space-y-2">
                  {sessions.map(s => (
                    <div key={s.id} className="flex items-center gap-2.5 py-1.5 px-2 rounded-lg hover:bg-[rgba(255,255,255,0.02)] transition-colors">
                      {/* Status icon */}
                      <div className={`w-6 h-6 rounded-full flex items-center justify-center flex-shrink-0 ${
                        s.phase === 'focus'
                          ? 'bg-[rgba(74,217,255,0.1)] border border-[rgba(74,217,255,0.2)]'
                          : 'bg-[rgba(139,122,255,0.1)] border border-[rgba(139,122,255,0.2)]'
                      }`}>
                        {s.phase === 'focus'
                          ? <Check className="w-3 h-3 text-[#4ad9ff]" />
                          : <Coffee className="w-3 h-3 text-[#8b7aff]" />
                        }
                      </div>

                      {/* Info */}
                      <div className="flex-1 min-w-0">
                        <p className="text-[11px] text-[rgba(245,247,251,0.8)]">
                          {s.phase === 'focus' ? `Foco #${s.cycle}` : 'Pausa'}
                          <span className="text-[rgba(245,247,251,0.3)] ml-1.5">
                            {s.mode === 'pomodoro' ? 'Pomodoro' : 'Ultradian'}
                          </span>
                        </p>
                        <p className="text-[9px] text-[rgba(245,247,251,0.3)]">
                          {s.completedAt.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })} · {fmtShort(s.durationMs)}
                        </p>
                      </div>

                      {/* Score */}
                      {s.phase === 'focus' && (
                        <div className={`px-2 py-0.5 rounded-full text-[9px] font-medium ${
                          s.productivity >= 80 ? 'bg-[rgba(74,222,128,0.15)] text-[#4ade80]'
                          : s.productivity >= 50 ? 'bg-[rgba(251,191,36,0.15)] text-[#fbbf24]'
                          : 'bg-[rgba(248,113,113,0.15)] text-[#f87171]'
                        }`}>
                          {s.productivity}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}

// ============================================================================
// LARGE POMODORO RING
// ============================================================================

function LargePomodoroRing({ progress, phase, phaseColor, phaseGlow, timeDisplay }: {
  progress: number; phase: TimerPhase; phaseColor: string; phaseGlow: string; timeDisplay: string;
}) {
  const size = 260;
  const cx = size / 2;
  const cy = size / 2;
  const radius = 110;
  const circumference = 2 * Math.PI * radius;
  const numTicks = 60;

  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90">
        {Array.from({ length: numTicks }).map((_, i) => {
          const angle = (i / numTicks) * 360;
          const rad = (angle * Math.PI) / 180;
          const isActive = (i / numTicks) <= progress;
          const innerR = radius - 12;
          const outerR = radius - (i % 5 === 0 ? 3 : 7);
          return (
            <line key={i}
              x1={cx + innerR * Math.cos(rad)} y1={cy + innerR * Math.sin(rad)}
              x2={cx + outerR * Math.cos(rad)} y2={cy + outerR * Math.sin(rad)}
              stroke={isActive ? phaseColor : 'rgba(255,255,255,0.06)'}
              strokeWidth={i % 5 === 0 ? 2 : 1} strokeLinecap="round"
              style={isActive ? { filter: `drop-shadow(0 0 3px ${phaseGlow})` } : undefined}
            />
          );
        })}
        <circle cx={cx} cy={cy} r={radius} fill="none" stroke="rgba(255,255,255,0.03)" strokeWidth="3" />
        <circle cx={cx} cy={cy} r={radius} fill="none" stroke={phaseColor} strokeWidth="3"
          strokeDasharray={circumference} strokeDashoffset={circumference * (1 - progress)}
          strokeLinecap="round" opacity="0.5"
          style={{ filter: `drop-shadow(0 0 6px ${phaseGlow})`, transition: 'stroke-dashoffset 0.1s linear' }}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-[52px] font-bold font-mono tracking-wider" style={{ color: phase === 'idle' ? 'rgba(245,247,251,0.5)' : '#f5f7fb' }}>
          {timeDisplay}
        </span>
        {phase !== 'idle' && (
          <span className="text-[11px] uppercase tracking-[0.2em] mt-1" style={{ color: phaseColor }}>
            {phase === 'focus' ? 'focando' : 'descansando'}
          </span>
        )}
      </div>
    </div>
  );
}

// ============================================================================
// LARGE ULTRADIAN WAVE
// ============================================================================

function LargeUltradianWave({ progress, phase, timeDisplay, totalWaves, currentWave }: {
  progress: number; phase: TimerPhase; timeDisplay: string; totalWaves: number; currentWave: number;
}) {
  const w = 500;
  const h = 180;
  const padX = 20;
  const padTop = 50;
  const padBot = 30;
  const graphW = w - padX * 2;
  const graphH = h - padTop - padBot;
  const midY = padTop + graphH / 2;
  const focusFraction = 90 / 110;

  const focusColor = '#c27aff';
  const breakColor = '#8b7aff';
  const activeColor = phase === 'break' ? breakColor : focusColor;
  const isIdle = phase === 'idle';

  const points = useMemo(() => {
    const pts: { x: number; y: number }[] = [];
    const stepsPerWave = 80;
    const totalSteps = stepsPerWave * totalWaves;
    for (let i = 0; i <= totalSteps; i++) {
      const t = i / totalSteps;
      const x = padX + t * graphW;
      const withinWaveT = ((i / totalSteps) * totalWaves) % 1;
      const y = withinWaveT <= focusFraction
        ? midY - Math.sin((withinWaveT / focusFraction) * Math.PI) * (graphH * 0.42)
        : midY + Math.sin(((withinWaveT - focusFraction) / (1 - focusFraction)) * Math.PI) * (graphH * 0.28);
      pts.push({ x, y });
    }
    return pts;
  }, [graphW, graphH, midY, totalWaves, padX]);

  const markerIdx = Math.min(Math.floor(progress * (points.length - 1)), points.length - 1);
  const marker = points[markerIdx] ?? points[0];

  const fillPath = useMemo(() => {
    const filled = points.slice(0, markerIdx + 1);
    if (filled.length < 2) return '';
    const d = filled.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');
    const bottom = midY + graphH / 2 + 2;
    return `${d} L ${filled[filled.length - 1].x.toFixed(1)} ${bottom} L ${filled[0].x.toFixed(1)} ${bottom} Z`;
  }, [points, markerIdx, midY, graphH]);

  const waveBoundaries = totalWaves > 1
    ? Array.from({ length: totalWaves - 1 }, (_, i) => padX + ((i + 1) / totalWaves) * graphW)
    : [];

  return (
    <div className="flex flex-col items-center">
      {/* Timer */}
      <div className="mb-2 text-center">
        <span className="text-[48px] font-bold font-mono tracking-wider" style={{ color: isIdle ? 'rgba(245,247,251,0.4)' : '#f5f7fb' }}>{timeDisplay}</span>
        {!isIdle && <span className="text-[11px] uppercase tracking-[0.2em] ml-3" style={{ color: activeColor }}>{phase === 'focus' ? 'peak' : 'recovery'}</span>}
      </div>

      {/* Wave */}
      <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} className="max-w-full">
        <defs>
          <linearGradient id="lgUltFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={activeColor} stopOpacity="0.25" />
            <stop offset="100%" stopColor={activeColor} stopOpacity="0.02" />
          </linearGradient>
        </defs>
        <line x1={padX} y1={midY} x2={padX + graphW} y2={midY} stroke="rgba(255,255,255,0.04)" strokeWidth="1" strokeDasharray="4 4" />
        {waveBoundaries.map((bx, i) => <line key={i} x1={bx} y1={padTop - 4} x2={bx} y2={h - padBot + 4} stroke="rgba(255,255,255,0.05)" strokeWidth="1" strokeDasharray="3 3" />)}
        {Array.from({ length: totalWaves }).map((_, i) => {
          const cx = padX + ((i + 0.5) / totalWaves) * graphW;
          return <text key={i} x={cx} y={h - 8} textAnchor="middle" fontSize="9" fill={i === currentWave && !isIdle ? 'rgba(245,247,251,0.5)' : 'rgba(245,247,251,0.15)'} fontFamily="sans-serif">{totalWaves === 1 ? 'FOCUS · REST' : `Onda ${i + 1}`}</text>;
        })}
        <path d={points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')} fill="none" stroke={focusColor} strokeWidth="2" opacity="0.12" />
        {!isIdle && fillPath && <path d={fillPath} fill="url(#lgUltFill)" />}
        {!isIdle && <path d={points.slice(0, markerIdx + 1).map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')} fill="none" stroke={activeColor} strokeWidth="3" strokeLinecap="round" style={{ filter: `drop-shadow(0 0 4px ${activeColor}50)` }} />}
        {!isIdle && (
          <>
            <circle cx={marker.x} cy={marker.y} r="7" fill={activeColor} opacity="0.15"><animate attributeName="r" values="7;12;7" dur="2s" repeatCount="indefinite" /></circle>
            <circle cx={marker.x} cy={marker.y} r="5" fill={activeColor} style={{ filter: `drop-shadow(0 0 6px ${activeColor})` }} />
            <circle cx={marker.x} cy={marker.y} r="2" fill="#fff" />
          </>
        )}
        {isIdle && <circle cx={points[0].x} cy={points[0].y} r="4" fill="rgba(245,247,251,0.15)" />}
      </svg>

      {/* Wave dots */}
      {totalWaves > 1 && !isIdle && (
        <div className="flex items-center gap-2 mt-3">
          {Array.from({ length: totalWaves }).map((_, i) => (
            <div key={i} className="w-2.5 h-2.5 rounded-full transition-all duration-300" style={{
              backgroundColor: i < currentWave ? focusColor : i === currentWave ? activeColor : 'rgba(255,255,255,0.08)',
              boxShadow: i <= currentWave ? `0 0 5px ${focusColor}40` : 'none',
              opacity: i < currentWave ? 1 : i === currentWave ? 0.7 : 0.3,
            }} />
          ))}
        </div>
      )}
    </div>
  );
}
