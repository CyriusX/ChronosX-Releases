import { MoreVertical, ChevronDown } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse } from '../../types/ipc';

interface TopCardsProps {
  summary: TodaySummaryResponse | null;
  isPaused: boolean;
  isTracking: boolean;
  onStartTracking: () => void;
  onPauseTracking: () => void;
  onStopTracking: () => void;
}

export function TopCards({
  summary,
  isPaused,
  isTracking,
  onStartTracking,
  onPauseTracking,
  onStopTracking,
}: TopCardsProps) {
  // Calculate values from summary
  const totalMinutes = summary?.totalDuration ?? 0;
  const focusTime = summary?.focusTime ?? 0;
  const sessionsCount = summary?.sessionsCount ?? 0;
  const productiveTime = summary?.productiveTime ?? 0;

  // Focus percentage (0-100)
  const focusPercentage = totalMinutes > 0 ? Math.round((focusTime / totalMinutes) * 100) : 0;

  // Progress towards 8-hour goal
  const progressPercentage = Math.min((totalMinutes / (8 * 60)) * 100, 100);

  // Current project from top applications
  const currentProject = summary?.topProjects?.[0]?.name ?? 'Sem projeto';

  return (
    <div className="grid grid-cols-3 gap-6">
      {/* Tempo Rastreado Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Tempo rastreado</span>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="flex flex-col items-center">
            <div className="relative w-[128px] h-[128px]">
              <svg className="w-full h-full -rotate-90" viewBox="0 0 128 128">
                <circle cx="64" cy="64" r="56" fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="12" />
                <circle cx="64" cy="64" r="56" fill="none" stroke="url(#gradient1)" strokeWidth="12" strokeDasharray={`${progressPercentage * 3.52} 352`} strokeLinecap="round" />
                <defs>
                  <linearGradient id="gradient1" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stopColor="#4ad9ff" />
                    <stop offset="100%" stopColor="#3c7bff" />
                  </linearGradient>
                </defs>
              </svg>
              <div className="absolute inset-0 flex items-center justify-center">
                <span className="text-[24px] font-semibold text-[#f5f7fb]">{formatDuration(totalMinutes)}</span>
              </div>
            </div>
            <div className="mt-4 text-center">
              <p className="text-[24px] font-semibold text-[rgba(245,247,251,0.9)]">{Math.round(progressPercentage)}%</p>
              <div className="flex items-center justify-center gap-[6px] mt-1">
                <div className="w-1 h-1 rounded-full bg-[#00d3f3]" />
                <span className="text-[10px] text-[rgba(245,247,251,0.4)] tracking-[0.25px] uppercase">vs ontem</span>
              </div>
              <p className="text-[10px] text-[rgba(245,247,251,0.3)] mt-2">Meta de hoje</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Foco Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Foco</span>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="flex flex-col items-center">
            <div className="relative w-[128px] h-[128px]">
              <svg className="w-full h-full -rotate-90" viewBox="0 0 128 128">
                <circle cx="64" cy="64" r="56" fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="12" />
                <circle cx="64" cy="64" r="56" fill="none" stroke="#05df72" strokeWidth="12" strokeDasharray={`${focusPercentage * 3.52} 352`} strokeLinecap="round" />
              </svg>
              <div className="absolute inset-0 flex items-center justify-center">
                <span className="text-[36px] font-semibold text-[#f5f7fb]">{focusPercentage}</span>
              </div>
            </div>
            <div className="mt-4 text-center space-y-1">
              <p className="text-[12px] text-[rgba(245,247,251,0.5)]">
                Produtivo: <span className="text-[rgba(245,247,251,0.8)]">{formatDuration(productiveTime)}</span>
              </p>
              <p className="text-[12px] text-[rgba(245,247,251,0.5)]">
                Sessões: <span className="text-[rgba(245,247,251,0.8)]">{sessionsCount}</span>
              </p>
            </div>
            <div className="flex gap-2 mt-4">
              <span className="px-[10px] py-1 text-[10px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]">
                Foco: {focusPercentage}%
              </span>
              <span className="px-[10px] py-1 text-[10px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.5)]">
                {sessionsCount} sessões
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Timer Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Timer</span>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="flex items-center gap-2 pl-2 mb-3">
            <div
              className={`w-2 h-2 rounded-full ${
                !isTracking
                  ? 'bg-[#ff5f5f] opacity-70'
                  : isPaused
                    ? 'bg-[#f3d05d] opacity-70'
                    : 'bg-[#c8db68] opacity-50 shadow-[0px_10px_15px_0px_rgba(200,219,104,0.6),0px_4px_6px_0px_rgba(200,219,104,0.6)]'
              }`}
            />
            <span className="text-[12px] text-[rgba(245,247,251,0.6)]">
              {!isTracking ? 'Parado' : isPaused ? 'Pausado' : 'Em andamento'}
            </span>
          </div>
          <div className="bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] rounded-[10px] px-[9px] py-2 flex items-center justify-between mb-2">
            <div>
              <p className="text-[10px] text-[rgba(245,247,251,0.4)]">Projetos</p>
              <p className="text-[14px] text-[rgba(245,247,251,0.9)]">{currentProject}</p>
            </div>
            <ChevronDown className="w-[14px] h-[14px] text-[rgba(245,247,251,0.4)]" />
          </div>
          <div className="bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] rounded-[10px] px-[9px] py-2 flex items-center justify-between mb-3">
            <p className="text-[10px] text-[rgba(245,247,251,0.4)]">Tags</p>
            <ChevronDown className="w-[14px] h-[14px] text-[rgba(245,247,251,0.4)]" />
          </div>
          <div className="flex gap-2 pl-2 mb-4">
            <span className="px-[13px] py-[5px] text-[12px] rounded-[8px] bg-gradient-to-r from-[rgba(255,105,0,0.2)] to-[rgba(240,177,0,0.2)] border border-[rgba(255,137,4,0.2)] text-[rgba(245,247,251,0.8)]">UI</span>
            <span className="px-[13px] py-[5px] text-[12px] rounded-[8px] bg-gradient-to-r from-[rgba(43,127,255,0.2)] to-[rgba(0,184,219,0.2)] border border-[rgba(81,162,255,0.2)] text-[rgba(245,247,251,0.8)]">Design</span>
            <span className="px-[13px] py-[5px] text-[12px] rounded-[8px] bg-gradient-to-r from-[rgba(173,70,255,0.2)] to-[rgba(246,51,154,0.2)] border border-[rgba(194,122,255,0.2)] text-[rgba(245,247,251,0.8)]">Core</span>
          </div>
          <div className="flex gap-2">
            {!isTracking && !isPaused ? (
              <button
                onClick={onStartTracking}
                className="flex-1 py-[8px] rounded-full bg-gradient-to-b from-[#05df72] to-[#00b359] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
              >
                Iniciar
              </button>
            ) : (
              <>
                <button
                  onClick={onPauseTracking}
                  className="flex-1 py-[8px] rounded-full bg-gradient-to-b from-[#4ad9ff] to-[#3c7bff] text-[12px] font-medium text-white hover:opacity-90 transition-opacity"
                >
                  {isPaused ? 'Retomar' : 'Pausar'}
                </button>
                <button
                  onClick={onStopTracking}
                  className="flex-1 py-[9px] rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[12px] font-medium text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                >
                  Finalizar
                </button>
              </>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
