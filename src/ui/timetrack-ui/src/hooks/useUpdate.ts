/**
 * useUpdate Hook - Manual update management via IPC
 *
 * Provides check, install, and progress tracking for app updates.
 * Auto-update is disabled — updates only trigger when the user clicks.
 */

import { useCallback, useEffect, useState } from 'react';
import { useIpc } from './useIpc';
import type {
  UpdateAvailablePayload,
  UpdateProgressPayload,
  UpdateCompletePayload,
  UpdateFailedPayload,
} from '../types/ipc';

export type UpdateStage = 'idle' | 'checking' | 'downloading' | 'installing' | 'complete' | 'failed';

export interface UpdateInfo {
  hasUpdate: boolean;
  currentVersion: string;
  latestVersion: string;
  fileSizeBytes: number;
  releaseNotes: string;
}

export interface UpdateProgress {
  stage: UpdateStage;
  percentage: number;
  message: string;
  bytesDownloaded?: number;
  bytesTotal?: number;
}

export function useUpdate() {
  const { sendCommand, subscribeToEvent } = useIpc();

  const [updateInfo, setUpdateInfo] = useState<UpdateInfo | null>(null);
  const [progress, setProgress] = useState<UpdateProgress>({
    stage: 'idle',
    percentage: 0,
    message: '',
  });
  const [error, setError] = useState<string | null>(null);

  // Subscribe to update events
  useEffect(() => {
    const unsubAvailable = subscribeToEvent('updateAvailable', (payload: UpdateAvailablePayload) => {
      setUpdateInfo({
        hasUpdate: payload.hasUpdate,
        currentVersion: payload.currentVersion,
        latestVersion: payload.latestVersion,
        fileSizeBytes: payload.fileSizeBytes,
        releaseNotes: payload.releaseNotes,
      });
      setProgress({ stage: 'idle', percentage: 0, message: '' });
      setError(null);
    });

    const unsubProgress = subscribeToEvent('updateProgress', (payload: UpdateProgressPayload) => {
      setProgress({
        stage: mapStage(payload.stage),
        percentage: payload.percentage,
        message: payload.message,
        bytesDownloaded: payload.bytesDownloaded,
        bytesTotal: payload.bytesTotal,
      });
    });

    const unsubComplete = subscribeToEvent('updateComplete', (payload: UpdateCompletePayload) => {
      setProgress({ stage: 'complete', percentage: 100, message: `Updated to ${payload.version}` });
      setUpdateInfo(null);
      setError(null);
    });

    const unsubFailed = subscribeToEvent('updateFailed', (payload: UpdateFailedPayload) => {
      setProgress({ stage: 'failed', percentage: 0, message: payload.error });
      setError(payload.error);
    });

    return () => {
      unsubAvailable();
      unsubProgress();
      unsubComplete();
      unsubFailed();
    };
  }, [subscribeToEvent]);

  const checkForUpdates = useCallback(async () => {
    setError(null);
    setProgress({ stage: 'checking', percentage: 0, message: 'Checking...' });
    setUpdateInfo(null);

    try {
      await sendCommand('checkForUpdates');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to check for updates');
      setProgress({ stage: 'failed', percentage: 0, message: 'Check failed' });
    }
  }, [sendCommand]);

  const startUpdate = useCallback(async () => {
    if (!updateInfo?.hasUpdate) return;

    setError(null);
    setProgress({ stage: 'downloading', percentage: 0, message: 'Starting update...' });

    try {
      await sendCommand('startUpdate');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start update');
      setProgress({ stage: 'failed', percentage: 0, message: 'Update failed' });
    }
  }, [sendCommand, updateInfo]);

  return {
    updateInfo,
    progress,
    error,
    isChecking: progress.stage === 'checking',
    isUpdating: progress.stage === 'downloading' || progress.stage === 'installing',
    checkForUpdates,
    startUpdate,
  };
}

function mapStage(stage: string): UpdateStage {
  switch (stage?.toLowerCase()) {
    case 'checking':
      return 'checking';
    case 'downloading':
      return 'downloading';
    case 'verifying':
    case 'backingup':
    case 'stoppingservices':
    case 'installing':
    case 'startingservices':
      return 'installing';
    case 'completed':
      return 'complete';
    case 'failed':
    case 'rollingback':
      return 'failed';
    default:
      return 'idle';
  }
}
