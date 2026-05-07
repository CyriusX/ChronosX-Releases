import { AlertCircle, CheckCircle, Download, Loader2, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '../ui/dialog';
import type { UpdateInfo, UpdateProgress } from '../../hooks/useUpdate';

export type UpdateModalStage =
  | 'available'
  | 'downloading'
  | 'installing'
  | 'complete'
  | 'failed'
  | 'updateInProgress';

interface UpdateNotificationModalProps {
  open: boolean;
  stage: UpdateModalStage;
  updateInfo: UpdateInfo | null;
  progress: UpdateProgress | null;
  error: string | null;
  onInstall: () => void;
  onDismiss: () => void;
  onRetry: () => void;
}

export function UpdateNotificationModal({
  open,
  stage,
  updateInfo,
  progress,
  error,
  onInstall,
  onDismiss,
  onRetry,
}: UpdateNotificationModalProps) {
  const { t } = useTranslation();

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  };

  const isBusy = stage === 'downloading' || stage === 'installing' || stage === 'updateInProgress';

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v && !isBusy) onDismiss(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-[#8B5CF6] to-[#22D3EE] flex items-center justify-center">
                {stage === 'complete' ? (
                  <CheckCircle className="w-4.5 h-4.5 text-white" />
                ) : stage === 'failed' ? (
                  <AlertCircle className="w-4.5 h-4.5 text-white" />
                ) : (
                  <Download className="w-4.5 h-4.5 text-white" />
                )}
              </div>
              <DialogTitle className="text-[16px]">
                {stage === 'complete'
                  ? t('updateModal.complete.title', 'Update installed!')
                  : stage === 'failed'
                    ? t('updateModal.failed.title', 'Update failed')
                    : t('updateModal.available.title', 'Update available')}
              </DialogTitle>
            </div>
            {!isBusy && (
              <button
                onClick={onDismiss}
                className="p-1.5 rounded-lg hover:bg-[rgba(255,255,255,0.06)] transition-colors"
              >
                <X className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
              </button>
            )}
          </div>
          <DialogDescription className="sr-only">
            {stage === 'complete'
              ? t('updateModal.complete.desc', 'The application has been updated.')
              : stage === 'failed'
                ? t('updateModal.failed.desc', 'The update could not be installed.')
                : t('updateModal.available.desc', 'A new version is available.')}
          </DialogDescription>
        </DialogHeader>

        {/* Available state */}
        {stage === 'available' && updateInfo && (
          <div className="space-y-4">
            <div className="space-y-2">
              <div className="flex items-center justify-between py-1.5">
                <span className="text-[12px] text-[rgba(245,247,251,0.5)]">
                  {t('updateModal.currentVersion', 'Current version')}
                </span>
                <span className="text-[12px] font-medium text-[rgba(245,247,251,0.7)]">
                  {updateInfo.currentVersion}
                </span>
              </div>
              <div className="flex items-center justify-between py-1.5">
                <span className="text-[12px] text-[rgba(245,247,251,0.5)]">
                  {t('updateModal.newVersion', 'New version')}
                </span>
                <span className="text-[12px] font-medium text-[#22D3EE]">
                  {updateInfo.latestVersion}
                </span>
              </div>
              <div className="flex items-center justify-between py-1.5">
                <span className="text-[12px] text-[rgba(245,247,251,0.5)]">
                  {t('updateModal.fileSize', 'Size')}
                </span>
                <span className="text-[12px] font-medium text-[rgba(245,247,251,0.8)]">
                  {formatBytes(updateInfo.fileSizeBytes)}
                </span>
              </div>
            </div>

            {updateInfo.releaseNotes && (
              <div className="bg-[rgba(255,255,255,0.03)] rounded-xl p-3 border border-[rgba(255,255,255,0.04)] max-h-[160px] overflow-y-auto">
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-1">
                  {t('updateModal.releaseNotes', 'Release notes')}
                </p>
                <p className="text-[12px] text-[rgba(245,247,251,0.7)] whitespace-pre-wrap">
                  {updateInfo.releaseNotes}
                </p>
              </div>
            )}

            <div className="flex gap-3">
              <button
                onClick={onDismiss}
                className="flex-1 py-2.5 px-4 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] font-medium text-[rgba(245,247,251,0.7)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
              >
                {t('updateModal.later', 'Later')}
              </button>
              <button
                onClick={onInstall}
                className="flex-1 py-2.5 px-4 rounded-xl bg-gradient-to-r from-[#8B5CF6] to-[#6D28D9] text-[13px] font-medium text-white hover:opacity-90 transition-opacity"
              >
                {t('updateModal.installNow', 'Update now')}
              </button>
            </div>
          </div>
        )}

        {/* Downloading / Installing */}
        {(stage === 'downloading' || stage === 'installing' || stage === 'updateInProgress') && (
          <div className="space-y-4">
            <div className="flex items-center gap-3">
              <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
              <span className="text-[13px] text-[rgba(245,247,251,0.7)]">
                {stage === 'downloading'
                  ? t('updateModal.downloading', 'Downloading update...')
                  : t('updateModal.installing', 'Installing update...')}
              </span>
            </div>

            <div className="w-full h-2 bg-[rgba(255,255,255,0.06)] rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full transition-all duration-300"
                style={{ width: `${Math.max(progress?.percentage ?? 2, 2)}%` }}
              />
            </div>

            <div className="flex justify-between">
              <span className="text-[11px] text-[rgba(245,247,251,0.4)]">
                {progress?.bytesDownloaded && progress?.bytesTotal
                  ? `${formatBytes(progress.bytesDownloaded)} / ${formatBytes(progress.bytesTotal)}`
                  : progress?.message ?? ''}
              </span>
              <span className="text-[11px] text-[rgba(245,247,251,0.5)]">
                {progress?.percentage ?? 0}%
              </span>
            </div>

            {stage === 'updateInProgress' && (
              <p className="text-[11px] text-[rgba(245,247,251,0.4)]">
                {t('updateModal.restartHint', 'The app will restart shortly. If it doesn\'t, you can safely close and reopen it.')}
              </p>
            )}
          </div>
        )}

        {/* Complete */}
        {stage === 'complete' && (
          <div className="space-y-4">
            <div className="flex items-center gap-3 py-3">
              <CheckCircle className="w-5 h-5 text-[#22c55e]" />
              <span className="text-[13px] text-[#22c55e]">
                {t('updateModal.complete.message', 'ChronosX has been updated successfully. The app will restart.')}
              </span>
            </div>
          </div>
        )}

        {/* Failed */}
        {stage === 'failed' && (
          <div className="space-y-4">
            <div className="flex items-center gap-3 py-2">
              <AlertCircle className="w-4 h-4 text-[#ff6b6b]" />
              <span className="text-[13px] text-[#ff6b6b]">
                {t('updateModal.failed.message', 'The update could not be installed.')}
              </span>
            </div>
            {error && (
              <p className="text-[12px] text-[rgba(245,247,251,0.5)] bg-[rgba(255,107,107,0.1)] rounded-lg p-3">
                {error}
              </p>
            )}
            <button
              onClick={onRetry}
              className="w-full py-2.5 px-4 rounded-xl bg-gradient-to-r from-[#8B5CF6] to-[#6D28D9] text-[13px] font-medium text-white hover:opacity-90 transition-opacity"
            >
              {t('updateModal.retry', 'Try again')}
            </button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
