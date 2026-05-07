import { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Play, Pause, Square, SkipForward, ChevronDown, Focus, Activity, Coffee } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useIpc } from '../../hooks/useIpc';
import { useTimerStore, getUserTimerConfig, type TimerPhase } from '../../stores/timerStore';
import { fadeIn, scaleIn, SPRING, TIMING } from '../../lib/animation';
import { getDemoMode } from '../demoMode';

function fmtDuration(ms: number) {
  const sec = Math.ceil(ms / 1000);
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export default function TimerDemo() {
  const { t } = useTranslation();
  const { sendQuery } = useIpc();
  const demoMode = getDemoMode();
  const isMobileDemo = demoMode === 'mobile';

  const mode = useTimerStore(s => s.mode);
  const phase = useTimerStore(s => s.phase);
  const remainingMs = useTimerStore(s => s.remainingMs);
  const totalMs = useTimerStore(s => s.totalMs);
  const cycle = useTimerStore(s => s.cycle);
  const isPaused = useTimerStore(s => s.isPaused);
  const ultradianWaves = useTimerStore(s => s.ultradianWaves);
  const sessionName = useTimerStore(s => s.sessionName);
  const selectedProject = useTimerStore(s => s.selectedProject);

  const setMode = useTimerStore(s => s.setMode);
  const start = useTimerStore(s => s.start);
  const togglePause = useTimerStore(s => s.togglePause);
  const skip = useTimerStore(s => s.skip);
  const stop = useTimerStore(s => s.stop);
  const setSessionName = useTimerStore(s => s.setSessionName);
  const setSelectedProject = useTimerStore(s => s.setSelectedProject);

  const [projects, setProjects] = useState<{ id: string; name: string }[]>([]);
  const [showProjectDropdown, setShowProjectDropdown] = useState(false);

  useEffect(() => {
    sendQuery('getProjects')
      .then(r => {
        if (r.success && r.data) setProjects((r.data as { id: string; name: string }[]) ?? []);
      })
      .catch(() => {});
  }, [sendQuery]);

  const config = getUserTimerConfig(mode);
  const progress = totalMs > 0 ? 1 - (remainingMs / totalMs) : 0;
  const timeDisplay = fmtDuration(remainingMs);

  const phaseColor = phase === 'break' ? '#8b7aff' : mode === 'ultradian' ? '#c27aff' : '#8B5CF6';
  const phaseGlow = phase === 'break' ? 'rgba(139,122,255,0.4)' : mode === 'ultradian' ? 'rgba(194,122,255,0.4)' : 'rgba(139,92,246,0.4)';

  const singleWaveMs = config.focusMs + config.shortBreakMs;
  const allWavesMs = singleWaveMs * ultradianWaves;
  const completedWavesMs = cycle * singleWaveMs;
  const currentWaveElapsed = phase === 'focus' ? config.focusMs * progress : config.focusMs + config.shortBreakMs * progress;
  const totalElapsed = completedWavesMs + (phase === 'idle' ? 0 : currentWaveElapsed);
  const ultradianProgress = allWavesMs > 0 ? totalElapsed / allWavesMs : 0;

  const outerClass = isMobileDemo
    ? 'min-h-[100dvh] bg-[#0b0d14] pb-14 md:pb-0 overflow-x-hidden'
    : 'flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0 overflow-x-hidden';

  const mainClass = isMobileDemo
    ? 'px-4 pt-4 pb-4 overflow-x-hidden'
    : 'flex-1 flex flex-col min-w-0 min-h-0 overflow-y-auto overflow-x-hidden';

  return (
    <div className={outerClass}>
      <main className={mainClass} data-demo-scroll-root={isMobileDemo ? 'timer' : undefined}>
        <motion.div
          className={isMobileDemo ? 'flex flex-col items-center justify-center min-w-0' : 'flex flex-col items-center justify-center p-4 sm:p-8 min-w-0 flex-1'}
          variants={fadeIn}
          initial="hidden"
          animate="visible"
          transition={{ duration: TIMING.normal }}
        >
          {/* Mode toggle */}
          <div className="flex bg-[rgba(255,255,255,0.04)] rounded-full p-1 border border-[rgba(255,255,255,0.06)] mb-4 sm:mb-6">
            <button
              onClick={() => setMode('pomodoro')}
              className={`relative px-5 py-1.5 rounded-full text-[12px] font-medium transition-all ${mode === 'pomodoro' ? 'text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}
            >
              {mode === 'pomodoro' && (
                <motion.div
                  layoutId="timer-mode"
                  className="absolute inset-0 rounded-full bg-[#8B5CF6]"
                  transition={SPRING.snappy}
                />
              )}
              <span className="relative z-10"><Focus className="w-3.5 h-3.5 inline -mt-0.5 mr-1.5" />{t('timer.pomodoro')}</span>
            </button>
            <button
              onClick={() => setMode('ultradian')}
              className={`relative px-5 py-1.5 rounded-full text-[12px] font-medium transition-all ${mode === 'ultradian' ? 'text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}
            >
              {mode === 'ultradian' && (
                <motion.div
                  layoutId="timer-mode"
                  className="absolute inset-0 rounded-full bg-[#c27aff]"
                  transition={SPRING.snappy}
                />
              )}
              <span className="relative z-10"><Activity className="w-3.5 h-3.5 inline -mt-0.5 mr-1.5" />Ultradian</span>
            </button>
          </div>

          {/* Phase label */}
          <AnimatePresence mode="wait">
            <motion.div
              key={phase}
              className="flex items-center gap-2 mb-4"
              initial={{ opacity: 0, y: -8 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: 8 }}
              transition={{ duration: TIMING.fast }}
            >
              {phase === 'break' && <Coffee className="w-4 h-4" style={{ color: phaseColor }} />}
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.7)]">
                {phase === 'idle'
                  ? (mode === 'pomodoro'
                    ? t('timer.readyToFocus')
                    : `${ultradianWaves} ${ultradianWaves === 1 ? t('timer.wave') : t('timer.waves')} · ${ultradianWaves * 110}min`)
                  : phase === 'focus'
                  ? (mode === 'pomodoro'
                    ? `${t('timer.focusCycle')} · ${t('timer.cycleOf', { current: cycle + 1, total: config.cyclesBeforeLong })}`
                    : `${t('timer.peakWave')} · ${t('timer.cycleOf', { current: cycle + 1, total: ultradianWaves })}`)
                  : t('timer.breakRest')}
              </span>
            </motion.div>
          </AnimatePresence>

          {/* Timer visualization */}
          <div className="w-full flex justify-center" style={{ maxWidth: 'min(100%, 520px)' }}>
            {mode === 'pomodoro' ? (
              <LargePomodoroRing progress={progress} phase={phase} phaseColor={phaseColor} phaseGlow={phaseGlow} timeDisplay={timeDisplay} />
            ) : (
              <LargeUltradianWave progress={ultradianProgress} phase={phase} timeDisplay={timeDisplay} totalWaves={ultradianWaves} currentWave={cycle} />
            )}
          </div>

          {/* Cycle dots (pomodoro) */}
          {mode === 'pomodoro' && (
            <div className="flex items-center gap-2.5 mt-6">
              {Array.from({ length: config.cyclesBeforeLong }).map((_, i) => (
                <motion.div
                  key={i}
                  className="w-3 h-3 rounded-full transition-all duration-300"
                  initial={{ scale: 0.6, opacity: 0.3 }}
                  animate={{
                    scale: i < cycle ? 1 : i === cycle && phase === 'focus' ? 0.85 : 0.6,
                    opacity: i < cycle ? 1 : i === cycle && phase === 'focus' ? 0.6 : 0.3,
                  }}
                  transition={SPRING.snappy}
                  style={{
                    backgroundColor: i < cycle ? phaseColor : i === cycle && phase === 'focus' ? phaseColor : 'rgba(255,255,255,0.08)',
                    boxShadow: i <= cycle ? `0 0 6px ${phaseGlow}` : 'none',
                  }}
                />
              ))}
            </div>
          )}

          {/* Session name + Project selector */}
          <div className="flex flex-col gap-2 mt-4 sm:mt-6 w-full max-w-[280px]">
            <input
              type="text"
              value={sessionName}
              onChange={e => setSessionName(e.target.value)}
              placeholder={t('timer.sessionName')}
              className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[12px] text-[rgba(245,247,251,0.7)] placeholder-[rgba(245,247,251,0.2)] focus:outline-none focus:border-[rgba(255,255,255,0.15)] transition-colors"
            />
            <div className="relative">
              <button onClick={() => setShowProjectDropdown(!showProjectDropdown)} className="w-full flex items-center justify-between px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors">
                <span className="text-[rgba(245,247,251,0.5)] truncate">{selectedProject ? projects.find(p => p.id === selectedProject)?.name : t('timer.noProject')}</span>
                <ChevronDown className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
              </button>
              {showProjectDropdown && (
                <div className="absolute bottom-full left-0 right-0 mb-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-lg overflow-hidden z-10 shadow-xl max-h-[150px] overflow-y-auto">
                  <button onClick={() => { setSelectedProject(''); setShowProjectDropdown(false); }} className={`w-full text-left px-3 py-2 text-[11px] hover:bg-[rgba(255,255,255,0.06)] ${!selectedProject ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.6)]'}`}>{t('timer.noProject')}</button>
                  {projects.map(p => (
                    <button key={p.id} onClick={() => { setSelectedProject(p.id); setShowProjectDropdown(false); }} className={`w-full text-left px-3 py-2 text-[11px] hover:bg-[rgba(255,255,255,0.06)] truncate ${selectedProject === p.id ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.6)]'}`}>{p.name}</button>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Controls */}
          <div className="flex flex-wrap gap-2 sm:gap-3 mt-4 sm:mt-6 justify-center">
            {phase === 'idle' ? (
              <motion.button
                onClick={start}
                className="flex items-center gap-2 px-8 py-3 rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[14px] font-medium text-white hover:opacity-90 transition-opacity shadow-[0_4px_15px_rgba(5,223,114,0.25)]"
                whileHover={{ scale: 1.04 }}
                whileTap={{ scale: 0.97 }}
              >
                <Play className="w-5 h-5" /> {t('timer.startFocus')}
              </motion.button>
            ) : (
              <motion.div
                className="flex gap-3"
                initial="hidden"
                animate="visible"
                variants={{
                  hidden: { opacity: 1 },
                  visible: {
                    opacity: 1,
                    transition: { staggerChildren: 0.05 },
                  },
                }}
              >
                <motion.button
                  onClick={togglePause}
                  className="flex items-center gap-2 px-6 py-3 rounded-full bg-gradient-to-b from-[#8B5CF6] to-[#22D3EE] text-[13px] font-medium text-white hover:opacity-90 transition-opacity"
                  variants={scaleIn}
                  transition={{ duration: TIMING.fast }}
                >
                  {isPaused ? <Play className="w-4 h-4" /> : <Pause className="w-4 h-4" />} {isPaused ? t('timer.resume') : t('timer.pause')}
                </motion.button>
                <motion.button
                  onClick={skip}
                  className="flex items-center gap-1.5 px-4 py-3 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[13px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                  variants={scaleIn}
                  transition={{ duration: TIMING.fast }}
                >
                  <SkipForward className="w-4 h-4" /> {t('timer.skip')}
                </motion.button>
                <motion.button
                  onClick={stop}
                  className="flex items-center gap-1.5 px-4 py-3 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[13px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                  variants={scaleIn}
                  transition={{ duration: TIMING.fast }}
                >
                  <Square className="w-4 h-4" /> {t('timer.stop')}
                </motion.button>
              </motion.div>
            )}
          </div>

          {isMobileDemo && (
            <>
              <div data-demo-marker="timer-bottom" className="h-px w-full" />
              <div data-demo-scroll-end className="h-px w-full" />
            </>
          )}
        </motion.div>
      </main>
    </div>
  );
}

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
      <div className="mb-2 text-center">
        <span className="text-[48px] font-bold font-mono tracking-wider" style={{ color: isIdle ? 'rgba(245,247,251,0.4)' : '#f5f7fb' }}>{timeDisplay}</span>
        {!isIdle && <span className="text-[11px] uppercase tracking-[0.2em] ml-3" style={{ color: activeColor }}>{phase === 'focus' ? 'peak' : 'recovery'}</span>}
      </div>

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
    </div>
  );
}
