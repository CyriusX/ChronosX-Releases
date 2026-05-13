import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Camera, Globe, X, Plus, AlertTriangle } from 'lucide-react';
import { PolicyCardShell } from './PolicyCardShell';
import { getEvidencePolicy, updateEvidencePolicy } from '../../services/policyApi';
import { useAuthStore } from '../../stores/authStore';
import type { EvidencePolicyResponse } from '../../types/settings';

const RETENTION_OPTIONS = [7, 14, 30, 60, 90];

export function EvidencePolicyCard() {
  const { t } = useTranslation();
  const orgId = useAuthStore(s => s.user?.orgId);
  const role = useAuthStore(s => s.user?.role);
  const canEdit = role === 'Admin';

  const [policy, setPolicy] = useState<EvidencePolicyResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Edit state
  const [screenshotsEnabled, setScreenshotsEnabled] = useState(false);
  const [intervalMinutes, setIntervalMinutes] = useState(5);
  const [excludedApps, setExcludedApps] = useState<string[]>([]);
  const [retentionDays, setRetentionDays] = useState(30);
  const [websiteTracking, setWebsiteTracking] = useState(true);
  const [newApp, setNewApp] = useState('');

  const fetchPolicy = useCallback(async () => {
    if (!orgId) return;
    try {
      const result = await getEvidencePolicy(orgId);
      setPolicy(result);
      setScreenshotsEnabled(result.screenshotsEnabled);
      setIntervalMinutes(result.screenshotIntervalMinutes);
      setExcludedApps(result.screenshotExcludedApps);
      setRetentionDays(result.evidenceRetentionDays);
      setWebsiteTracking(result.websiteTrackingEnabled);
    } catch {
      setError('Failed to load evidence policy');
    } finally {
      setLoading(false);
    }
  }, [orgId]);

  useEffect(() => { fetchPolicy(); }, [fetchPolicy]);

  const handleSave = async () => {
    if (!orgId) return;
    setSaving(true);
    setError(null);
    try {
      const result = await updateEvidencePolicy(orgId, {
        screenshotsEnabled,
        screenshotIntervalMinutes: intervalMinutes,
        screenshotExcludedApps: excludedApps,
        evidenceRetentionDays: retentionDays,
        websiteTrackingEnabled: websiteTracking,
      });
      setPolicy(result);
      setIsEditing(false);
    } catch {
      setError('Failed to save evidence policy');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (policy) {
      setScreenshotsEnabled(policy.screenshotsEnabled);
      setIntervalMinutes(policy.screenshotIntervalMinutes);
      setExcludedApps(policy.screenshotExcludedApps);
      setRetentionDays(policy.evidenceRetentionDays);
      setWebsiteTracking(policy.websiteTrackingEnabled);
    }
    setIsEditing(false);
    setError(null);
  };

  const addExcludedApp = () => {
    const trimmed = newApp.trim().toLowerCase();
    if (trimmed && !excludedApps.includes(trimmed)) {
      setExcludedApps([...excludedApps, trimmed]);
      setNewApp('');
    }
  };

  const removeExcludedApp = (app: string) => {
    setExcludedApps(excludedApps.filter(a => a !== app));
  };

  if (loading) {
    return (
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-5 animate-pulse">
        <div className="h-6 w-48 bg-[rgba(255,255,255,0.05)] rounded mb-4" />
        <div className="h-4 w-full bg-[rgba(255,255,255,0.03)] rounded mb-2" />
        <div className="h-4 w-3/4 bg-[rgba(255,255,255,0.03)] rounded" />
      </div>
    );
  }

  return (
    <PolicyCardShell
      title={t('evidence.policyTitle')}
      icon={Camera}
      iconColor="text-[#a78bfa]"
      iconBgColor="bg-[rgba(139,92,246,0.15)]"
      canEdit={canEdit}
      isEditing={isEditing}
      onEdit={() => setIsEditing(true)}
      onSave={handleSave}
      onCancel={handleCancel}
      isSaving={saving}
    >
      {error && (
        <div className="text-[11px] text-[#f87171] mb-3">{error}</div>
      )}

      <div className="space-y-5">
        {/* Screenshots toggle */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Camera className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
            <span className="text-[12px] text-[rgba(245,247,251,0.7)]">{t('evidence.screenshotsEnabled')}</span>
          </div>
          {isEditing ? (
            <button
              onClick={() => setScreenshotsEnabled(!screenshotsEnabled)}
              className={`relative w-10 h-5 rounded-full transition-colors ${screenshotsEnabled ? 'bg-[#8B5CF6]' : 'bg-[rgba(255,255,255,0.1)]'}`}
            >
              <div className={`absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform ${screenshotsEnabled ? 'translate-x-5' : 'translate-x-0.5'}`} />
            </button>
          ) : (
            <span className={`text-[11px] font-medium ${screenshotsEnabled ? 'text-[#4ade80]' : 'text-[rgba(245,247,251,0.35)]'}`}>
              {screenshotsEnabled ? t('evidence.enabled') : t('evidence.disabled')}
            </span>
          )}
        </div>

        {screenshotsEnabled && (
          <>
            {/* Interval slider */}
            <div>
              <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-2 block">{t('evidence.interval')}</label>
              {isEditing ? (
                <div className="space-y-1">
                  <input
                    type="range"
                    min={1}
                    max={60}
                    value={intervalMinutes}
                    onChange={e => setIntervalMinutes(Number(e.target.value))}
                    className="w-full h-1 bg-[rgba(255,255,255,0.1)] rounded-lg appearance-none cursor-pointer accent-[#8B5CF6]"
                  />
                  <div className="flex justify-between text-[9px] text-[rgba(245,247,251,0.25)]">
                    <span>1 min</span>
                    <span className="text-[#a78bfa] font-medium">{intervalMinutes} min</span>
                    <span>60 min</span>
                  </div>
                </div>
              ) : (
                <span className="text-[12px] text-[rgba(245,247,251,0.7)]">{intervalMinutes} {t('evidence.minutes')}</span>
              )}
            </div>

            {/* Excluded apps */}
            <div>
              <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-2 block">{t('evidence.excludedApps')}</label>
              <div className="flex flex-wrap gap-1.5">
                {excludedApps.map(app => (
                  <span key={app} className="flex items-center gap-1 px-2 py-0.5 rounded-md bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[10px] text-[rgba(245,247,251,0.6)]">
                    {app}
                    {isEditing && (
                      <button onClick={() => removeExcludedApp(app)} className="hover:text-[#f87171] transition-colors">
                        <X className="w-2.5 h-2.5" />
                      </button>
                    )}
                  </span>
                ))}
                {isEditing && (
                  <div className="flex items-center gap-1">
                    <input
                      value={newApp}
                      onChange={e => setNewApp(e.target.value)}
                      onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addExcludedApp(); } }}
                      placeholder="app.exe"
                      className="w-24 px-2 py-0.5 rounded-md bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[10px] text-[rgba(245,247,251,0.7)] placeholder:text-[rgba(245,247,251,0.2)] outline-none focus:border-[rgba(139,92,246,0.3)]"
                    />
                    <button onClick={addExcludedApp} className="p-0.5 rounded hover:bg-[rgba(255,255,255,0.06)] transition-colors">
                      <Plus className="w-3 h-3 text-[#a78bfa]" />
                    </button>
                  </div>
                )}
              </div>
            </div>
          </>
        )}

        {/* Retention */}
        <div>
          <label className="text-[11px] text-[rgba(245,247,251,0.5)] mb-2 block">{t('evidence.retention')}</label>
          {isEditing ? (
            <div className="space-y-1">
              <select
                value={retentionDays}
                onChange={e => setRetentionDays(Number(e.target.value))}
                className="px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[11px] text-[rgba(245,247,251,0.7)] outline-none focus:border-[rgba(139,92,246,0.3)]"
              >
                {RETENTION_OPTIONS.map(d => (
                  <option key={d} value={d}>{d} {t('evidence.days')}</option>
                ))}
              </select>
              {retentionDays < 30 && (
                <div className="flex items-center gap-1">
                  <AlertTriangle className="w-3 h-3 text-[#fbbf24]" />
                  <span className="text-[9px] text-[#fbbf24]">{t('evidence.retentionWarning')}</span>
                </div>
              )}
            </div>
          ) : (
            <span className="text-[12px] text-[rgba(245,247,251,0.7)]">{retentionDays} {t('evidence.days')}</span>
          )}
        </div>

        {/* Website tracking toggle */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Globe className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
            <span className="text-[12px] text-[rgba(245,247,251,0.7)]">{t('evidence.websiteTracking')}</span>
          </div>
          {isEditing ? (
            <button
              onClick={() => setWebsiteTracking(!websiteTracking)}
              className={`relative w-10 h-5 rounded-full transition-colors ${websiteTracking ? 'bg-[#8B5CF6]' : 'bg-[rgba(255,255,255,0.1)]'}`}
            >
              <div className={`absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform ${websiteTracking ? 'translate-x-5' : 'translate-x-0.5'}`} />
            </button>
          ) : (
            <span className={`text-[11px] font-medium ${websiteTracking ? 'text-[#4ade80]' : 'text-[rgba(245,247,251,0.35)]'}`}>
              {websiteTracking ? t('evidence.enabled') : t('evidence.disabled')}
            </span>
          )}
        </div>
      </div>
    </PolicyCardShell>
  );
}
