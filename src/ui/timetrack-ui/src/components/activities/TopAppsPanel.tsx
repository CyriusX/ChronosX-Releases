import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { AppIcon } from '../dashboard/shared';
import { fadeUp, staggerContainer, STAGGER } from '../../lib/animation';
import { formatDuration } from '../../lib/utils';
import { cardBase } from '../dashboard/shared/styles';
import type { useActivitiesData } from '../../hooks/useActivitiesData';

export function TopAppsPanel({ summary }: { summary: ReturnType<typeof useActivitiesData>['summary'] }) {
  const { t } = useTranslation();
  const apps = [...(summary?.topApplications ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 8);

  const totalTime = apps.reduce((sum, a) => sum + a.duration, 0);

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
          {t('activities.topApps')}
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-3 px-4">
        {apps.length === 0 ? (
          <p className="text-[11px] text-[rgba(245,247,251,0.3)] text-center py-3">{t('activities.noApps')}</p>
        ) : (
          <motion.div
            className="space-y-2"
            variants={staggerContainer(STAGGER.listItems)}
            initial="hidden"
            animate="visible"
          >
            {apps.map((app, i) => {
              const pct = totalTime > 0 ? Math.round((app.duration / totalTime) * 100) : 0;
              return (
                <motion.div key={i} className="flex items-center gap-2" variants={fadeUp}>
                  <AppIcon name={app.name} size={14} />
                  <span className="text-[10px] text-[rgba(245,247,251,0.7)] flex-1 truncate">{app.name}</span>
                  <span className="text-[9px] text-[rgba(245,247,251,0.4)] tabular-nums">{formatDuration(app.duration)}</span>
                  <span className="text-[8px] text-[rgba(245,247,251,0.3)] w-[28px] text-right tabular-nums">{pct}%</span>
                </motion.div>
              );
            })}
          </motion.div>
        )}
      </CardContent>
    </Card>
  );
}

