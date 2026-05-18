/**
 * TimerFocusCard — Dashboard Timer status card (premium redesign)
 *
 * Shows timer status, project, tags, and controls in a clean status card layout.
 * PomodoroRing and UltradianWave are preserved for the full Timer page.
 */

import { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Play, Pause, Square, ChevronDown, MoreVertical } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { TagPill } from '../shared/TagPill';
import { useIpc } from '../../../hooks/useIpc';
import { useTimerStore, type TimerPhase } from '../../../stores/timerStore';
import { useNotifications } from '../../../stores/uiStore';
import type { TodaySummaryResponse } from '../../../types/ipc';
import { cardBase } from '../shared/styles';

interface TimerFocusCardProps {
  summary?: TodaySummaryResponse | null;
  [key: string]: unknown;
}

// Predefined tag colors
const TAG_COLORS = ['#8B5CF6', '#22D3EE', '#3B82F6', '#F59E0B', '#10B981', '#EC4899'];

export function TimerFocusCard({ summary: _summary }: TimerFocusCardProps) {
  const { t } = useTranslation();
  const { sendQuery, sendCommand } = useIpc();
  const { notify } = useNotifications();

  // --- Shared timer state from store ---
  const mode = useTimerStore(s => s.mode);
  const phase = useTimerStore(s => s.phase);
  const remainingMs = useTimerStore(s => s.remainingMs);
  const isPaused = useTimerStore(s => s.isPaused);
  const selectedProject = useTimerStore(s => s.selectedProject);

  // --- Store actions ---
  const setMode = useTimerStore(s => s.setMode);
  const start = useTimerStore(s => s.start);
  const togglePause = useTimerStore(s => s.togglePause);
  const stop = useTimerStore(s => s.stop);

  // --- Tracking-connected handlers ---
  const handleStart = async () => {
    start();
    const res = await sendCommand('startTracking');
    if (!res.success) notify.error('Failed to start tracking', res.error);
  };
  const setSelectedProject = useTimerStore(s => s.setSelectedProject);

  // --- Local UI state ---
  const [projects, setProjects] = useState<{ id: string; name: string }[]>([]);
  const [showProjectDropdown, setShowProjectDropdown] = useState(false);
  const [tags, setTags] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [showTagInput, setShowTagInput] = useState(false);

  useEffect(() => {
    sendQuery('getProjects').then((res) => {
      if (res.success && res.data) setProjects((res.data as { id: string; name: string }[]) ?? []);
    }).catch(() => {});
  }, [sendQuery]);

  // --- Computed ---
  const totalSec = Math.max(0, Math.ceil(remainingMs / 1000));
  const minutes = Math.floor(totalSec / 60);
  const seconds = totalSec % 60;
  const timeDisplay = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

  const isRunning = phase !== 'idle';
  const statusDotColor = isRunning
    ? isPaused ? '#fbbf24' : '#05df72'
    : 'rgba(245,247,251,0.3)';
  const statusLabel = isRunning
    ? isPaused ? t('timer.paused') : t('timer.inProgress')
    : t('timer.inactive');

  const selectedProjectName = selectedProject
    ? projects.find(p => p.id === selectedProject)?.name ?? selectedProject
    : t('timer.noProject');

  const addTag = (tag: string) => {
    const trimmed = tag.trim();
    if (trimmed && !tags.includes(trimmed)) {
      setTags(prev => [...prev, trimmed]);
    }
    setTagInput('');
    setShowTagInput(false);
  };

  const removeTag = (tag: string) => {
    setTags(prev => prev.filter(t => t !== tag));
  };

  return (
    <Card className={`${cardBase} h-full`}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{t('timer.timerTitle')}</span>
          <div className="flex items-center gap-2">
            {/* Mode toggle (compact) */}
            {!isRunning && (
              <div className="flex bg-[rgba(255,255,255,0.04)] rounded-full p-0.5 border border-[rgba(255,255,255,0.06)]">
                <button onClick={() => setMode('pomodoro')} className={`px-2 py-0.5 rounded-full text-[8px] font-medium transition-all ${mode === 'pomodoro' ? 'bg-[#8B5CF6] text-white' : 'text-[rgba(245,247,251,0.4)]'}`}>25/5</button>
                <button onClick={() => setMode('ultradian')} className={`px-2 py-0.5 rounded-full text-[8px] font-medium transition-all ${mode === 'ultradian' ? 'bg-[#c27aff] text-[#0b0d14]' : 'text-[rgba(245,247,251,0.4)]'}`}>90/20</button>
              </div>
            )}
            <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
          </div>
        </CardTitle>
      </CardHeader>

      <CardContent className="pt-2 pb-3 px-4">
        <div className="flex flex-col gap-2.5">

          {/* Status indicator */}
          <div className="flex items-center gap-2">
            <div
              className="w-2.5 h-2.5 rounded-full flex-shrink-0"
              style={{
                backgroundColor: statusDotColor,
                boxShadow: isRunning && !isPaused ? `0 0 8px ${statusDotColor}` : 'none',
              }}
            />
            <span className="text-[11px] font-medium text-[rgba(245,247,251,0.7)]">{statusLabel}</span>
            {isRunning && (
              <span className="text-[11px] font-mono text-[rgba(245,247,251,0.5)] ml-auto">{timeDisplay}</span>
            )}
          </div>

          {/* Project selector */}
          <div className="relative">
            <label className="text-[9px] uppercase tracking-wider text-[rgba(245,247,251,0.3)] mb-1 block">{t('timer.project')}</label>
            <button
              onClick={() => setShowProjectDropdown(!showProjectDropdown)}
              className="w-full flex items-center justify-between px-2.5 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[10px] hover:bg-[rgba(255,255,255,0.05)] transition-colors"
            >
              <span className="text-[rgba(245,247,251,0.7)] truncate">{selectedProjectName}</span>
              <ChevronDown className="w-3 h-3 text-[rgba(245,247,251,0.3)] flex-shrink-0" />
            </button>
            {showProjectDropdown && (
              <div className="absolute top-full left-0 right-0 mt-1 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-lg overflow-hidden z-10 shadow-xl max-h-[120px] overflow-y-auto">
                <button onClick={() => { setSelectedProject(''); setShowProjectDropdown(false); }} className={`w-full text-left px-2.5 py-1.5 text-[10px] hover:bg-[rgba(255,255,255,0.06)] transition-colors ${!selectedProject ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.6)]'}`}>{t('timer.noProject')}</button>
                {projects.map((p) => (
                  <button key={p.id} onClick={() => { setSelectedProject(p.id); setShowProjectDropdown(false); }} className={`w-full text-left px-2.5 py-1.5 text-[10px] hover:bg-[rgba(255,255,255,0.06)] transition-colors truncate ${selectedProject === p.id ? 'text-[#8B5CF6]' : 'text-[rgba(245,247,251,0.6)]'}`}>{p.name}</button>
                ))}
              </div>
            )}
          </div>

          {/* Tags */}
          <div>
            <label className="text-[9px] uppercase tracking-wider text-[rgba(245,247,251,0.3)] mb-1 block">{t('timer.tags')}</label>
            <div className="flex flex-wrap gap-1.5 items-center">
              {tags.map((tag, i) => (
                <TagPill
                  key={tag}
                  label={tag}
                  color={TAG_COLORS[i % TAG_COLORS.length]}
                  size="sm"
                  onRemove={() => removeTag(tag)}
                />
              ))}
              {showTagInput ? (
                <input
                  autoFocus
                  value={tagInput}
                  onChange={e => setTagInput(e.target.value)}
                  onKeyDown={e => {
                    if (e.key === 'Enter') addTag(tagInput);
                    if (e.key === 'Escape') { setShowTagInput(false); setTagInput(''); }
                  }}
                  onBlur={() => { if (tagInput) addTag(tagInput); else setShowTagInput(false); }}
                  placeholder="Tag..."
                  className="px-2 py-0.5 rounded-full bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.08)] text-[9px] text-[rgba(245,247,251,0.7)] placeholder-[rgba(245,247,251,0.2)] w-16 focus:outline-none focus:border-[rgba(139,92,246,0.3)]"
                />
              ) : (
                <button
                  onClick={() => setShowTagInput(true)}
                  className="px-2 py-0.5 rounded-full border border-dashed border-[rgba(255,255,255,0.1)] text-[9px] text-[rgba(245,247,251,0.3)] hover:text-[rgba(245,247,251,0.6)] hover:border-[rgba(255,255,255,0.2)] transition-colors"
                >
                  + Tag
                </button>
              )}
            </div>
          </div>

          {/* Action buttons */}
          <div className="flex gap-2 mt-1">
            {phase === 'idle' ? (
              <button
                onClick={handleStart}
                className="flex items-center justify-center gap-1.5 flex-1 py-2 rounded-full bg-gradient-to-r from-[#05df72] to-[#00b8db] text-[11px] font-medium text-white hover:opacity-90 transition-opacity shadow-[0_3px_12px_rgba(5,223,114,0.25)]"
              >
                <Play className="w-3.5 h-3.5" /> {t('timer.start')}
              </button>
            ) : (
              <>
                <button
                  onClick={togglePause}
                  className="flex items-center justify-center gap-1 flex-1 py-2 rounded-full bg-gradient-to-r from-[#05df72] to-[#00b8db] text-[11px] font-medium text-white hover:opacity-90 transition-opacity shadow-[0_3px_12px_rgba(5,223,114,0.2)]"
                >
                  {isPaused ? <Play className="w-3 h-3" /> : <Pause className="w-3 h-3" />}
                  {isPaused ? t('timer.resume') : t('timer.pause')}
                </button>
                <button
                  onClick={stop}
                  className="flex items-center justify-center gap-1 px-4 py-2 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[11px] font-medium text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                >
                  <Square className="w-3 h-3" /> {t('timer.finish')}
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
// POMODORO RING — animated clock with tick marks (used by Timer page)
// ============================================================================

export function PomodoroRing({ progress, phase, phaseColor, phaseGlow, timeDisplay }: {
  progress: number; phase: TimerPhase; phaseColor: string; phaseGlow: string; timeDisplay: string;
}) {
  const { t } = useTranslation();
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
        {phase !== 'idle' && <span className="text-[8px] uppercase tracking-widest mt-0.5" style={{ color: phaseColor }}>{phase === 'focus' ? t('timer.focusing') : t('timer.resting')}</span>}
      </div>
    </div>
  );
}

// ============================================================================
// ULTRADIAN WAVE — sine wave showing focus peak → recovery trough (used by Timer page)
// ============================================================================

export function UltradianWave({ progress, phase, timeDisplay, totalWaves, currentWave }: {
  progress: number; phase: TimerPhase; timeDisplay: string; totalWaves: number; currentWave: number;
}) {
  const { t } = useTranslation();
  const w = 220;
  const h = 100;
  const padX = 8;
  const padTop = 16;
  const padBot = 20;
  const graphW = w - padX * 2;
  const graphH = h - padTop - padBot;
  const midY = padTop + graphH / 2;

  const focusFraction = 90 / 110;

  const points = useMemo(() => {
    const pts: { x: number; y: number; waveIdx: number; isFocus: boolean }[] = [];
    const stepsPerWave = 60;
    const totalSteps = stepsPerWave * totalWaves;
    for (let i = 0; i <= totalSteps; i++) {
      const t = i / totalSteps;
      const x = padX + t * graphW;
      const waveIdx = Math.min(Math.floor((i / totalSteps) * totalWaves), totalWaves - 1);
      const withinWaveT = ((i / totalSteps) * totalWaves) % 1;
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
      <div className="mb-1 text-center">
        <span className="text-[22px] font-bold font-mono tracking-wider" style={{ color: isIdle ? 'rgba(245,247,251,0.5)' : '#f5f7fb' }}>{timeDisplay}</span>
        {!isIdle && (
          <span className="text-[8px] uppercase tracking-widest ml-2" style={{ color: activeColor }}>
            {phase === 'focus' ? t('timer.peak') : t('timer.recovery')}
          </span>
        )}
      </div>

      <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} className="w-full">
        <defs>
          <linearGradient id="ultFillGrad" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={activeColor} stopOpacity="0.25" />
            <stop offset="100%" stopColor={activeColor} stopOpacity="0.02" />
          </linearGradient>
        </defs>
        <line x1={padX} y1={midY} x2={padX + graphW} y2={midY} stroke="rgba(255,255,255,0.05)" strokeWidth="1" strokeDasharray="3 3" />
        {waveBoundaries.map((bx, i) => (
          <line key={i} x1={bx} y1={padTop - 2} x2={bx} y2={h - padBot + 2} stroke="rgba(255,255,255,0.06)" strokeWidth="1" strokeDasharray="2 2" />
        ))}
        {Array.from({ length: totalWaves }).map((_, i) => {
          const cx = padX + ((i + 0.5) / totalWaves) * graphW;
          return (
            <text key={i} x={cx} y={h - 4} textAnchor="middle" fontSize="7" fill={i === currentWave && !isIdle ? 'rgba(245,247,251,0.6)' : 'rgba(245,247,251,0.2)'} fontFamily="sans-serif">
              {totalWaves === 1 ? t('timer.focusRest') : t('timer.waveLabel', { number: i + 1 })}
            </text>
          );
        })}
        <path d={linePath} fill="none" stroke={focusColor} strokeWidth="1.5" opacity="0.15" />
        {!isIdle && fillPath && <path d={fillPath} fill="url(#ultFillGrad)" />}
        {!isIdle && (
          <path
            d={points.slice(0, markerIdx + 1).map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')}
            fill="none" stroke={activeColor} strokeWidth="2.5" strokeLinecap="round"
            style={{ filter: `drop-shadow(0 0 3px ${activeColor}50)` }}
          />
        )}
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
        {isIdle && (
          <circle cx={points[0].x} cy={points[0].y} r="3" fill="rgba(245,247,251,0.2)" stroke="rgba(245,247,251,0.1)" strokeWidth="1" />
        )}
      </svg>

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
