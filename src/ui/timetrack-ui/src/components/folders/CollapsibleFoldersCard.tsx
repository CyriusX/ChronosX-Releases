import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import { ChevronDown, ChevronUp, Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import type { TopFolderItem } from '../../types/reports';
import { FoldersList } from './FoldersList';

export interface CollapsibleFoldersCardProps {
  title: string;
  folders?: TopFolderItem[];
  isLoading?: boolean;
  defaultCollapsed?: boolean;
  maxItems?: number;
  loadFolders?: () => Promise<TopFolderItem[]>;
}

const cardBase =
  'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

export function CollapsibleFoldersCard({
  title,
  folders,
  isLoading = false,
  defaultCollapsed = true,
  maxItems = 20,
  loadFolders,
}: CollapsibleFoldersCardProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(!defaultCollapsed);
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [internalLoading, setInternalLoading] = useState(false);
  const [internalFolders, setInternalFolders] = useState<TopFolderItem[]>([]);

  const effectiveFolders = useMemo(() => (loadFolders ? internalFolders : (folders ?? [])), [folders, internalFolders, loadFolders]);
  const effectiveLoading = loadFolders ? internalLoading : isLoading;

  useEffect(() => {
    if (loadFolders) return;
    setInternalFolders(folders ?? []);
  }, [folders, loadFolders]);

  const count = effectiveFolders.length;

  const onToggle = async () => {
    const next = !open;
    setOpen(next);

    if (!next) return;
    if (!loadFolders) return;
    if (loadedOnce) return;

    setInternalLoading(true);
    try {
      const fetched = await loadFolders();
      setInternalFolders(fetched);
      setLoadedOnce(true);
    } finally {
      setInternalLoading(false);
    }
  };

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-2 pt-3 px-4">
        <button
          type="button"
          onClick={onToggle}
          className="w-full flex items-center justify-between gap-3 text-left"
        >
          <CardTitle className="flex items-center justify-between w-full">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
            <span className="flex items-center gap-2">
              <span className="text-[10px] text-[rgba(245,247,251,0.4)]">{count}</span>
              {open ? (
                <ChevronUp className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              ) : (
                <ChevronDown className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              )}
            </span>
          </CardTitle>
        </button>
      </CardHeader>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <CardContent className="pt-2 pb-3 px-4">
              {effectiveLoading ? (
                <div className="flex items-center justify-center h-[160px]">
                  <Loader2 className="w-5 h-5 text-[#8B5CF6] animate-spin" />
                </div>
              ) : effectiveFolders.length === 0 ? (
                <div className="flex items-center justify-center h-[120px] text-[rgba(245,247,251,0.4)] text-[12px]">
                  {t('reports.noDataToShow')}
                </div>
              ) : (
                <FoldersList folders={effectiveFolders} maxItems={maxItems} />
              )}
            </CardContent>
          </motion.div>
        )}
      </AnimatePresence>
    </Card>
  );
}
