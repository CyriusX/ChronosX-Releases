import { useMemo } from 'react';
import { motion } from 'motion/react';
import { Folder } from 'lucide-react';
import { formatDuration } from '../../lib/utils';
import { fadeUp, staggerContainer, STAGGER } from '../../lib/animation';
import type { TopFolderItem } from '../../types/reports';

export interface FoldersListProps {
  folders: TopFolderItem[];
  maxItems?: number;
}

export function FoldersList({ folders, maxItems = 20 }: FoldersListProps) {
  const displayed = useMemo(() => folders.slice(0, maxItems), [folders, maxItems]);
  const maxSeconds = useMemo(() => (folders.length ? Math.max(...folders.map(f => f.totalSeconds), 1) : 1), [folders]);

  if (displayed.length === 0) return null;

  return (
    <motion.div
      className="space-y-1 max-h-[420px] overflow-y-auto"
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
            className="flex items-start gap-3 py-2 px-3 rounded-lg hover:bg-[rgba(255,255,255,0.02)] border border-transparent"
            title={f.folderPath}
          >
            <span className="text-[10px] text-[rgba(245,247,251,0.4)] w-8 text-right shrink-0 tabular-nums pt-[2px]">
              {f.visitCount}x
            </span>
            <div className="w-5 h-5 rounded-lg flex items-center justify-center shrink-0 bg-[rgba(5,223,114,0.12)] mt-[1px]">
              <Folder className="w-3 h-3 text-[rgba(5,223,114,0.8)]" />
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-[11px] text-[rgba(245,247,251,0.85)] font-medium font-mono break-all whitespace-normal">
                {f.folderPath}
              </div>
              <div className="w-full h-2 bg-[rgba(255,255,255,0.05)] rounded-full overflow-hidden mt-1">
                <motion.div
                  className="h-full rounded-full"
                  initial={{ width: 0 }}
                  animate={{ width: `${Math.min(pct, 100)}%` }}
                  transition={{ duration: 0.35, ease: 'easeOut' }}
                  style={{ background: 'linear-gradient(90deg, rgba(5,223,114,0.55), rgba(5,223,114,0.25))' }}
                />
              </div>
            </div>
            <span className="text-[11px] text-[rgba(245,247,251,0.6)] w-12 text-right shrink-0 tabular-nums font-medium pt-[1px]">
              {formatDuration(Number(f.totalSeconds))}
            </span>
          </motion.div>
        );
      })}
    </motion.div>
  );
}

