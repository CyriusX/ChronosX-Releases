import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { isDesktopRuntime } from '../../lib/runtime';
import { useIpc } from '../../hooks/useIpc';
import { getTopFolders } from '../../services/reportApi';
import { toLocalDateStr, type TopFolderItem } from '../../types/reports';
import { CollapsibleFoldersCard } from '../folders/CollapsibleFoldersCard';

export interface ActivitiesFoldersCardProps {
  date: Date;
  userId?: string;
  limit?: number;
}

export function ActivitiesFoldersCard({ date, userId, limit = 20 }: ActivitiesFoldersCardProps) {
  const { t } = useTranslation();
  const { sendQuery, isConnected } = useIpc();
  const desktopRuntime = isDesktopRuntime();

  const dateStr = toLocalDateStr(date);
  const isToday = dateStr === toLocalDateStr(new Date());
  const shouldUseIpc = desktopRuntime && isConnected && isToday && !userId;

  const loadFolders = useCallback(async (): Promise<TopFolderItem[]> => {
    if (shouldUseIpc) {
      const res = await sendQuery('getTopFolders', { date: dateStr, limit });
      if (res.success && res.data) {
        const d = res.data as { folders?: TopFolderItem[] };
        return d.folders ?? [];
      }
      return [];
    }

    const backend = await getTopFolders(dateStr, dateStr, limit, userId);
    return backend.folders ?? [];
  }, [shouldUseIpc, sendQuery, dateStr, limit, userId]);

  return (
    <CollapsibleFoldersCard
      title={t('reports.topFolders')}
      defaultCollapsed={true}
      maxItems={limit}
      loadFolders={loadFolders}
    />
  );
}

