import { MoreVertical, TrendingUp, Globe, Briefcase, Code2, Users, MessageSquare, Search as SearchIcon, Music } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { CategoryItem, AppItem, ProjectItem } from './shared';
import { formatDuration } from '../../lib/utils';
import type { TodaySummaryResponse } from '../../types/ipc';

interface BottomCardsProps {
  summary: TodaySummaryResponse | null;
}

// Icon mapping for categories
const categoryIcons: Record<string, React.ReactNode> = {
  'Desenvolvimento': <Code2 className="w-3 h-3" />,
  'Reuniões': <Users className="w-3 h-3" />,
  'Pesquisa': <SearchIcon className="w-3 h-3" />,
  'Comunicação': <MessageSquare className="w-3 h-3" />,
};

// Icon mapping for applications
const appIcons: Record<string, React.ReactNode> = {
  'VS Code': <Code2 className="w-[14px] h-[14px]" />,
  'Chrome': <Globe className="w-[14px] h-[14px]" />,
  'Slack': <MessageSquare className="w-[14px] h-[14px]" />,
  'Spotify': <Music className="w-[14px] h-[14px]" />,
};

// Default icon for unknown apps
const defaultAppIcon = <Globe className="w-[14px] h-[14px]" />;

export function BottomCards({ summary }: BottomCardsProps) {
  // Get categories from summary or use empty array
  const categories = summary?.categories ?? [];

  // Get top applications sorted by duration
  const topApplications = [...(summary?.topApplications ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 4);

  // Get top projects sorted by duration
  const topProjects = [...(summary?.topProjects ?? [])]
    .sort((a, b) => b.duration - a.duration)
    .slice(0, 4);

  // Calculate total for percentage calculations
  const totalAppTime = topApplications.reduce((sum, app) => sum + app.duration, 0);
  const totalProjectTime = topProjects.reduce((sum, proj) => sum + proj.duration, 0);

  return (
    <div className="grid grid-cols-3 gap-6">
      {/* Categorias Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-5 h-5 rounded-lg bg-gradient-to-br from-[rgba(5,223,114,0.2)] to-[rgba(0,213,190,0.2)] border border-[rgba(5,223,114,0.3)] flex items-center justify-center">
                <TrendingUp className="w-3 h-3 text-[#05df72]" />
              </div>
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Categorias</span>
            </div>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="space-y-[10px]">
            {categories.length > 0 ? (
              categories.slice(0, 4).map((category, index) => (
                <CategoryItem
                  key={index}
                  percentage={category.percentage}
                  icon={categoryIcons[category.name] ?? <TrendingUp className="w-3 h-3" />}
                  label={category.name}
                  time={formatDuration(category.duration)}
                  color={category.color}
                />
              ))
            ) : (
              <p className="text-[12px] text-[rgba(245,247,251,0.4)] text-center py-4">
                Nenhuma categoria registrada
              </p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Apps & Sites Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-5 h-5 rounded-lg bg-gradient-to-br from-[rgba(81,162,255,0.2)] to-[rgba(0,211,243,0.2)] border border-[rgba(81,162,255,0.3)] flex items-center justify-center">
                <Globe className="w-3 h-3 text-[#51a2ff]" />
              </div>
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Apps & Sites</span>
            </div>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="space-y-[10px]">
            {topApplications.length > 0 ? (
              topApplications.map((app, index) => {
                const percentage = totalAppTime > 0
                  ? Math.round((app.duration / totalAppTime) * 100)
                  : app.percentage;

                return (
                  <AppItem
                    key={index}
                    percentage={percentage}
                    icon={appIcons[app.name] ?? defaultAppIcon}
                    label={app.name}
                    time={formatDuration(app.duration)}
                    color="rgba(255,255,255,0.1)"
                  />
                );
              })
            ) : (
              <p className="text-[12px] text-[rgba(245,247,251,0.4)] text-center py-4">
                Nenhum app registrado
              </p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Projetos Card */}
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-5 h-5 rounded-lg bg-gradient-to-br from-[rgba(194,122,255,0.2)] to-[rgba(81,162,255,0.2)] border border-[rgba(194,122,255,0.3)] flex items-center justify-center">
                <Briefcase className="w-3 h-3 text-[#c27aff]" />
              </div>
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Projetos</span>
            </div>
            <button className="w-4 h-4 flex items-center justify-center">
              <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
            </button>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-4 px-[17px]">
          <div className="space-y-[12px]">
            {topProjects.length > 0 ? (
              topProjects.map((project, index) => {
                const barWidth = totalProjectTime > 0
                  ? Math.round((project.duration / totalProjectTime) * 100)
                  : project.percentage;

                // Color rotation for bars
                const colors = ['#4ad9ff', '#8b7aff', '#ff9c5b', '#54e4c5'];
                const barColor = colors[index % colors.length];

                return (
                  <ProjectItem
                    key={index}
                    percentage={project.percentage}
                    label={project.name}
                    time={formatDuration(project.duration)}
                    barColor={barColor}
                    barWidth={barWidth}
                  />
                );
              })
            ) : (
              <p className="text-[12px] text-[rgba(245,247,251,0.4)] text-center py-4">
                Nenhum projeto registrado
              </p>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
