/**
 * Reports Components Barrel Export
 *
 * Composition Pattern: All report-related components composed together
 * SRP: Each component handles a single responsibility
 * OCP: New components can be added without modifying existing ones
 * DIP: All components receive data via props
 */

// Summary Card
export { SummaryCard } from './SummaryCard';
export type { SummaryCardProps } from './SummaryCard';

// Activity Heatmap (GitHub-style)
export { ActivityHeatmap } from './ActivityHeatmap';
export type { ActivityHeatmapProps } from './ActivityHeatmap';

// Productivity Trend (Stacked bar chart)
export { ProductivityTrend } from './ProductivityTrend';
export type { ProductivityTrendProps } from './ProductivityTrend';

// Top Apps Section (with category filter)
export { TopAppsSection } from './TopAppsSection';
export type { TopAppsSectionProps } from './TopAppsSection';

// Top Paths Section (URLs and file paths)
export { TopPathsSection } from './TopPathsSection';
export type { TopPathsSectionProps } from './TopPathsSection';

// Category Donut Chart
export { CategoryDonut } from './CategoryDonut';
export type { CategoryDonutProps } from './CategoryDonut';

// Distraction Section
export { DistractionSection } from './DistractionSection';
export type { DistractionSectionProps } from './DistractionSection';
