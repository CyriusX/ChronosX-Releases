import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Folder, Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { cardBase as sharedCardBase } from './shared/styles';
import { formatDuration } from '../../lib/utils';
import { toLocalDateStr } from '../../types/reports';
import { useIpc } from '../../hooks/useIpc';
import { isDesktopRuntime } from '../../lib/runtime';
import { getTopFolders } from '../../services/reportApi';
import type { TopFoldersResponse } from '../../types/ipc';

const POLL_INTERVAL_MS = 30_000;
const cardBase = sharedCardBase + ' overflow-hidden';

export function FoldersAccessedCard({
  date,
  limit = 5,
  userId,
}: {
  date?: Date;
  limit?: number;
  userId?: string;
} = {}) {
  const { t } = useTranslation();
  const { sendQuery, isConnected } = useIpc();
  const desktopRuntime = isDesktopRuntime();

  const [data, setData] = useState<TopFoldersResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const dateStr = useMemo(() => toLocalDateStr(date ?? new Date()), [date]);
  const isToday = useMemo(() => dateStr === toLocalDateStr(new Date()), [dateStr]);
  const shouldUseIpc = desktopRuntime && isConnected && isToday && !userId;

  const fetchFolders = useCallback(async () => {
    try {
      if (shouldUseIpc) {
        const res = await sendQuery('getTopFolders', { date: dateStr, limit });
        if (res.success) {
          setData(res.data ?? { folders: [] });
        }
        return;
      }

      const backend = await getTopFolders(dateStr, dateStr, limit, userId);
      setData(backend as unknown as TopFoldersResponse);
    } catch {
      setData({ folders: [] });
    } finally {
      setLoading(false);
    }
  }, [shouldUseIpc, sendQuery, dateStr, limit, userId]);

  useEffect(() => {
    fetchFolders();
    // Poll only when using IPC (today, desktop) for near real-time updates.
    if (shouldUseIpc) {
      pollRef.current = setInterval(fetchFolders, POLL_INTERVAL_MS);
    }
    return () => {
      if (pollRef.current) {
        clearInterval(pollRef.current);
        pollRef.current = null;
      }
    };
  }, [fetchFolders, shouldUseIpc]);

  const folders = useMemo(() => data?.folders ?? [], [data]);

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {t('dashboard.foldersAccessed')}
          </span>
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">{folders.length}</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2.5 pb-3 px-4">
        {loading ? (
          <div className="flex items-center justify-center py-3">
            <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
          </div>
        ) : folders.length === 0 ? (
          <div className="flex items-center gap-2 py-2 text-[11px] text-[rgba(245,247,251,0.4)]">
            <Folder className="w-4 h-4 text-[rgba(245,247,251,0.25)]" />
            <span>{t('dashboard.noFolders')}</span>
          </div>
        ) : (
          <div className="space-y-2">
            {folders.map((f) => (
              <div
                key={f.folderPath}
                className="flex items-start justify-between gap-3 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]"
                title={f.folderPath}
              >
                <div className="min-w-0">
                  <p className="text-[11px] text-[rgba(245,247,251,0.85)] truncate">
                    {f.folderPath}
                  </p>
                  <p className="text-[9px] text-[rgba(245,247,251,0.35)] mt-0.5">
                    {t('dashboard.visits', { count: f.visitCount })}
                  </p>
                </div>
                <span className="shrink-0 text-[11px] font-semibold text-[rgba(245,247,251,0.75)] tabular-nums">
                  {formatDuration(Number(f.totalSeconds))}
                </span>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
