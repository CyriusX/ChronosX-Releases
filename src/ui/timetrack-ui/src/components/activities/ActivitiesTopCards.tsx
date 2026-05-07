import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { formatDuration } from '../../lib/utils';
import { useTimerStore, selectCurrentUserSessions } from '../../stores/timerStore';
import { Clock } from 'lucide-react';
import { fadeUp, staggerContainer, STAGGER } from '../../lib/animation';
import { cardBase } from '../dashboard/shared/styles';
import type { useActivitiesData } from '../../hooks/useActivitiesData';

export function ActivitiesTopCards({
  summary,
  workGoalSeconds = 28800,
}: {
  summary: ReturnType<typeof useActivitiesData>['summary'];
  workGoalSeconds?: number;
}) {
  const { t } = useTranslation();
  const totalSeconds = summary?.totalDuration ?? 0;
  const idleSeconds = summary?.idleTime ?? 0;

  // Ring progress — Apple Health style
  const progressRatio = workGoalSeconds > 0 ? totalSeconds / workGoalSeconds : 0;
  const progressPercentage = Math.round(progressRatio * 100);
  const ringDash = Math.min(progressRatio, 1) * 264;
  const overflowDash = Math.min(Math.max(0, progressRatio - 1), 1) * 264;

  // Focus Score = average from Pomodoro/Ultradian focus sessions (timer store)
  const timerSessions = useTimerStore(selectCurrentUserSessions);
  const scoredFocusSessions = timerSessions.filter(s => s.phase === 'focus' && s.productivity >= 0);
  const focusSessionScore = scoredFocusSessions.length > 0
    ? Math.round(scoredFocusSessions.reduce((sum, s) => sum + s.productivity, 0) / scoredFocusSessions.length)
    : 0;
  const focusSessionCount = timerSessions.filter(s => s.phase === 'focus').length;

  const categories = summary?.categories ?? [];
  const productiveSecs = categories
    .filter(c => c.productivity === 'productive')
    .reduce((sum, c) => sum + c.duration, 0);
  const distractionSecs = categories
    .filter(c => c.productivity === 'distraction')
    .reduce((sum, c) => sum + c.duration, 0);
  const neutralSecs = Math.max(0, totalSeconds - productiveSecs - distractionSecs);

  const productivityScore = totalSeconds > 0
    ? Math.round((productiveSecs / totalSeconds) * 100)
    : 0;

  const scoreColor =
    productivityScore >= 80 ? '#05df72'
    : productivityScore >= 60 ? '#4ade80'
    : productivityScore >= 40 ? '#fbbf24'
    : productivityScore >= 20 ? '#fb923c'
    : '#f87171';

  const ringRadius = 42;
  const circumference = 2 * Math.PI * ringRadius;

  return (
    <motion.div
      className="grid grid-cols-1 sm:grid-cols-3 gap-4 flex-shrink-0"
      variants={staggerContainer(STAGGER.cards)}
      initial="hidden"
      animate="visible"
    >
      {/* Tempo Rastreado */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              {t('activities.timeTracked')}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-3 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="8" />
                  <circle cx="50" cy="50" r="42" fill="none" stroke="url(#actGrad1)" strokeWidth="8" strokeDasharray={`${ringDash} 264`} strokeLinecap="round" />
                  {overflowDash > 0 && (
                    <circle cx="50" cy="50" r="42" fill="none" stroke="url(#actGrad1)" strokeWidth="8" strokeDasharray={`${overflowDash} 264`} strokeLinecap="round" filter="url(#actGlow)" />
                  )}
                  <defs>
                    <linearGradient id="actGrad1" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor="#8B5CF6" />
                      <stop offset="100%" stopColor="#22D3EE" />
                    </linearGradient>
                    <filter id="actGlow">
                      <feGaussianBlur stdDeviation="2" result="blur" />
                      <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
                    </filter>
                  </defs>
                </svg>
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-[20px] font-semibold text-[#f5f7fb]">{formatDuration(totalSeconds)}</span>
                </div>
              </div>
              <div className="mt-3 text-center">
                <p className="text-[16px] font-semibold" style={{ color: progressRatio >= 1 ? '#05df72' : 'rgba(245,247,251,0.9)' }}>
                  {progressPercentage}%
                </p>
                <div className="flex items-center justify-center gap-1 mt-1">
                  <Clock className="w-3 h-3 text-[rgba(245,247,251,0.35)]" />
                  <span className="text-[10px] text-[rgba(245,247,251,0.45)]">{t('activities.idle')} {formatDuration(idleSeconds)}</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Foco */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              {t('activities.focus')}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2 pb-3 px-4">
            <div className="flex flex-col items-center">
              <div className="relative w-[100px] h-[100px]">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r={ringRadius} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="9" />
                  <circle
                    cx="50" cy="50" r={ringRadius}
                    fill="none"
                    stroke="url(#actGradFoco)"
                    strokeWidth="9"
                    strokeDasharray={`${(productivityScore / 100) * circumference} ${circumference}`}
                    strokeLinecap="round"
                  />
                  <defs>
                    <linearGradient id="actGradFoco" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor="#8B5CF6" />
                      <stop offset="100%" stopColor="#22D3EE" />
                    </linearGradient>
                  </defs>
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-[26px] font-bold" style={{ color: scoreColor }}>{productivityScore}</span>
                  <span className="text-[8px] text-[rgba(245,247,251,0.4)] -mt-0.5">SCORE</span>
                </div>
              </div>
              <div className="mt-2.5 w-full space-y-1">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#4ade80]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">{t('activities.productive')}</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(productiveSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#fbbf24]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">{t('activities.neutral')}</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(neutralSecs)}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-[#f87171]" /><span className="text-[10px] text-[rgba(245,247,251,0.6)]">{t('activities.distraction')}</span></div>
                  <span className="text-[10px] text-[rgba(245,247,251,0.8)]">{formatDuration(distractionSecs)}</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Resumo do dia */}
      <motion.div variants={fadeUp} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
              {t('activities.dailySummary')}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-4 pb-3 px-4">
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('activities.productiveTime')}</span>
                <span className="text-[16px] font-bold text-[#4ade80]">{formatDuration(productiveSecs)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('activities.idleTime')}</span>
                <span className="text-[16px] font-bold text-[rgba(245,247,251,0.5)]">{formatDuration(idleSeconds)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('activities.focusScore')}</span>
                <div className="flex items-center gap-1.5">
                  <span className="text-[16px] font-bold" style={{
                    color: focusSessionScore >= 80 ? '#4ade80' : focusSessionScore >= 50 ? '#fbbf24' : '#f87171'
                  }}>{focusSessionScore}</span>
                  <span className="text-[9px] text-[rgba(245,247,251,0.3)]">
                    ({focusSessionCount} {focusSessionCount === 1 ? t('activities.session') : t('activities.sessions')})
                  </span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </motion.div>
  );
}

