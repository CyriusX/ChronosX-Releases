/**
 * useUpdate Hook - Manual update management via IPC
 *
 * Provides check, install, and progress tracking for app updates.
 * Auto-update is disabled — updates only trigger when the user clicks.
 *
 * Includes stale-progress detection: if no IPC event arrives for 3 minutes
 * while updating, the hook assumes the agent was killed by the installer
 * and transitions to an "update-in-progress" state instead of spinning forever.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { useIpc } from './useIpc';
import type {
  UpdateAvailablePayload,
  UpdateProgressPayload,
  UpdateCompletePayload,
  UpdateFailedPayload,
} from '../types/ipc';

export type UpdateStage = 'idle' | 'checking' | 'downloading' | 'installing' | 'complete' | 'failed' | 'updateInProgress';

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

/** How long (ms) without a progress event before we assume the agent was killed */
const STALE_TIMEOUT_MS = 3 * 60 * 1000; // 3 minutes

const STORAGE_KEY = 'chronosx-update-pending';

export function useUpdate() {
  const { sendCommand, subscribeToEvent } = useIpc();

  const [updateInfo, setUpdateInfo] = useState<UpdateInfo | null>(null);
  const [progress, setProgress] = useState<UpdateProgress>({
    stage: 'idle',
    percentage: 0,
    message: '',
  });
  const [error, setError] = useState<string | null>(null);
  const [dismissedVersion, setDismissedVersion] = useState<string | null>(null);

  const lastProgressRef = useRef<number>(Date.now());
  const staleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // --- localStorage persistence ---
  const markPending = useCallback((version: string) => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({ version, ts: Date.now() }));
    } catch { /* ignore */ }
  }, []);

  const clearPending = useCallback(() => {
    try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
  }, []);

  const getPending = useCallback((): { version: string; ts: number } | null => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      return JSON.parse(raw);
    } catch { return null; }
  }, []);

  // --- Stale-progress watchdog ---
  const resetStaleTimer = useCallback(() => {
    lastProgressRef.current = Date.now();
    if (staleTimerRef.current) clearTimeout(staleTimerRef.current);
    staleTimerRef.current = null;
  }, []);

  const startStaleTimer = useCallback(() => {
    resetStaleTimer();

    const tick = () => {
      const elapsed = Date.now() - lastProgressRef.current;
      if (elapsed >= STALE_TIMEOUT_MS) {
        // No progress for too long — agent was probably killed by the installer
        setProgress({
          stage: 'updateInProgress',
          percentage: 100,
          message: 'Update is being installed. The app will restart shortly.',
        });
        setError(null);
        return;
      }
      staleTimerRef.current = setTimeout(tick, 5000);
    };

    staleTimerRef.current = setTimeout(tick, 5000);
  }, [resetStaleTimer]);

  // Cleanup stale timer on unmount
  useEffect(() => {
    return () => {
      if (staleTimerRef.current) clearTimeout(staleTimerRef.current);
    };
  }, []);

  // --- Recovery: check pending update on mount ---
  useEffect(() => {
    const pending = getPending();
    if (!pending) return;

    // Only care if pending was set within the last 30 minutes
    const age = Date.now() - pending.ts;
    if (age > 30 * 60 * 1000) {
      clearPending();
      return;
    }

    // We had a pending update — agent likely restarted after install.
    // Set a temporary state while we wait for the agent to confirm.
    setProgress({
      stage: 'updateInProgress',
      percentage: 100,
      message: 'Verifying update...',
    });

    // The agent will emit updateComplete/updateFailed via IPC,
    // or we'll time out and reset to idle.
  }, [getPending, clearPending]);

  // Subscribe to update events
  useEffect(() => {
    const unsubAvailable = subscribeToEvent('updateAvailable', (payload: UpdateAvailablePayload) => {
      const newVersion = payload.latestVersion;
      setUpdateInfo(prev => {
        // Reset dismissed if the version changed
        if (prev?.latestVersion !== newVersion) {
          setDismissedVersion(null);
        }
        return {
          hasUpdate: payload.hasUpdate,
          currentVersion: payload.currentVersion,
          latestVersion: newVersion,
          fileSizeBytes: payload.fileSizeBytes,
          releaseNotes: payload.releaseNotes,
        };
      });
      setProgress({ stage: 'idle', percentage: 0, message: '' });
      setError(null);
    });

    const unsubProgress = subscribeToEvent('updateProgress', (payload: UpdateProgressPayload) => {
      const mappedStage = mapStage(payload.stage);
      setProgress({
        stage: mappedStage,
        percentage: payload.percentage,
        message: payload.message,
        bytesDownloaded: payload.bytesDownloaded,
        bytesTotal: payload.bytesTotal,
      });
      resetStaleTimer();

      // If the agent sends an "installing" or "startingServices" progress,
      // keep the stale timer running — the agent might be killed at any moment.
      if (mappedStage === 'installing') {
        // Timer already running, just reset the timestamp
      }
    });

    const unsubComplete = subscribeToEvent('updateComplete', (payload: UpdateCompletePayload) => {
      setProgress({ stage: 'complete', percentage: 100, message: `Updated to ${payload.version}` });
      setUpdateInfo(null);
      setError(null);
      clearPending();
      if (staleTimerRef.current) clearTimeout(staleTimerRef.current);
    });

    const unsubFailed = subscribeToEvent('updateFailed', (payload: UpdateFailedPayload) => {
      setProgress({ stage: 'failed', percentage: 0, message: payload.error });
      setError(payload.error);
      clearPending();
      if (staleTimerRef.current) clearTimeout(staleTimerRef.current);
    });

    return () => {
      unsubAvailable();
      unsubProgress();
      unsubComplete();
      unsubFailed();
    };
  }, [subscribeToEvent, resetStaleTimer, clearPending]);

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
    markPending(updateInfo.latestVersion);
    startStaleTimer();

    try {
      await sendCommand('startUpdate');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start update');
      setProgress({ stage: 'failed', percentage: 0, message: 'Update failed' });
      clearPending();
      if (staleTimerRef.current) clearTimeout(staleTimerRef.current);
    }
  }, [sendCommand, updateInfo, markPending, clearPending, startStaleTimer]);

  const dismissUpdate = useCallback(() => {
    if (updateInfo) setDismissedVersion(updateInfo.latestVersion);
  }, [updateInfo]);

  const shouldShowModal = !!(
    updateInfo?.hasUpdate
    && updateInfo.latestVersion !== dismissedVersion
    && (progress.stage === 'idle' || progress.stage === 'downloading' || progress.stage === 'installing' || progress.stage === 'updateInProgress' || progress.stage === 'complete' || progress.stage === 'failed')
  );

  return {
    updateInfo,
    progress,
    error,
    isChecking: progress.stage === 'checking',
    isUpdating: progress.stage === 'downloading' || progress.stage === 'installing' || progress.stage === 'updateInProgress',
    shouldShowModal,
    dismissUpdate,
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
