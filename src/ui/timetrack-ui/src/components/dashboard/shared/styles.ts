/**
 * Shared style constants for dashboard components
 */

export const cardBase =
  "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl hover:border-[rgba(74,217,255,0.15)] hover:shadow-[0_0_0_1px_rgba(74,217,255,0.12),0_4px_24px_rgba(74,217,255,0.06),0_0_40px_rgba(74,217,255,0.03)] transition-all duration-200";

export const cardBaseNoHover =
  "bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl";

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
