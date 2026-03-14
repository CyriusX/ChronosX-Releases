/**
 * Format remaining time utility
 *
 * CX-139: Timer Focus Card
 */

import type { FocusModeType } from '../../../types/ipc';

/**
 * Formats remaining time in MM:SS or HH:MM:SS format
 * Based on focus mode type
 */
export function formatRemaining(
  remainingMs: number,
  mode: FocusModeType
): string {
  const totalSec = Math.max(0, Math.ceil(remainingMs / 1000));
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;

  // Ultradian (90 min) or any duration > 1 hour -> HH:MM:SS
  if (mode === 'Ultradian' || h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  // Pomodoro (25 min) -> MM:SS
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

/**
 * Formats duration in a human-readable format
 */
export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;

  if (h > 0 && m > 0) {
    return `${h}h ${m}m`;
  }
  if (h > 0) {
    return `${h}h`;
  }
  return `${m}min`;
}
