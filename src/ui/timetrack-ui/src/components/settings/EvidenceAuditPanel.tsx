import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Eye, Download, Trash2, FileDown, Search, ChevronLeft, ChevronRight } from 'lucide-react';
import { getEvidenceAccessLogs, type EvidenceAccessLogItem } from '../../services/auditApi';
import { useAuthStore } from '../../stores/authStore';

type ActionFilter = '' | 'evidence.view' | 'evidence.download' | 'evidence.delete';
type PeriodFilter = 'today' | '7days' | '30days';

const PAGE_SIZE = 20;

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

function ActionBadge({ action }: { action: string }) {
  const { t } = useTranslation();
  const config: Record<string, { icon: typeof Eye; color: string; label: string }> = {
    'evidence.view': { icon: Eye, color: 'text-[#38bdf8] bg-[rgba(56,189,248,0.1)]', label: t('evidence.auditView') },
    'evidence.download': { icon: Download, color: 'text-[#4ade80] bg-[rgba(74,222,128,0.1)]', label: t('evidence.auditDownload') },
    'evidence.delete': { icon: Trash2, color: 'text-[#f87171] bg-[rgba(248,113,113,0.1)]', label: t('evidence.auditDelete') },
  };
  const c = config[action] ?? { icon: Eye, color: 'text-[rgba(245,247,251,0.4)] bg-[rgba(255,255,255,0.04)]', label: action };
  const Icon = c.icon;

  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-medium ${c.color}`}>
      <Icon className="w-3 h-3" />
      {c.label}
    </span>
  );
}

export function EvidenceAuditPanel() {
  const { t } = useTranslation();
  const orgId = useAuthStore(s => s.user?.orgId);

  const [items, setItems] = useState<EvidenceAccessLogItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [actionFilter, setActionFilter] = useState<ActionFilter>('');
  const [periodFilter, setPeriodFilter] = useState<PeriodFilter>('7days');
  const [searchTerm, setSearchTerm] = useState('');

  const getDateRange = useCallback(() => {
    const now = new Date();
    let startDate: Date;
    switch (periodFilter) {
      case 'today':
        startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        break;
      case '7days':
        startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
        break;
      case '30days':
        startDate = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
        break;
    }
    return { startDate: startDate.toISOString(), endDate: now.toISOString() };
  }, [periodFilter]);

  const fetchData = useCallback(async () => {
    if (!orgId) return;
    setLoading(true);
    try {
      const { startDate, endDate } = getDateRange();
      const result = await getEvidenceAccessLogs(orgId, {
        page,
        pageSize: PAGE_SIZE,
        startDate,
        endDate,
        action: actionFilter || undefined,
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
      setTotalPages(result.totalPages);
    } catch {
      setItems([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [orgId, page, actionFilter, periodFilter, getDateRange]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleExportCsv = () => {
    if (items.length === 0) return;
    const header = 'Date,Actor,Target,Action,Evidence ID,IP\n';
    const rows = items.map(i =>
      `"${formatDate(i.accessedAt)}","${i.actorName ?? i.actorEmail ?? ''}","${i.targetUserId ?? ''}","${i.action}","${i.evidenceId ?? ''}","${i.ipAddress ?? ''}"`
    ).join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `auditoria-evidencias-${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const filteredItems = searchTerm
    ? items.filter(i =>
        (i.actorName?.toLowerCase().includes(searchTerm.toLowerCase()) ?? false) ||
        (i.actorEmail?.toLowerCase().includes(searchTerm.toLowerCase()) ?? false) ||
        (i.targetUserId?.includes(searchTerm) ?? false))
    : items;

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex items-center gap-3 flex-wrap">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-[rgba(245,247,251,0.25)]" />
          <input
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            placeholder={t('evidence.auditSearchPlaceholder')}
            className="w-full pl-8 pr-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[11px] text-[rgba(245,247,251,0.7)] placeholder:text-[rgba(245,247,251,0.2)] outline-none focus:border-[rgba(139,92,246,0.3)]"
          />
        </div>

        <select
          value={periodFilter}
          onChange={e => { setPeriodFilter(e.target.value as PeriodFilter); setPage(1); }}
          className="px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[11px] text-[rgba(245,247,251,0.7)] outline-none"
        >
          <option value="today">{t('evidence.auditToday')}</option>
          <option value="7days">{t('evidence.audit7Days')}</option>
          <option value="30days">{t('evidence.audit30Days')}</option>
        </select>

        <select
          value={actionFilter}
          onChange={e => { setActionFilter(e.target.value as ActionFilter); setPage(1); }}
          className="px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[11px] text-[rgba(245,247,251,0.7)] outline-none"
        >
          <option value="">{t('evidence.auditAllActions')}</option>
          <option value="evidence.view">{t('evidence.auditView')}</option>
          <option value="evidence.download">{t('evidence.auditDownload')}</option>
          <option value="evidence.delete">{t('evidence.auditDelete')}</option>
        </select>

        <button
          onClick={handleExportCsv}
          disabled={items.length === 0}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.7)] disabled:opacity-30 transition-colors"
        >
          <FileDown className="w-3.5 h-3.5" />
          {t('evidence.auditExport')}
        </button>
      </div>

      {/* Table */}
      <div className="rounded-xl bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] overflow-hidden">
        <table className="w-full">
          <thead>
            <tr className="border-b border-[rgba(255,255,255,0.04)]">
              <th className="text-left px-4 py-2 text-[9px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.3)]">{t('evidence.auditWho')}</th>
              <th className="text-left px-4 py-2 text-[9px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.3)]">{t('evidence.auditTarget')}</th>
              <th className="text-left px-4 py-2 text-[9px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.3)]">{t('evidence.auditWhen')}</th>
              <th className="text-left px-4 py-2 text-[9px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.3)]">{t('evidence.auditAction')}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={4} className="text-center py-8">
                  <div className="text-[11px] text-[rgba(245,247,251,0.35)] animate-pulse">{t('evidence.loading')}</div>
                </td>
              </tr>
            ) : filteredItems.length === 0 ? (
              <tr>
                <td colSpan={4} className="text-center py-8">
                  <div className="text-[11px] text-[rgba(245,247,251,0.3)]">{t('evidence.auditEmpty')}</div>
                </td>
              </tr>
            ) : (
              filteredItems.map(item => (
                <tr key={item.id} className="border-b border-[rgba(255,255,255,0.02)] hover:bg-[rgba(255,255,255,0.02)] transition-colors">
                  <td className="px-4 py-2.5">
                    <div className="text-[11px] text-[rgba(245,247,251,0.7)]">{item.actorName ?? 'Unknown'}</div>
                    <div className="text-[9px] text-[rgba(245,247,251,0.3)]">{item.actorEmail}</div>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{item.targetUserId ? item.targetUserId.slice(0, 8) + '...' : '-'}</span>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{formatDate(item.accessedAt)}</span>
                  </td>
                  <td className="px-4 py-2.5">
                    <ActionBadge action={item.action} />
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <span className="text-[10px] text-[rgba(245,247,251,0.3)]">{totalCount} {t('evidence.auditRecords')}</span>
          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="p-1 rounded hover:bg-[rgba(255,255,255,0.04)] disabled:opacity-30 transition-colors"
            >
              <ChevronLeft className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
            </button>
            <span className="text-[10px] text-[rgba(245,247,251,0.4)] px-2">{page} / {totalPages}</span>
            <button
              onClick={() => setPage(p => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="p-1 rounded hover:bg-[rgba(255,255,255,0.04)] disabled:opacity-30 transition-colors"
            >
              <ChevronRight className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
