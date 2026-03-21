import { useEffect, useRef } from 'react';
import {
  TrendingUp, Globe, Briefcase, Code2, Users, MessageSquare,
  Play, Paintbrush, Video
} from 'lucide-react';
import { motion } from 'motion/react';
import { animate } from 'animejs';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { CategoryItem, ProjectItem, AppIcon } from './shared';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse, AppProductivityCategory } from '../../types/ipc';
import { fadeUp, staggerContainer, STAGGER, SPRING, TIMING_MS } from '../../lib/animation';

interface BottomCardsProps {
  summary: TodaySummaryResponse | null;
}

const getProductivityBadgeStyle = (productivity?: AppProductivityCategory): string => {
  switch (productivity) {
    case 'productive': return 'bg-[rgba(74,222,128,0.15)] border-[rgba(74,222,128,0.25)] text-[#4ade80]';
    case 'distraction': return 'bg-[rgba(248,113,113,0.15)] border-[rgba(248,113,113,0.25)] text-[#f87171]';
    default: return 'bg-[rgba(251,191,36,0.15)] border-[rgba(251,191,36,0.25)] text-[#fbbf24]';
  }
};

const categoryIcons: Record<string, React.ReactNode> = {
  'development': <Code2 className="w-3 h-3" />,
  'meetings': <Video className="w-3 h-3" />,
  'communication': <MessageSquare className="w-3 h-3" />,
  'design': <Paintbrush className="w-3 h-3" />,
  'productivity_tools': <TrendingUp className="w-3 h-3" />,
  'browser_general': <Globe className="w-3 h-3" />,
  'social_media': <Users className="w-3 h-3" />,
  'entertainment': <Play className="w-3 h-3" />,
};

const cardBase = "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl overflow-hidden hover:border-[rgba(74,217,255,0.15)] hover:shadow-[0_0_0_1px_rgba(74,217,255,0.08),0_4px_20px_rgba(74,217,255,0.04)] transition-all duration-200";

export function BottomCards({ summary }: BottomCardsProps) {
  const categories = summary?.categories ?? [];

  const topApplications = [...(summary?.topApplications ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 5);

  const topProjects = [...(summary?.topProjects ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 5);

  const totalAppTime = topApplications.reduce((sum, app) => sum + app.duration, 0);
  const totalProjectTime = topProjects.reduce((sum, proj) => sum + proj.duration, 0);

  // Animate project progress bars
  const barRefs = useRef<(HTMLDivElement | null)[]>([]);
  useEffect(() => {
    barRefs.current.forEach((el, i) => {
      if (!el) return;
      const pct = totalProjectTime > 0
        ? Math.round((topProjects[i]?.duration ?? 0) / totalProjectTime * 100)
        : 0;
      animate(el, {
        width: [`0%`, `${pct}%`],
        duration: TIMING_MS.deliberate,
        ease: 'outQuart',
        delay: 200 + i * 80,
      });
    });
  }, [topProjects.length, totalProjectTime]);

  return (
    <motion.div
      variants={staggerContainer(STAGGER.cards)}
      initial="hidden"
      animate="visible"
      className="grid grid-cols-3 gap-4 flex-shrink-0"
    >
      {/* Categorias */}
      <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className="w-5 h-5 rounded-md bg-gradient-to-br from-[rgba(5,223,114,0.2)] to-[rgba(0,213,190,0.2)] border border-[rgba(5,223,114,0.3)] flex items-center justify-center">
                  <TrendingUp className="w-3 h-3 text-[#05df72]" />
                </div>
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Categorias</span>
              </div>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2.5 pb-3 px-4">
            <motion.div
              variants={staggerContainer(STAGGER.listItems)}
              initial="hidden"
              animate="visible"
              className="space-y-2"
            >
              {categories.length > 0 ? (
                categories.slice(0, 5).map((category, index) => (
                  <motion.div key={index} variants={fadeUp}>
                    <CategoryItem
                      percentage={category.percentage}
                      icon={categoryIcons[category.name] ?? <TrendingUp className="w-3 h-3" />}
                      label={category.name}
                      time={formatDuration(category.duration)}
                      color={category.color}
                    />
                  </motion.div>
                ))
              ) : (
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-3">Nenhuma categoria</p>
              )}
            </motion.div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Apps & Sites */}
      <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className="w-5 h-5 rounded-md bg-gradient-to-br from-[rgba(81,162,255,0.2)] to-[rgba(0,211,243,0.2)] border border-[rgba(81,162,255,0.3)] flex items-center justify-center">
                  <Globe className="w-3 h-3 text-[#51a2ff]" />
                </div>
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Apps & Sites</span>
              </div>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2.5 pb-3 px-4">
            <motion.div
              variants={staggerContainer(STAGGER.listItems)}
              initial="hidden"
              animate="visible"
              className="space-y-2"
            >
              {topApplications.length > 0 ? (
                topApplications.map((app, index) => {
                  const percentage = totalAppTime > 0
                    ? Math.round((app.duration / totalAppTime) * 100)
                    : Math.round(app.percentage);
                  const badgeStyle = getProductivityBadgeStyle(app.productivity);

                  return (
                    <motion.div key={index} variants={fadeUp} className="flex items-center gap-2 min-w-0">
                      <span className="text-[11px] text-[rgba(245,247,251,0.4)] w-7 text-right flex-shrink-0">{percentage}%</span>
                      <AppIcon name={app.name} size={14} />
                      <span className="text-[11px] text-[rgba(245,247,251,0.8)] flex-1 truncate min-w-0">{app.name}</span>
                      <span className="text-[11px] text-[rgba(245,247,251,0.4)] flex-shrink-0">{formatDuration(app.duration)}</span>
                      {app.productivity && (
                        <motion.span
                          initial={{ scale: 0 }}
                          animate={{ scale: 1 }}
                          transition={{ type: 'spring', stiffness: 400, damping: 15, delay: 0.3 + index * 0.05 }}
                          className={`px-1.5 py-0.5 text-[8px] rounded-full border flex-shrink-0 ${badgeStyle}`}
                        >
                          {app.productivity === 'productive' ? 'P' : app.productivity === 'distraction' ? 'D' : 'N'}
                        </motion.span>
                      )}
                    </motion.div>
                  );
                })
              ) : (
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-3">Nenhum app</p>
              )}
            </motion.div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Projetos */}
      <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
        <Card className={`${cardBase} h-full`}>
          <CardHeader className="pb-0 pt-3 px-4">
            <CardTitle className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className="w-5 h-5 rounded-md bg-gradient-to-br from-[rgba(194,122,255,0.2)] to-[rgba(246,51,154,0.2)] border border-[rgba(194,122,255,0.3)] flex items-center justify-center">
                  <Briefcase className="w-3 h-3 text-[#c27aff]" />
                </div>
                <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Projetos</span>
              </div>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-2.5 pb-3 px-4">
            <motion.div
              variants={staggerContainer(STAGGER.listItems)}
              initial="hidden"
              animate="visible"
              className="space-y-2"
            >
              {topProjects.length > 0 ? (
                topProjects.map((project, index) => {
                  const barColors = ['#4ad9ff', '#c27aff', '#05df72', '#ff9c5b', '#f87171'];
                  const percentage = totalProjectTime > 0
                    ? Math.round((project.duration / totalProjectTime) * 100)
                    : Math.round(project.percentage);
                  return (
                    <motion.div key={index} variants={fadeUp}>
                      <ProjectItem
                        percentage={percentage}
                        label={project.name}
                        time={formatDuration(project.duration)}
                        barColor={barColors[index % barColors.length]}
                        barWidth={percentage}
                        barRef={(el) => { barRefs.current[index] = el; }}
                      />
                    </motion.div>
                  );
                })
              ) : (
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-3">Nenhum projeto</p>
              )}
            </motion.div>
          </CardContent>
        </Card>
      </motion.div>
    </motion.div>
  );
}
