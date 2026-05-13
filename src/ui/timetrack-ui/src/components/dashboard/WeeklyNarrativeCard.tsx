import { useState, useEffect, useCallback } from 'react';
import { motion } from 'motion/react';
import { ThumbsUp, ThumbsDown, Loader2, Sparkles } from 'lucide-react';
import { getWeeklyNarrative, submitNarrativeFeedback } from '../../services/insightsApi';

export function WeeklyNarrativeCard() {
  const [narrative, setNarrative] = useState<string | null>(null);
  const [narrativeId, setNarrativeId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [feedback, setFeedback] = useState<'useful' | 'not_useful' | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchNarrative = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getWeeklyNarrative();
      setNarrative(response.narrative);
      if (response.narrative) {
        setNarrativeId(null); // ID not returned by narrative endpoint
      }
    } catch {
      setNarrative(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchNarrative();
  }, [fetchNarrative]);

  const handleFeedback = async (outcome: 'useful' | 'not_useful') => {
    if (isSubmitting) return;
    setFeedback(outcome);
    if (!narrativeId) return;

    setIsSubmitting(true);
    try {
      await submitNarrativeFeedback(narrativeId, outcome);
    } catch {
      // Feedback is non-critical
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
          <span className="text-[12px] text-[rgba(245,247,251,0.4)]">Loading weekly summary...</span>
        </div>
        <div className="space-y-2">
          <div className="h-3 bg-[rgba(255,255,255,0.04)] rounded w-full animate-pulse" />
          <div className="h-3 bg-[rgba(255,255,255,0.04)] rounded w-4/5 animate-pulse" />
          <div className="h-3 bg-[rgba(255,255,255,0.04)] rounded w-3/5 animate-pulse" />
        </div>
      </div>
    );
  }

  if (!narrative) return null;

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.15)] p-5"
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
            <Sparkles className="w-3.5 h-3.5 text-[#8B5CF6]" />
          </div>
          <span className="text-[13px] font-medium text-[#f5f7fb]">
            Your week in summary
          </span>
        </div>

        <div className="flex items-center gap-1">
          <button
            onClick={() => handleFeedback('useful')}
            disabled={isSubmitting}
            className={`w-7 h-7 rounded-lg flex items-center justify-center transition-colors ${
              feedback === 'useful'
                ? 'bg-[rgba(34,197,94,0.2)] text-[#22c55e]'
                : 'text-[rgba(245,247,251,0.3)] hover:text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.04)]'
            }`}
            title="Useful"
          >
            <ThumbsUp className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={() => handleFeedback('not_useful')}
            disabled={isSubmitting}
            className={`w-7 h-7 rounded-lg flex items-center justify-center transition-colors ${
              feedback === 'not_useful'
                ? 'bg-[rgba(239,68,68,0.2)] text-[#ef4444]'
                : 'text-[rgba(245,247,251,0.3)] hover:text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.04)]'
            }`}
            title="Not useful"
          >
            <ThumbsDown className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      <p className="text-[13px] text-[rgba(245,247,251,0.65)] leading-relaxed">
        {narrative}
      </p>
    </motion.div>
  );
}
