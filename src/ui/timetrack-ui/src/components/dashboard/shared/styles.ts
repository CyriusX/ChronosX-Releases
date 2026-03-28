/**
 * Shared style constants for dashboard components
 */

export const cardBase =
  "glass-card-interactive overflow-hidden";

export const cardBaseNoHover =
  "glass-card overflow-hidden";

export const cardElevated =
  "glass-card-elevated overflow-hidden";

export const cardCompact =
  "glass-card overflow-hidden rounded-[16px]";

export const MEMBER_GRADIENTS = [
  'from-[#ff8904] to-[#f6339a]',
  'from-[#51a2ff] to-[#00b8db]',
  'from-[#c27aff] to-[#f6339a]',
  'from-[#05df72] to-[#00bba7]',
  'from-[#fdc700] to-[#ff6900]',
];

export function getMemberGradient(name: string): string {
  const hash = name.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
  return MEMBER_GRADIENTS[hash % MEMBER_GRADIENTS.length];
}

export const ACCENT_COLORS = {
  purple: '#8B5CF6',
  blue: '#3B82F6',
  cyan: '#22D3EE',
  amber: '#F59E0B',
  green: '#10B981',
  red: '#EF4444',
  pink: '#EC4899',
} as const;

export const CHART_COLORS = [
  '#8B5CF6', '#22D3EE', '#3B82F6', '#F59E0B', '#10B981', '#EC4899', '#EF4444',
];
