/**
 * SessionList — Collapsible detailed list of activity sessions
 */

import { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { AppIcon } from '../dashboard/shared';
import { formatDuration } from '../../lib/utils';
import { fadeUp, staggerContainer, STAGGER } from '../../lib/animation';
import type { ActivityBlock } from '../../hooks/useActivitiesData';

interface SessionListProps {
  activities: ActivityBlock[];
}

const cardBase =
  'bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-xl';

const CATEGORY_FRIENDLY_NAMES: Record<string, string> = {
  development: 'Development',
  design: 'Design',
  productivity_tools: 'Productivity',
  communication: 'Communication',
  meetings: 'Meetings',
  entertainment: 'Entertainment',
  social_media: 'Social Media',
  browser_general: 'Browser',
  productive: 'Productive',
  neutral: 'Neutral',
  distraction: 'Distraction',
  other: 'Outros',
  unknown: '',
};

function formatSubcategory(raw: string): string {
  if (!raw || raw === 'unknown') return 'Sem Categoria';
  const friendly = CATEGORY_FRIENDLY_NAMES[raw.toLowerCase()];
  if (friendly) return friendly;
  // Fallback: replace underscores, capitalize each word
  return raw
    .split('_')
    .map(w => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

const productivityBadge = (prod: string) => {
  switch (prod) {
    case 'productive':
      return 'bg-[rgba(74,222,128,0.15)] border-[rgba(74,222,128,0.25)] text-[#4ade80]';
    case 'distraction':
      return 'bg-[rgba(248,113,113,0.15)] border-[rgba(248,113,113,0.25)] text-[#f87171]';
    default:
      return 'bg-[rgba(251,191,36,0.15)] border-[rgba(251,191,36,0.25)] text-[#fbbf24]';
  }
};

const productivityLabel = (prod: string) => {
  switch (prod) {
    case 'productive': return 'P';
    case 'distraction': return 'D';
    default: return 'N';
  }
};

function fmtTime(iso: string) {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

const DEFAULT_VISIBLE = 8;

export function SessionList({ activities }: SessionListProps) {
  const [expanded, setExpanded] = useState(false);

  const sorted = [...activities].sort(
    (a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime(),
  );

  const visible = expanded ? sorted : sorted.slice(0, DEFAULT_VISIBLE);
  const hasMore = sorted.length > DEFAULT_VISIBLE;

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
            Sessoes detalhadas
          </span>
          <span className="text-[10px] text-[rgba(245,247,251,0.3)] px-2 py-0.5 rounded-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)]">
            {sorted.length}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-3 pb-3 px-4">
        {sorted.length === 0 ? (
          <p className="text-[11px] text-[rgba(245,247,251,0.3)] text-center py-4">
            Nenhuma sessao registrada
          </p>
        ) : (
          <>
            <motion.div
              className="space-y-1"
              variants={staggerContainer(STAGGER.listItems)}
              initial="hidden"
              animate="visible"
            >
              {visible.map((a, i) => (
                <motion.div
                  key={a.id || i}
                  className="flex items-center gap-2 py-1.5 px-2 rounded-lg hover:bg-[rgba(255,255,255,0.02)] transition-colors"
                  variants={fadeUp}
                >
                  {/* Time range */}
                  <span className="text-[10px] text-[rgba(245,247,251,0.4)] tabular-nums w-[80px] flex-shrink-0">
                    {fmtTime(a.startUtc)} – {fmtTime(a.endUtc)}
                  </span>

                  {/* Duration */}
                  <span className="text-[10px] text-[rgba(245,247,251,0.5)] w-[40px] flex-shrink-0 text-right tabular-nums">
                    {formatDuration(a.duration)}
                  </span>

                  {/* App icon + name */}
                  <div className="flex items-center gap-1.5 flex-1 min-w-0">
                    <AppIcon name={a.name} size={13} />
                    <span className="text-[10px] text-[rgba(245,247,251,0.7)] truncate">
                      {a.name}
                    </span>
                  </div>

                  {/* Category */}
                  <span className="text-[9px] text-[rgba(245,247,251,0.3)] truncate max-w-[80px] flex-shrink-0">
                    {formatSubcategory(a.subcategory)}
                  </span>

                  {/* Productivity badge */}
                  {a.productivity && (
                    <span
                      className={`px-1.5 py-0.5 text-[8px] rounded-full border flex-shrink-0 ${productivityBadge(a.productivity)}`}
                    >
                      {productivityLabel(a.productivity)}
                    </span>
                  )}
                </motion.div>
              ))}
            </motion.div>

            {/* Show more/less toggle */}
            {hasMore && (
              <AnimatePresence mode="wait">
                <motion.button
                  key={expanded ? 'less' : 'more'}
                  onClick={() => setExpanded(!expanded)}
                  className="flex items-center justify-center gap-1 w-full mt-2 py-1.5 rounded-lg text-[10px] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.03)] transition-colors"
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  transition={{ duration: 0.2 }}
                >
                  {expanded ? (
                    <>
                      <ChevronUp className="w-3 h-3" /> Mostrar menos
                    </>
                  ) : (
                    <>
                      <ChevronDown className="w-3 h-3" /> Mostrar mais ({sorted.length - DEFAULT_VISIBLE})
                    </>
                  )}
                </motion.button>
              </AnimatePresence>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
