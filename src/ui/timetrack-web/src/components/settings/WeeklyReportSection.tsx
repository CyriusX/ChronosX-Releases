import { useState, useEffect, useCallback, useRef } from 'react';
import { Mail, Clock, Calendar, Users, AlertTriangle, TrendingUp, Eye, Loader2, Check, ChevronDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import {
  getWeeklyReportSchedule,
  upsertWeeklyReportSchedule,
  deleteWeeklyReportSchedule,
  previewWeeklyReport,
  type WeeklyReportSchedule,
  type ReportPreferences,
} from '../../services/weeklyReportApi';
import { useNotifications } from '@desktop/stores/uiStore';

const DAYS_OF_WEEK = [
  { value: 0, label: 'Domingo' },
  { value: 1, label: 'Segunda-feira' },
  { value: 2, label: 'Terca-feira' },
  { value: 3, label: 'Quarta-feira' },
  { value: 4, label: 'Quinta-feira' },
  { value: 5, label: 'Sexta-feira' },
  { value: 6, label: 'Sabado' },
];

function generateTimeOptions(): string[] {
  const options: string[] = [];
  for (let h = 6; h <= 22; h++) {
    options.push(`${String(h).padStart(2, '0')}:00`);
    options.push(`${String(h).padStart(2, '0')}:30`);
  }
  return options;
}

const TIME_OPTIONS = generateTimeOptions();

export function WeeklyReportSection() {
  const { t } = useTranslation();
  const { notify } = useNotifications();

  const [schedule, setSchedule] = useState<WeeklyReportSchedule | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);

  const [isEnabled, setIsEnabled] = useState(false);
  const [dayOfWeek, setDayOfWeek] = useState(1);
  const [timeOfDay, setTimeOfDay] = useState('09:00');
  const [preferences, setPreferences] = useState<ReportPreferences>({
    includeTeamComparison: true,
    includeDifficultyAnalysis: true,
    includeWeekOverWeek: true,
    includeUnproductiveDays: true,
  });

  // Use ref to avoid re-creating the function on every render
  const hasLoadedRef = useRef(false);

  const loadSchedule = useCallback(async () => {
    if (hasLoadedRef.current) return;
    hasLoadedRef.current = true;

    setIsLoading(true);
    console.log('[WeeklyReportSection] Loading schedule...');
    try {
      const result = await getWeeklyReportSchedule();
      console.log('[WeeklyReportSection] Schedule loaded:', result);
      setSchedule(result);
      if (result) {
        setIsEnabled(result.isEnabled);
        setDayOfWeek(result.dayOfWeek);
        setTimeOfDay(result.timeOfDay);
        setPreferences(result.preferences);
      }
    } catch (error) {
      console.error('[WeeklyReportSection] Failed to load schedule:', error);
      notify.error(t('settings.weeklyReport.loadFailed'));
    } finally {
      console.log('[WeeklyReportSection] Loading finished');
      setIsLoading(false);
    }
  }, [t, notify]);

  useEffect(() => {
    loadSchedule();
  }, [loadSchedule]);

  const handleSave = async () => {
    setIsSaving(true);
    try {
      const result = await upsertWeeklyReportSchedule({
        dayOfWeek,
        timeOfDay,
        isEnabled,
        preferences,
      });
      setSchedule(result);
      notify.success(t('settings.weeklyReport.saveSuccess'));
    } catch {
      notify.error(t('settings.weeklyReport.saveFailed'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleDisable = async () => {
    if (!schedule) return;
    setIsSaving(true);
    try {
      await deleteWeeklyReportSchedule();
      setSchedule(null);
      setIsEnabled(false);
      notify.success(t('settings.weeklyReport.disabledSuccess'));
    } catch {
      notify.error(t('settings.weeklyReport.saveFailed'));
    } finally {
      setIsSaving(false);
    }
  };

  const handlePreview = async () => {
    setIsPreviewLoading(true);
    setPreview(null);
    try {
      const result = await previewWeeklyReport();
      setPreview(result.htmlReport);
    } catch {
      notify.error(t('settings.weeklyReport.previewFailed'));
    } finally {
      setIsPreviewLoading(false);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6 max-w-2xl">
        <div>
          <h2 className="text-[20px] font-semibold text-[#f5f7fb]">
            {t('settings.weeklyReport.title')}
          </h2>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
            {t('settings.weeklyReport.subtitle')}
          </p>
        </div>
        <div className="text-[rgba(245,247,251,0.5)] text-[13px]">Loading...</div>
        <div className="animate-pulse space-y-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-20 bg-[rgba(255,255,255,0.04)] rounded-2xl" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-2xl">
      {/* Header */}
      <div>
        <h2 className="text-[20px] font-semibold text-[#f5f7fb]">
          {t('settings.weeklyReport.title')}
        </h2>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
          {t('settings.weeklyReport.subtitle')}
        </p>
      </div>

      {/* Enable/Disable Toggle */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.08)] rounded-2xl p-5">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-[#8b5cf6] to-[#6366f1] flex items-center justify-center">
              <Mail className="w-5 h-5 text-white" />
            </div>
            <div>
              <p className="text-[14px] font-medium text-[#f5f7fb]">
                {t('settings.weeklyReport.enableTitle')}
              </p>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                {t('settings.weeklyReport.enableDescription')}
              </p>
            </div>
          </div>
          <button
            onClick={() => setIsEnabled(!isEnabled)}
            className={`relative w-12 h-6 rounded-full transition-colors ${
              isEnabled
                ? 'bg-[#8b5cf6]'
                : 'bg-[rgba(255,255,255,0.1)]'
            }`}
          >
            <motion.div
              animate={{ x: isEnabled ? 24 : 2 }}
              transition={{ type: 'spring', stiffness: 500, damping: 30 }}
              className="absolute top-1 w-4 h-4 rounded-full bg-white"
            />
          </button>
        </div>
      </div>

      {isEnabled && (
        <motion.div
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          className="space-y-4"
        >
          {/* Day and Time Selection */}
          <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.08)] rounded-2xl p-5">
            <div className="flex items-center gap-2 mb-4">
              <Clock className="w-4 h-4 text-[#8b5cf6]" />
              <p className="text-[14px] font-medium text-[#f5f7fb]">
                {t('settings.weeklyReport.scheduleTitle')}
              </p>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {/* Day of Week */}
              <div>
                <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-1.5 block">
                  <Calendar className="w-3 h-3 inline mr-1" />
                  {t('settings.weeklyReport.dayLabel')}
                </label>
                <div className="relative">
                  <select
                    value={dayOfWeek}
                    onChange={(e) => setDayOfWeek(Number(e.target.value))}
                    className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-xl px-3 py-2.5 text-[13px] text-[#f5f7fb] appearance-none cursor-pointer hover:border-[rgba(139,92,246,0.3)] transition-colors"
                  >
                    {DAYS_OF_WEEK.map((d) => (
                      <option key={d.value} value={d.value}>
                        {d.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[rgba(245,247,251,0.4)] pointer-events-none" />
                </div>
              </div>

              {/* Time of Day */}
              <div>
                <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-1.5 block">
                  <Clock className="w-3 h-3 inline mr-1" />
                  {t('settings.weeklyReport.timeLabel')}
                </label>
                <div className="relative">
                  <select
                    value={timeOfDay}
                    onChange={(e) => setTimeOfDay(e.target.value)}
                    className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-xl px-3 py-2.5 text-[13px] text-[#f5f7fb] appearance-none cursor-pointer hover:border-[rgba(139,92,246,0.3)] transition-colors"
                  >
                    {TIME_OPTIONS.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[rgba(245,247,251,0.4)] pointer-events-none" />
                </div>
              </div>
            </div>
          </div>

          {/* Report Preferences */}
          <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.08)] rounded-2xl p-5">
            <div className="flex items-center gap-2 mb-4">
              <Users className="w-4 h-4 text-[#8b5cf6]" />
              <p className="text-[14px] font-medium text-[#f5f7fb]">
                {t('settings.weeklyReport.preferencesTitle')}
              </p>
            </div>

            <div className="space-y-3">
              <PreferenceToggle
                icon={<Users className="w-3.5 h-3.5" />}
                label={t('settings.weeklyReport.prefTeamComparison')}
                checked={preferences.includeTeamComparison}
                onChange={(v) => setPreferences((p) => ({ ...p, includeTeamComparison: v }))}
              />
              <PreferenceToggle
                icon={<AlertTriangle className="w-3.5 h-3.5" />}
                label={t('settings.weeklyReport.prefDifficulties')}
                checked={preferences.includeDifficultyAnalysis}
                onChange={(v) => setPreferences((p) => ({ ...p, includeDifficultyAnalysis: v }))}
              />
              <PreferenceToggle
                icon={<TrendingUp className="w-3.5 h-3.5" />}
                label={t('settings.weeklyReport.prefWeekOverWeek')}
                checked={preferences.includeWeekOverWeek}
                onChange={(v) => setPreferences((p) => ({ ...p, includeWeekOverWeek: v }))}
              />
              <PreferenceToggle
                icon={<Calendar className="w-3.5 h-3.5" />}
                label={t('settings.weeklyReport.prefUnproductiveDays')}
                checked={preferences.includeUnproductiveDays}
                onChange={(v) => setPreferences((p) => ({ ...p, includeUnproductiveDays: v }))}
              />
            </div>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-3">
            <button
              onClick={handleSave}
              disabled={isSaving}
              className="flex items-center gap-2 px-5 py-2.5 bg-gradient-to-r from-[#8b5cf6] to-[#6366f1] text-white text-[13px] font-medium rounded-xl hover:opacity-90 transition-opacity disabled:opacity-50"
            >
              {isSaving ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Check className="w-4 h-4" />
              )}
              {t('settings.weeklyReport.saveButton')}
            </button>

            <button
              onClick={handlePreview}
              disabled={isPreviewLoading}
              className="flex items-center gap-2 px-5 py-2.5 bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[rgba(245,247,251,0.7)] text-[13px] font-medium rounded-xl hover:bg-[rgba(255,255,255,0.08)] transition-colors disabled:opacity-50"
            >
              {isPreviewLoading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Eye className="w-4 h-4" />
              )}
              {t('settings.weeklyReport.previewButton')}
            </button>

            {schedule && (
              <button
                onClick={handleDisable}
                disabled={isSaving}
                className="ml-auto text-[12px] text-[rgba(245,247,251,0.4)] hover:text-[#ff6b6b] transition-colors"
              >
                {t('settings.weeklyReport.disableButton')}
              </button>
            )}
          </div>

          {/* Preview */}
          {preview && (
            <motion.div
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(139,92,246,0.2)] rounded-2xl p-5"
            >
              <div className="flex items-center gap-2 mb-3">
                <Mail className="w-4 h-4 text-[#8b5cf6]" />
                <p className="text-[14px] font-medium text-[#f5f7fb]">
                  {t('settings.weeklyReport.previewTitle')}
                </p>
              </div>
              <div className="prose prose-invert prose-sm max-w-none text-[rgba(245,247,251,0.7)] text-[13px] leading-relaxed whitespace-pre-wrap">
                {preview.split('\n').map((line, i) => {
                  if (line.startsWith('## ')) {
                    return (
                      <h3 key={i} className="text-[15px] font-semibold text-[#f5f7fb] mt-4 mb-2">
                        {line.slice(3)}
                      </h3>
                    );
                  }
                  if (line.trim()) {
                    return <p key={i} className="my-1">{line}</p>;
                  }
                  return null;
                })}
              </div>
            </motion.div>
          )}
        </motion.div>
      )}
    </div>
  );
}

function PreferenceToggle({
  icon,
  label,
  checked,
  onChange,
}: {
  icon: React.ReactNode;
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <label className="flex items-center justify-between py-2 px-3 rounded-xl hover:bg-[rgba(255,255,255,0.02)] transition-colors cursor-pointer">
      <div className="flex items-center gap-2.5">
        <span className="text-[rgba(245,247,251,0.4)]">{icon}</span>
        <span className="text-[13px] text-[rgba(245,247,251,0.7)]">{label}</span>
      </div>
      <button
        onClick={() => onChange(!checked)}
        className={`relative w-9 h-5 rounded-full transition-colors ${
          checked ? 'bg-[#8b5cf6]' : 'bg-[rgba(255,255,255,0.1)]'
        }`}
      >
        <motion.div
          animate={{ x: checked ? 16 : 2 }}
          transition={{ type: 'spring', stiffness: 500, damping: 30 }}
          className="absolute top-0.5 w-4 h-4 rounded-full bg-white"
        />
      </button>
    </label>
  );
}
