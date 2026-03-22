/**
 * TopAppsSection - Top apps list with category filter
 *
 * SRP: Apenas exibe lista de apps mais usados
 * OCP: Extensível via props
 * DIP: Recebe dados via props
 *
 * Composition: Composto por AppItem e AppIcon (de dashboard/shared)
 */

import { useState, useMemo } from "react";
import { motion } from "motion/react";
import { Card, CardContent, CardHeader, CardTitle } from "../../ui/card";
import { AppItem } from "../../dashboard/shared/AppItem";
import { AppIcon } from "../../dashboard/shared/AppIcon";
import { fadeUp, staggerContainer, STAGGER } from "../../../lib/animation";
import type { TopAppItem, ProductivityCategory } from "../../../types/reports";

export interface TopAppsSectionProps {
  /** Top apps data */
  apps: TopAppItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Title */
  title?: string;
  /** Max items to show */
  maxItems?: number;
}

function getProductivityColor(productivity?: string): string {
  switch (productivity) {
    case "productive":
      return "rgba(74,222,128,0.15)"; // #4ade80
    case "distraction":
      return "rgba(248,113,113,0.15)"; // #f87171
    case "neutral":
    default:
      return "rgba(251,191,36,0.15)"; // #fbbf24
  }
}

function getProductivityBorderColor(productivity?: string): string {
  switch (productivity) {
    case "productive":
      return "rgba(74,222,128,0.25)"; // #4ade80
    case "distraction":
      return "rgba(248,113,113,0.25)"; // #f87171
    case "neutral":
    default:
      return "rgba(251,191,36,0.25)"; // #fbbf24
  }
}

function formatTime(seconds: number): string {
  if (seconds === 0) return "0m";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

export function TopAppsSection({
  apps,
  isLoading = false,
  title = "Apps Mais Usados",
  maxItems = 10,
}: TopAppsSectionProps) {
  const [filter, setFilter] = useState<ProductivityCategory | "all">("all");

  // Filter apps by category
  const filteredApps = useMemo(() => {
    if (filter === "all") return apps;
    return apps.filter((app) => app.productivity === filter);
  }, [apps, filter]);

  // Calculate max percentage for scaling
  const maxSeconds = useMemo(() => {
    if (filteredApps.length === 0) return 1;
    return Math.max(...filteredApps.map((a) => a.totalSeconds), 1);
  }, [filteredApps]);

  // Apply maxItems limit
  const displayedApps = useMemo(() => {
    return filteredApps.slice(0, maxItems);
  }, [filteredApps, maxItems]);

  // Count by category
  const categoryCounts = useMemo(() => {
    const counts = { productive: 0, neutral: 0, distraction: 0 };
    apps.forEach((app) => {
      if (
        app.productivity &&
        counts[app.productivity as keyof typeof counts] !== undefined
      ) {
        counts[app.productivity as keyof typeof counts]++;
      }
    });
    return counts;
  }, [apps]);

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
    <Card className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl h-full flex flex-col">
      <CardHeader className="pb-2 pt-3 px-4 shrink-0">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            {title}
          </span>
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
            {apps.length} apps
          </span>
        </CardTitle>
        {/* Filter buttons */}
        <div className="flex gap-1 mt-2">
          <button
            onClick={() => setFilter("all")}
            className={`px-2 py-0.5 text-[9px] rounded-md transition-colors ${
              filter === "all"
                ? "bg-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.9)]"
                : "bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)]"
            }`}
          >
            Todos
          </button>
          <button
            onClick={() => setFilter("productive")}
            className={`px-2 py-0.5 text-[9px] rounded-md transition-colors flex items-center gap-1 ${
              filter === "productive"
                ? "bg-[rgba(74,222,128,0.15)] text-[#4ade80]"
                : "bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)]"
            }`}
          >
            Produtivo
            <span className="text-[8px]">({categoryCounts.productive})</span>
          </button>
          <button
            onClick={() => setFilter("neutral")}
            className={`px-2 py-0.5 text-[9px] rounded-md transition-colors flex items-center gap-1 ${
              filter === "neutral"
                ? "bg-[rgba(251,191,36,0.15)] text-[#fbbf24]"
                : "bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)]"
            }`}
          >
            Neutro
            <span className="text-[8px]">({categoryCounts.neutral})</span>
          </button>
          <button
            onClick={() => setFilter("distraction")}
            className={`px-2 py-0.5 text-[9px] rounded-md transition-colors flex items-center gap-1 ${
              filter === "distraction"
                ? "bg-[rgba(248,113,113,0.15)] text-[#f87171]"
                : "bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)]"
            }`}
          >
            Distração
            <span className="text-[8px]">({categoryCounts.distraction})</span>
          </button>
        </div>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4 flex-1 overflow-hidden">
        {displayedApps.length === 0 ? (
          <div className="flex items-center justify-center h-full min-h-[100px] text-[rgba(245,247,251,0.4)] text-[12px]">
            Sem dados para exibir
          </div>
        ) : (
          <motion.div
            className="space-y-1.5 h-full overflow-y-auto"
            variants={staggerContainer(STAGGER.listItems)}
            initial="hidden"
            animate="visible"
          >
            {displayedApps.map((app, index) => (
              <motion.div
                key={`${app.displayName}-${index}`}
                variants={fadeUp}
              >
                <AppItem
                  percentage={(app.totalSeconds / maxSeconds) * 100}
                  icon={<AppIcon name={app.displayName} size={12} />}
                  label={app.displayName}
                  time={formatTime(app.totalSeconds)}
                  color={getProductivityColor(app.productivity)}
                  borderColor={getProductivityBorderColor(app.productivity)}
                />
              </motion.div>
            ))}
          </motion.div>
        )}
      </CardContent>
    </Card>
  );
}
