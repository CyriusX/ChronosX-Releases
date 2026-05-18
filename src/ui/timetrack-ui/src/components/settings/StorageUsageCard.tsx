import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { HardDrive, AlertTriangle } from 'lucide-react';
import { getStorageUsage } from '../../services/storageApi';
import { useAuthStore } from '../../stores/authStore';
import type { StorageUsageResponse } from '../../services/storageApi';

function getBarColor(percentage: number): string {
  if (percentage >= 80) return 'bg-[#f87171]';
  if (percentage >= 60) return 'bg-[#fbbf24]';
  return 'bg-[#4ade80]';
}

function getBarBgColor(percentage: number): string {
  if (percentage >= 80) return 'bg-[rgba(248,113,113,0.1)]';
  if (percentage >= 60) return 'bg-[rgba(251,191,36,0.1)]';
  return 'bg-[rgba(74,222,128,0.1)]';
}

export function StorageUsageCard() {
  const { t } = useTranslation();
  const orgId = useAuthStore(s => s.user?.orgId);
  const [usage, setUsage] = useState<StorageUsageResponse | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchUsage = useCallback(async () => {
    if (!orgId) return;
    try {
      const result = await getStorageUsage(orgId);
      setUsage(result);
    } catch { /* ignore */ }
    finally { setLoading(false); }
  }, [orgId]);

  useEffect(() => { fetchUsage(); }, [fetchUsage]);

  if (loading) {
    return (
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5 animate-pulse">
        <div className="h-6 w-32 bg-[rgba(255,255,255,0.05)] rounded mb-4" />
        <div className="h-3 w-full bg-[rgba(255,255,255,0.03)] rounded" />
      </div>
    );
  }

  if (!usage) return null;

  const pct = Math.min(100, usage.percentage);

  return (
    <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-10 h-10 rounded-xl bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <HardDrive className="w-5 h-5 text-[#a78bfa]" />
        </div>
        <h3 className="text-[14px] font-medium text-[#f5f7fb]">{t('evidence.storageTitle')}</h3>
      </div>

      {/* Usage text */}
      <div className="mb-3">
        <span className="text-[12px] text-[rgba(245,247,251,0.7)]">
          {usage.usedGb} GB {t('evidence.storageOf')} {usage.quotaGb} GB ({usage.percentage}%)
        </span>
      </div>

      {/* Progress bar */}
      <div className={`h-2 rounded-full ${getBarBgColor(pct)} mb-3`}>
        <div
          className={`h-full rounded-full transition-all duration-500 ${getBarColor(pct)}`}
          style={{ width: `${Math.max(0.5, pct)}%` }}
        />
      </div>

      {/* Warning at 80%+ */}
      {pct >= 80 && (
        <div className="flex items-start gap-2 p-3 rounded-lg bg-[rgba(251,191,36,0.06)] border border-[rgba(251,191,36,0.12)]">
          <AlertTriangle className="w-3.5 h-3.5 text-[#fbbf24] flex-shrink-0 mt-0.5" />
          <div>
            <p className="text-[10px] text-[#fbbf24] leading-relaxed">
              {t('evidence.storageWarning')}
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
