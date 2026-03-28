/**
 * SummaryCard - Reusable card for displaying summary statistics
 *
 * SRP: Apenas exibe um card de estatística
 * OCP: Extensível via props
 * DIP: Recebe dados via props (não depende de estado externo)
 *
 * Composition: Componente atômico para ser composto em SummaryCards
 */

import { motion } from 'motion/react';
import { LucideIcon } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';

export interface SummaryCardProps {
  /** Card title */
  title: string;
  /** Main value to display */
  value: string;
  /** Optional percentage or subtitle */
  subtitle?: string;
  /** Icon for the card */
  icon: LucideIcon;
  /** Icon background color (CSS color or gradient) */
  iconBgColor?: string;
  /** Icon color */
  iconColor?: string;
  /** Progress percentage (0-100) for circular progress */
  progress?: number;
  /** Progress bar color */
  progressColor?: string;
  /** Optional badge text */
  badge?: string;
  /** Badge color */
  badgeColor?: 'green' | 'yellow' | 'red' | 'blue' | 'purple';
  /** Loading state */
  isLoading?: boolean;
}

const badgeStyles: Record<string, string> = {
  green: 'bg-[rgba(74,222,128,0.15)] border-[rgba(74,222,128,0.25)] text-[#4ade80]',
  yellow: 'bg-[rgba(251,191,36,0.15)] border-[rgba(251,191,36,0.25)] text-[#fbbf24]',
  red: 'bg-[rgba(248,113,113,0.15)] border-[rgba(248,113,113,0.25)] text-[#f87171]',
  blue: 'bg-[rgba(139,92,246,0.15)] border-[rgba(139,92,246,0.25)] text-[#8B5CF6]',
  purple: 'bg-[rgba(138,92,246,0.15)] border-[rgba(138,92,246,0.25)] text-[#8a5cf6]',
};

export function SummaryCard({
  title,
  value,
  subtitle,
  icon: Icon,
  iconBgColor = 'rgba(139,92,246,0.15)',
  iconColor = '#8B5CF6',
  progress,
  progressColor = '#8B5CF6',
  badge,
  badgeColor = 'blue',
  isLoading = false,
}: SummaryCardProps) {
  const cardBase = 'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

  if (isLoading) {
    return (
      <Card className={`${cardBase} h-full`}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-5 h-5 rounded-md bg-[rgba(255,255,255,0.05)] animate-pulse" />
              <div className="w-20 h-4 bg-[rgba(255,255,255,0.05)] rounded animate-pulse" />
            </div>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-3 px-4 flex items-center justify-center flex-1">
          <div className="w-12 h-12 rounded-full border-2 border-[rgba(255,255,255,0.1)] border-t-[#8B5CF6] animate-spin" />
        </CardContent>
      </Card>
    );
  }

  return (
    <motion.div
      whileHover={{ y: -2, transition: { duration: 0.2 } }}
      className="h-full"
    >
      <Card className={`${cardBase} h-full`}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div
                className="w-5 h-5 rounded-md flex items-center justify-center"
                style={{ backgroundColor: iconBgColor }}
              >
                <Icon className="w-3 h-3" style={{ color: iconColor }} />
              </div>
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
            </div>
            {badge && (
              <span className={`px-2 py-0.5 text-[9px] rounded-full border ${badgeStyles[badgeColor]}`}>
                {badge}
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-3 pb-3 px-4">
          {progress !== undefined ? (
            <div className="flex flex-col items-center">
              <div className="relative w-20 h-20">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                  <circle cx="50" cy="50" r="38" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="8" />
                  <motion.circle
                    cx="50"
                    cy="50"
                    r="38"
                    fill="none"
                    stroke={progressColor}
                    strokeWidth="8"
                    strokeLinecap="round"
                    initial={{ strokeDasharray: '0 239' }}
                    animate={{ strokeDasharray: `${progress * 2.39} 239` }}
                    transition={{ duration: 0.8, ease: 'easeOut' }}
                  />
                </svg>
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-[18px] font-semibold text-[#f5f7fb]">{value}</span>
                </div>
              </div>
              {subtitle && (
                <p className="mt-2 text-[11px] text-[rgba(245,247,251,0.4)] text-center">{subtitle}</p>
              )}
            </div>
          ) : (
            <div className="flex flex-col items-center">
              <motion.span
                className="text-[24px] font-semibold text-[#f5f7fb]"
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ duration: 0.3 }}
              >
                {value}
              </motion.span>
              {subtitle && (
                <p className="mt-1 text-[12px] text-[rgba(245,247,251,0.5)] text-center">{subtitle}</p>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </motion.div>
  );
}
