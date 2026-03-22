/**
 * CategoryDonut - Donut chart for category distribution
 *
 * SRP: Apenas exibe gráfico de rosca para distribuição de categorias
 * OCP: Extensível via props
 * DIP: Recebe dados via props
 *
 * Composition: Composto por DonutSegment e CategoryLegend
 */

import { useState, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import type { CategoryDistributionItem, SubcategoryItem } from '../../../types/reports';

export interface CategoryDonutProps {
  /** Category distribution data */
  categories: CategoryDistributionItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Title */
  title?: string;
}

const CATEGORY_COLORS: Record<string, string> = {
  // Primary productivity categories (matching Dashboard)
  productive: '#4ade80',
  neutral: '#fbbf24',
  distraction: '#f87171',
  // Subcategories
  development: '#4ade80',
  communication: '#4ad9ff',
  productivity_tools: '#c27aff',
  research: '#06b6d4',
  entertainment: '#f87171',
  social: '#f97316',
  social_media: '#f97316',
  utilities: '#64748b',
  uncategorized: '#475569',
  unknown: '#475569',
  meetings: '#ec4899',
  email: '#14b8a6',
  design: '#a855f7',
  documentation: '#06b6d4',
  browser_general: '#51a2ff',
  other: '#71717a',
};

function formatTime(seconds: number): string {
  if (seconds === 0) return '0m';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

interface DonutSegmentProps {
  startAngle: number;
  endAngle: number;
  color: string;
  radius: number;
  strokeWidth: number;
}

function DonutSegment({ startAngle, endAngle, color, radius, strokeWidth }: DonutSegmentProps) {
  const cx = 50;
  const cy = 50;

  // Convert angles to radians
  const startRad = (startAngle - 90) * (Math.PI / 180);
  const endRad = (endAngle - 90) * (Math.PI / 180);

  // Calculate arc points
  const x1 = cx + radius * Math.cos(startRad);
  const y1 = cy + radius * Math.sin(startRad);
  const x2 = cx + radius * Math.cos(endRad);
  const y2 = cy + radius * Math.sin(endRad);

  // Determine if arc should be drawn as large arc
  const largeArc = endAngle - startAngle > 180 ? 1 : 0;

  const d = [
    `M ${x1} ${y1}`,
    `A ${radius} ${radius} 0 ${largeArc} 1 ${x2} ${y2}`,
  ].join(' ');

  return (
    <path
      d={d}
      fill="none"
      stroke={color}
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      className="transition-all duration-200"
    />
  );
}

interface CategoryLegendItemProps {
  category: string;
  totalSeconds: number;
  percentage: number;
  color: string;
  subcategories: SubcategoryItem[];
  isExpanded: boolean;
  onToggle: () => void;
}

function CategoryLegendItem({
  category,
  totalSeconds,
  percentage,
  color,
  subcategories,
  isExpanded,
  onToggle,
}: CategoryLegendItemProps) {
  const hasSubcategories = subcategories.length > 0;

  return (
    <div className="group">
      <div
        className={`flex items-center gap-3 py-1.5 px-2 rounded-lg transition-colors ${hasSubcategories ? 'cursor-pointer hover:bg-[rgba(255,255,255,0.03)]' : ''}`}
        onClick={hasSubcategories ? onToggle : undefined}
      >
        <div
          className="w-3 h-3 rounded-md shrink-0 ring-1 ring-[rgba(255,255,255,0.1)]"
          style={{ backgroundColor: color }}
        />
        <span className="text-[12px] text-[rgba(245,247,251,0.85)] flex-1 capitalize font-medium">
          {category.replace(/_/g, ' ')}
        </span>
        <span className="text-[11px] text-[rgba(245,247,251,0.6)] font-medium">
          {formatTime(totalSeconds)}
        </span>
        <span className="text-[11px] text-[rgba(245,247,251,0.4)] w-[42px] text-right">
          {percentage.toFixed(1)}%
        </span>
        {hasSubcategories && (
          <span className="text-[10px] text-[rgba(74,217,255,0.6)] ml-1">
            {isExpanded ? '▲' : '▼'}
          </span>
        )}
      </div>
      {isExpanded && hasSubcategories && (
        <div className="ml-5 mt-1 space-y-1 border-l-2 border-[rgba(255,255,255,0.08)] pl-3">
          {subcategories.map((sub, i) => (
            <div key={`${sub.name}-${i}`} className="flex items-center gap-2 py-0.5">
              <div
                className="w-2 h-2 rounded-sm shrink-0"
                style={{ backgroundColor: color, opacity: 0.7 }}
              />
              <span className="text-[11px] text-[rgba(245,247,251,0.6)] flex-1">
                {sub.name.replace(/_/g, ' ')}
              </span>
              <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                {formatTime(sub.totalSeconds)}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function CategoryDonut({
  categories,
  isLoading = false,
  title = 'Distribuição por Categoria',
}: CategoryDonutProps) {
  const [expandedCategory, setExpandedCategory] = useState<string | null>(null);

  // Calculate total for center display
  const totalSeconds = useMemo(() => {
    return categories.reduce((sum, c) => sum + c.totalSeconds, 0);
  }, [categories]);

  // Prepare donut segments
  const segments = useMemo(() => {
    if (categories.length === 0) return [];

    let currentAngle = 0;
    return categories.map((cat) => {
      const angle = (cat.percentage / 100) * 360;
      const segment = {
        startAngle: currentAngle,
        endAngle: currentAngle + angle,
        color: CATEGORY_COLORS[cat.category] || '#64748b',
        category: cat.category,
      };
      currentAngle += angle;
      return segment;
    });
  }, [categories]);

  // Toggle category expansion
  const handleToggle = (category: string) => {
    setExpandedCategory(expandedCategory === category ? null : category);
  };

  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
        <CardHeader className="pb-2 pt-3 px-4">
          <CardTitle className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          <div className="flex items-center justify-center h-[200px]">
            <div className="w-6 h-6 border-2 border-[#4ad9ff] border-t-transparent rounded-full animate-spin" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl">
      <CardHeader className="pb-3 pt-4 px-5">
        <CardTitle className="text-[14px] font-semibold text-[rgba(245,247,251,0.95)]">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-0 pb-4 px-5">
        {categories.length === 0 ? (
          <div className="flex items-center justify-center h-[180px] text-[rgba(245,247,251,0.4)] text-[13px]">
            Sem dados para exibir
          </div>
        ) : (
          <div className="flex gap-6 items-center">
            {/* Donut Chart */}
            <div className="shrink-0">
              <div className="relative w-[160px] h-[160px]">
                <svg viewBox="0 0 100 100" className="w-full h-full">
                  {/* Background circle */}
                  <circle
                    cx="50"
                    cy="50"
                    r="38"
                    fill="none"
                    stroke="rgba(255,255,255,0.04)"
                    strokeWidth="20"
                  />
                  {/* Segments */}
                  {segments.map((seg, i) => (
                    <DonutSegment
                      key={`${seg.category}-${i}`}
                      startAngle={seg.startAngle}
                      endAngle={seg.endAngle}
                      color={seg.color}
                      radius={38}
                      strokeWidth={20}
                    />
                  ))}
                </svg>
                {/* Center text */}
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-[18px] font-bold text-[#f5f7fb]">
                    {formatTime(totalSeconds)}
                  </span>
                  <span className="text-[10px] text-[rgba(245,247,251,0.5)] mt-0.5">tempo total</span>
                </div>
              </div>
            </div>

            {/* Legend */}
            <div className="flex-1 space-y-2 max-h-[180px] overflow-y-auto pr-2">
              {categories.map((cat) => (
                <CategoryLegendItem
                  key={cat.category}
                  category={cat.category}
                  totalSeconds={cat.totalSeconds}
                  percentage={cat.percentage}
                  color={CATEGORY_COLORS[cat.category] || '#64748b'}
                  subcategories={cat.subcategories}
                  isExpanded={expandedCategory === cat.category}
                  onToggle={() => handleToggle(cat.category)}
                />
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
