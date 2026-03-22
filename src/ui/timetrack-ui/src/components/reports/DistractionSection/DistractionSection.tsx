/**
 * DistractionSection - Distraction statistics display
 *
 * SRP: Apenas exibe estatísticas de distração
 * OCP: Extensível via props
 * DIP: Recebe dados via props
 *
 * Composition: Composto por DistractionBar e TopDistractionItem
 */

import { useMemo } from 'react';
import { AlertTriangle, Clock, TrendingDown, TrendingUp } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import type { DistractionStatsResponse } from '../../../types/reports';

export interface DistractionSectionProps {
  /** Distraction stats data */
  data: DistractionStatsResponse | null;
  /** Loading state */
  isLoading?: boolean;
  /** Title */
  title?: string;
}

function formatTime(seconds: number): string {
  if (seconds === 0) return '0m';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

interface DistractionBarProps {
  date: string;
  distractionSeconds: number;
  maxSeconds: number;
}

function DistractionBar({ date, distractionSeconds, maxSeconds }: DistractionBarProps) {
  const percentage = maxSeconds > 0 ? (distractionSeconds / maxSeconds) * 100 : 0;

  // Parse date for display
  const dateObj = new Date(date);
  const dayLabel = dateObj.toLocaleDateString('pt-BR', { weekday: 'short', day: 'numeric' });

  return (
    <div className="flex items-center gap-2 min-w-0">
      <span className="text-[10px] text-[rgba(245,247,251,0.4)] w-[50px] text-right flex-shrink-0 truncate">
        {dayLabel}
      </span>
      <div className="flex-1 h-[12px] bg-[rgba(255,255,255,0.03)] rounded overflow-hidden min-w-[60px]">
        <div
          className="h-full bg-gradient-to-r from-[rgba(251,191,36,0.5)] to-[rgba(251,191,36,0.7)] rounded transition-all"
          style={{ width: `${Math.min(percentage, 100)}%` }}
        />
      </div>
      <span className="text-[10px] text-[rgba(245,247,251,0.5)] w-[35px] text-right flex-shrink-0">
        {formatTime(distractionSeconds)}
      </span>
    </div>
  );
}

interface TopDistractionItemProps {
  displayName: string;
  processName: string;
  totalSeconds: number;
  sessionCount: number;
  maxSeconds: number;
}

function TopDistractionItem({
  displayName,
  totalSeconds,
  sessionCount,
  maxSeconds,
}: TopDistractionItemProps) {
  const percentage = maxSeconds > 0 ? (totalSeconds / maxSeconds) * 100 : 0;

  return (
    <div className="flex items-center gap-2 min-w-0 py-0.5">
      <div
        className="w-4 h-4 rounded flex items-center justify-center flex-shrink-0"
        style={{ backgroundColor: 'rgba(251,191,36,0.15)' }}
      >
        <AlertTriangle className="w-2.5 h-2.5 text-[rgba(251,191,36,0.8)]" />
      </div>
      <span className="text-[11px] text-[rgba(245,247,251,0.8)] flex-1 truncate">
        {displayName}
      </span>
      <span className="text-[9px] text-[rgba(245,247,251,0.35)]">
        {sessionCount} sessões
      </span>
      <div className="w-[40px] h-1 bg-[rgba(255,255,255,0.05)] rounded-full overflow-hidden">
        <div
          className="h-full bg-[rgba(251,191,36,0.6)] rounded-full"
          style={{ width: `${Math.min(percentage, 100)}%` }}
        />
      </div>
      <span className="text-[10px] text-[rgba(245,247,251,0.4)] w-[35px] text-right flex-shrink-0">
        {formatTime(totalSeconds)}
      </span>
    </div>
  );
}

export function DistractionSection({
  data,
  isLoading = false,
  title = 'Análise de Distrações',
}: DistractionSectionProps) {
  // Calculate stats
  const stats = useMemo(() => {
    if (!data || data.dailyDistractions.length === 0) {
      return {
        totalDistraction: 0,
        avgDaily: 0,
        trend: 0,
        worstDay: null,
      };
    }

    const totalDistraction = data.dailyDistractions.reduce((sum, d) => sum + d.distractionSeconds, 0);
    const avgDaily = totalDistraction / data.dailyDistractions.length;

    // Calculate trend (compare last 7 days to previous 7)
    const last7 = data.dailyDistractions.slice(-7);
    const prev7 = data.dailyDistractions.slice(-14, -7);

    const last7Avg = last7.length > 0
      ? last7.reduce((sum, d) => sum + d.distractionSeconds, 0) / last7.length
      : 0;
    const prev7Avg = prev7.length > 0
      ? prev7.reduce((sum, d) => sum + d.distractionSeconds, 0) / prev7.length
      : last7Avg;

    const trend = prev7Avg > 0 ? ((last7Avg - prev7Avg) / prev7Avg) * 100 : 0;

    // Find worst day
    const worstDay = data.dailyDistractions.reduce(
      (max, d) => (d.distractionSeconds > max.distractionSeconds ? d : max),
      data.dailyDistractions[0]
    );

    return {
      totalDistraction,
      avgDaily,
      trend,
      worstDay,
    };
  }, [data]);

  // Max for bars
  const maxDaily = useMemo(() => {
    if (!data || data.dailyDistractions.length === 0) return 1;
    return Math.max(...data.dailyDistractions.map(d => d.distractionSeconds), 1);
  }, [data]);

  const maxTop = useMemo(() => {
    if (!data || data.topDistractions.length === 0) return 1;
    return Math.max(...data.topDistractions.map(d => d.totalSeconds), 1);
  }, [data]);

  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
        <CardHeader className="pb-2 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="flex items-center justify-center h-[200px]">
            <div className="w-6 h-6 border-2 border-[#4ad9ff] border-t-transparent rounded-full animate-spin" />
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!data || (data.dailyDistractions.length === 0 && data.topDistractions.length === 0)) {
    return (
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
        <CardHeader className="pb-2 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="flex flex-col items-center justify-center h-[120px] text-[rgba(245,247,251,0.4)]">
            <AlertTriangle className="w-6 h-6 mb-2 opacity-50" />
            <span className="text-[12px]">Nenhuma distração registrada</span>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
      <CardHeader className="pb-2 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
          {stats.trend !== 0 && (
            <div className={`flex items-center gap-1 ${stats.trend > 0 ? 'text-[rgba(251,191,36,0.7)]' : 'text-[rgba(5,223,114,0.7)]'}`}>
              {stats.trend > 0 ? (
                <TrendingUp className="w-3 h-3" />
              ) : (
                <TrendingDown className="w-3 h-3" />
              )}
              <span className="text-[10px]">
                {stats.trend > 0 ? '+' : ''}{stats.trend.toFixed(0)}%
              </span>
            </div>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4">
        {/* Summary Stats */}
        <div className="grid grid-cols-3 gap-2 mb-3">
          <div className="text-center p-2 rounded-lg bg-[rgba(251,191,36,0.08)]">
            <Clock className="w-3 h-3 mx-auto text-[rgba(251,191,36,0.6)]" />
            <p className="text-[11px] font-medium text-[rgba(245,247,251,0.8)] mt-1">
              {formatTime(stats.totalDistraction)}
            </p>
            <p className="text-[9px] text-[rgba(245,247,251,0.4)]">Total</p>
          </div>
          <div className="text-center p-2 rounded-lg bg-[rgba(255,255,255,0.03)]">
            <p className="text-[11px] font-medium text-[rgba(245,247,251,0.8)]">
              {formatTime(stats.avgDaily)}
            </p>
            <p className="text-[9px] text-[rgba(245,247,251,0.4)]">Média/dia</p>
          </div>
          <div className="text-center p-2 rounded-lg bg-[rgba(255,255,255,0.03)]">
            <p className="text-[11px] font-medium text-[rgba(245,247,251,0.8)]">
              {data.topDistractions.length}
            </p>
            <p className="text-[9px] text-[rgba(245,247,251,0.4)]">Fontes</p>
          </div>
        </div>

        {/* Daily bars (last 14 days) */}
        {data.dailyDistractions.length > 0 && (
          <div className="mb-3">
            <p className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1">Últimos 14 dias</p>
            <div className="space-y-0.5 max-h-[100px] overflow-y-auto">
              {data.dailyDistractions.slice(-14).map((day, i) => (
                <DistractionBar
                  key={`${day.date}-${i}`}
                  date={day.date}
                  distractionSeconds={day.distractionSeconds}
                  maxSeconds={maxDaily}
                />
              ))}
            </div>
          </div>
        )}

        {/* Top distractions */}
        {data.topDistractions.length > 0 && (
          <div>
            <p className="text-[10px] text-[rgba(245,247,251,0.4)] mb-1">Principais distrações</p>
            <div className="space-y-0.5 max-h-[100px] overflow-y-auto">
              {data.topDistractions.slice(0, 5).map((item, i) => (
                <TopDistractionItem
                  key={`${item.processName}-${i}`}
                  displayName={item.displayName}
                  processName={item.processName}
                  totalSeconds={item.totalSeconds}
                  sessionCount={item.sessionCount}
                  maxSeconds={maxTop}
                />
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
