/**
 * BigMetricCard - Large number + subtitle display pattern
 */

import type { ReactNode } from 'react';
import { motion } from 'motion/react';
import { MoreVertical } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { useAnimatedCounter } from '../../../hooks/useAnimatedCounter';
import { cardBase } from './styles';
import { fadeUp, SPRING } from '../../../lib/animation';

interface BigMetricCardProps {
  title: string;
  value: number;
  subtitle: string;
  icon?: ReactNode;
  valueColor?: string;
}

export function BigMetricCard({ title, value, subtitle, icon, valueColor }: BigMetricCardProps) {
  const animatedValue = useAnimatedCounter(value);

  return (
    <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
      <Card className={`${cardBase} h-full`}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              {icon}
              <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{title}</span>
            </div>
            <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-4 pb-3 px-4">
          <div className="flex flex-col items-center justify-center">
            <span
              className="text-[40px] font-bold leading-none"
              style={{ color: valueColor ?? '#f5f7fb' }}
            >
              {animatedValue}
            </span>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-2">{subtitle}</p>
          </div>
        </CardContent>
      </Card>
    </motion.div>
  );
}
