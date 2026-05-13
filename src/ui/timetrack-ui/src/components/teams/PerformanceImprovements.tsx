import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import {
  TrendingUp, AlertTriangle, AlertCircle, Info, CheckCircle2,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { MemberSummaryResponse } from '../../types/member';
import { fadeUp, staggerContainer, STAGGER } from '../../lib/animation';

type Severity = 'critical' | 'warning' | 'info';

interface ImprovementItem {
  id: string;
  severity: Severity;
  titleKey: string;
  descriptionKey: string;
  descriptionVars?: Record<string, string | number>;
  details?: { label: string; value: string }[];
}

interface PerformanceImprovementsProps {
  summary: MemberSummaryResponse;
}

function formatDurationShort(seconds: number): string {
  if (!seconds || seconds <= 0) return '0m';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${String(m).padStart(2, '0')}m`;
  return `${m}m`;
}

function analyzePerformance(summary: MemberSummaryResponse): ImprovementItem[] {
  const items: ImprovementItem[] = [];
  const { totalDuration, productiveTime, idleTime, focusScore, sessionsCount } = summary;
  if (totalDuration <= 0) return items;

  const productivityRatio = productiveTime / totalDuration;
  const productivityPct = Math.round(productivityRatio * 100);
  const idleRatio = idleTime / totalDuration;
  const idlePct = Math.round(idleRatio * 100);

  // Focus score
  if (focusScore < 40) {
    items.push({
      id: 'focus-critical',
      severity: 'critical',
      titleKey: 'teams.improvements.focusCritical',
      descriptionKey: 'teams.improvements.focusCriticalDesc',
    });
  } else if (focusScore < 60) {
    items.push({
      id: 'focus-warning',
      severity: 'warning',
      titleKey: 'teams.improvements.focusWarning',
      descriptionKey: 'teams.improvements.focusWarningDesc',
    });
  }

  // Productivity ratio
  if (productivityRatio < 0.4) {
    items.push({
      id: 'productivity-critical',
      severity: 'critical',
      titleKey: 'teams.improvements.productivityCritical',
      descriptionKey: 'teams.improvements.productivityCriticalDesc',
      descriptionVars: { percentage: productivityPct },
    });
  } else if (productivityRatio < 0.6) {
    items.push({
      id: 'productivity-warning',
      severity: 'warning',
      titleKey: 'teams.improvements.productivityWarning',
      descriptionKey: 'teams.improvements.productivityWarningDesc',
      descriptionVars: { percentage: productivityPct },
    });
  }

  // Idle time
  if (idleRatio > 0.4) {
    items.push({
      id: 'idle-critical',
      severity: 'critical',
      titleKey: 'teams.improvements.idleCritical',
      descriptionKey: 'teams.improvements.idleCriticalDesc',
      descriptionVars: { percentage: idlePct },
    });
  } else if (idleRatio > 0.25) {
    items.push({
      id: 'idle-warning',
      severity: 'warning',
      titleKey: 'teams.improvements.idleWarning',
      descriptionKey: 'teams.improvements.idleWarningDesc',
      descriptionVars: { percentage: idlePct },
    });
  }

  // Distraction apps
  const distractionApps = (summary.topApplications ?? [])
    .filter(a => a.productivity === 'distraction' && totalDuration > 0 && (a.duration / totalDuration) > 0.1)
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 3);

  if (distractionApps.length > 0) {
    items.push({
      id: 'distraction-apps',
      severity: 'warning',
      titleKey: 'teams.improvements.distractionApps',
      descriptionKey: 'teams.improvements.distractionAppsDesc',
      details: distractionApps.map(a => ({
        label: a.name,
        value: `${formatDurationShort(a.duration)} (${Math.round(a.percentage)}%)`,
      })),
    });
  }

  // Distraction categories
  const distractionCategories = (summary.categories ?? [])
    .filter(c => c.productivity === 'distraction' && c.percentage > 20)
    .slice(0, 2);

  if (distractionCategories.length > 0) {
    items.push({
      id: 'distraction-categories',
      severity: 'info',
      titleKey: 'teams.improvements.distractionCategories',
      descriptionKey: 'teams.improvements.distractionCategoriesDesc',
      details: distractionCategories.map(c => ({
        label: c.name,
        value: `${Math.round(c.percentage)}%`,
      })),
    });
  }

  // Low session count
  if (sessionsCount < 3 && totalDuration > 7200) {
    items.push({
      id: 'low-sessions',
      severity: 'info',
      titleKey: 'teams.improvements.lowSessions',
      descriptionKey: 'teams.improvements.lowSessionsDesc',
      descriptionVars: { count: sessionsCount, hours: Math.round(totalDuration / 3600) },
    });
  }

  // Sort by severity: critical > warning > info
  const severityOrder: Record<Severity, number> = { critical: 0, warning: 1, info: 2 };
  items.sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity]);

  return items.slice(0, 5);
}

const severityConfig: Record<Severity, { icon: LucideIcon; borderColor: string; iconColor: string; bgColor: string }> = {
  critical: { icon: AlertTriangle, borderColor: 'border-l-[#f87171]', iconColor: 'text-[#f87171]', bgColor: 'bg-[rgba(248,113,113,0.08)]' },
  warning: { icon: AlertCircle, borderColor: 'border-l-[#fbbf24]', iconColor: 'text-[#fbbf24]', bgColor: 'bg-[rgba(251,191,36,0.08)]' },
  info: { icon: Info, borderColor: 'border-l-[#22D3EE]', iconColor: 'text-[#22D3EE]', bgColor: 'bg-[rgba(34,211,238,0.08)]' },
};

export function PerformanceImprovements({ summary }: PerformanceImprovementsProps) {
  const { t } = useTranslation();
  const items = useMemo(() => analyzePerformance(summary), [summary]);

  if (summary.totalDuration <= 0) return null;

  return (
    <div>
      <h3 className="text-[11px] font-medium text-[rgba(245,247,251,0.45)] uppercase tracking-wider mb-2.5 flex items-center gap-1.5">
        <TrendingUp className="w-3.5 h-3.5" />
        {t('teams.improvements')}
      </h3>

      {items.length === 0 ? (
        <div className="flex items-center gap-2 px-3 py-3 rounded-[12px] bg-[rgba(5,223,114,0.06)] border border-[rgba(5,223,114,0.15)]">
          <CheckCircle2 className="w-4 h-4 text-[#05df72] flex-shrink-0" />
          <span className="text-[12px] text-[rgba(245,247,251,0.6)]">
            {t('teams.improvements.allGood')}
          </span>
        </div>
      ) : (
        <motion.div
          variants={staggerContainer(STAGGER.listItems)}
          initial="hidden"
          animate="visible"
          className="space-y-2"
        >
          {items.map(item => {
            const config = severityConfig[item.severity];
            const Icon = config.icon;
            return (
              <motion.div
                key={item.id}
                variants={fadeUp}
                className={`rounded-[12px] border border-[rgba(255,255,255,0.05)] border-l-2 ${config.borderColor} ${config.bgColor} p-3`}
              >
                <div className="flex items-start gap-2.5">
                  <Icon className={`w-4 h-4 flex-shrink-0 mt-0.5 ${config.iconColor}`} />
                  <div className="flex-1 min-w-0">
                    <p className="text-[12px] font-medium text-[rgba(245,247,251,0.9)]">
                      {t(item.titleKey)}
                    </p>
                    <p className="text-[11px] text-[rgba(245,247,251,0.5)] mt-0.5 leading-relaxed">
                      {t(item.descriptionKey, item.descriptionVars ?? {})}
                    </p>
                    {item.details && item.details.length > 0 && (
                      <div className="mt-2 space-y-1">
                        {item.details.map((d, i) => (
                          <div key={i} className="flex items-center justify-between">
                            <span className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">{d.label}</span>
                            <span className="text-[10px] text-[rgba(245,247,251,0.6)] ml-2 flex-shrink-0">{d.value}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </motion.div>
            );
          })}
        </motion.div>
      )}
    </div>
  );
}
