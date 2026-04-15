import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';
import i18n from '../i18n/i18n';

/**
 * Merge class names with Tailwind CSS support
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Format duration in hours and minutes
 */
export function formatDuration(seconds: number): string {
  const totalMins = Math.floor(seconds / 60);
  const hours = Math.floor(totalMins / 60);
  const mins = totalMins % 60;

  if (hours === 0) {
    return `${mins}m`;
  }

  if (mins === 0) {
    return `${hours}h`;
  }

  return `${hours}h ${mins}m`;
}

/**
 * Format time as HH:MM
 */
export function formatTime(date: Date): string {
  return date.toLocaleTimeString(i18n.language, {
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Format date as DD/MM/YYYY
 */
export function formatDate(date: Date): string {
  return date.toLocaleDateString(i18n.language);
}

/**
 * Get relative time (e.g., "2 minutes ago")
 */
export function getRelativeTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return i18n.t('dates.now');
  if (diffMins < 60) return i18n.t('dates.minutesAgo', { count: diffMins });
  if (diffHours < 24) return i18n.t('dates.hoursAgo', { count: diffHours });
  if (diffDays < 7) return i18n.t('dates.daysAgo', { count: diffDays });

  return formatDate(date);
}
