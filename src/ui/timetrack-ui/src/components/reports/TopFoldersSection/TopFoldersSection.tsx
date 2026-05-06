/**
 * TopFoldersSection - Top folders list (derived from ActivitySessions.FilePath)
 */

import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { Folder } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { fadeUp, staggerContainer, STAGGER } from '../../../lib/animation';
import type { TopFolderItem } from '../../../types/reports';

export interface TopFoldersSectionProps {
  folders: TopFolderItem[];
  isLoading?: boolean;
  title?: string;
  maxItems?: number;
}

function formatTime(seconds: number): string {
  if (seconds <= 0) return '0m';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  return `${minutes}m`;
}

export function TopFoldersSection({
  folders,
  isLoading = false,
  title: titleProp,
  maxItems = 10,
}: TopFoldersSectionProps) {
  const { t } = useTranslation();
  const title = titleProp ?? t('reports.topFolders');

  const displayed = useMemo(() => folders.slice(0, maxItems), [folders, maxItems]);
  const maxSeconds = useMemo(() => (folders.length ? Math.max(...folders.map(f => f.totalSeconds), 1) : 1), [folders]);

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
            <div className="w-6 h-6 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl h-full flex flex-col">
      <CardHeader className="pb-2 pt-3 px-4 shrink-0">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
            {folders.length} {t('reports.items')}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4 flex-1 overflow-hidden">
        {displayed.length === 0 ? (
          <div className="flex items-center justify-center h-30 text-[rgba(245,247,251,0.4)] text-[12px]">
            {t('reports.noDataToShow')}
          </div>
        ) : (
          <motion.div
            className="space-y-1 h-full overflow-y-auto"
            variants={staggerContainer(STAGGER.listItems)}
            initial="hidden"
            animate="visible"
          >
            {displayed.map((f) => {
              const pct = (f.totalSeconds / maxSeconds) * 100;
              return (
                <motion.div
                  key={f.folderPath}
                  variants={fadeUp}
                  className="flex items-center gap-3 py-2 px-3 rounded-lg hover:bg-[rgba(255,255,255,0.02)] border border-transparent"
                  title={f.folderPath}
                >
                  <span className="text-[10px] text-[rgba(245,247,251,0.4)] w-8 text-right shrink-0 tabular-nums">
                    {f.visitCount}x
                  </span>
                  <div className="w-5 h-5 rounded-lg flex items-center justify-center shrink-0 bg-[rgba(5,223,114,0.12)]">
                    <Folder className="w-3 h-3 text-[rgba(5,223,114,0.8)]" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[12px] text-[rgba(245,247,251,0.85)] font-medium truncate font-mono">
                      {f.folderPath}
                    </div>
                    <div className="w-full h-2 bg-[rgba(255,255,255,0.05)] rounded-full overflow-hidden mt-1">
                      <motion.div
                        className="h-full rounded-full"
                        initial={{ width: 0 }}
                        animate={{ width: `${Math.min(pct, 100)}%` }}
                        transition={{ duration: 0.5, ease: 'easeOut' }}
                        style={{ background: 'linear-gradient(90deg, rgba(5,223,114,0.55), rgba(5,223,114,0.25))' }}
                      />
                    </div>
                  </div>
                  <span className="text-[11px] text-[rgba(245,247,251,0.5)] w-12 text-right shrink-0 tabular-nums font-medium">
                    {formatTime(f.totalSeconds)}
                  </span>
                </motion.div>
              );
            })}
          </motion.div>
        )}
      </CardContent>
    </Card>
  );
}

