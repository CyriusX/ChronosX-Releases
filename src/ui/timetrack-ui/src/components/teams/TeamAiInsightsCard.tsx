import { useState, useEffect, useCallback } from 'react';
import { motion } from 'motion/react';
import { Brain, AlertTriangle, CheckCircle2, Loader2 } from 'lucide-react';
import { getTeamLiveInsights, type TeamLiveInsightsResponse } from '../../services/insightsApi';

const POLL_INTERVAL = 120_000;

export function TeamAiInsightsCard() {
  const [data, setData] = useState<TeamLiveInsightsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetchData = useCallback(async () => {
    try {
      const response = await getTeamLiveInsights();
      setData(response);
    } catch {
      // Silently fail
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [fetchData]);

  if (isLoading) {
    return (
      <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
          <span className="text-[12px] text-[rgba(245,247,251,0.4)]">Analyzing team activity...</span>
        </div>
      </div>
    );
  }

  if (!data) return null;

  const attentionMembers = data.members.filter(m => m.latestAlert);
  const isAllGood = attentionMembers.length === 0;

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.15)] p-5"
    >
      <div className="flex items-center gap-2 mb-4">
        <div className="w-6 h-6 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <Brain className="w-3.5 h-3.5 text-[#8B5CF6]" />
        </div>
        <span className="text-[13px] font-medium text-[#f5f7fb]">
          Team AI Insights
        </span>
      </div>

      {isAllGood ? (
        <div className="flex items-center gap-2 py-2">
          <CheckCircle2 className="w-4 h-4 text-[#22c55e]" />
          <span className="text-[13px] text-[rgba(245,247,251,0.65)]">
            Team is focused! No alerts at the moment.
          </span>
        </div>
      ) : (
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-[#eab308]" />
            <span className="text-[13px] text-[rgba(245,247,251,0.65)]">
              {attentionMembers.length} member{attentionMembers.length > 1 ? 's' : ''} may need attention
            </span>
          </div>

          <div className="space-y-2">
            {attentionMembers.slice(0, 5).map(member => (
              <div
                key={member.userId}
                className="flex items-center gap-3 p-2.5 rounded-xl bg-[rgba(255,255,255,0.03)]"
              >
                <div className="w-7 h-7 rounded-full bg-[rgba(139,92,246,0.2)] flex items-center justify-center text-[11px] font-medium text-[#8B5CF6]">
                  {member.userName?.charAt(0)?.toUpperCase() ?? '?'}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[12px] font-medium text-[#f5f7fb] truncate">
                    {member.userName}
                  </div>
                  {member.latestAlert && (
                    <div className="text-[11px] text-[rgba(245,247,251,0.45)] truncate">
                      {member.latestAlert.message}
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-1.5 px-2 py-0.5 rounded-lg bg-[rgba(255,255,255,0.04)]">
                  <span className="text-[11px] font-medium text-[rgba(245,247,251,0.5)]">
                    {member.focusEstimate}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {data.orgAlert && (
        <div className="mt-3 pt-3 border-t border-[rgba(255,255,255,0.06)]">
          <p className="text-[12px] text-[rgba(245,247,251,0.45)]">
            {data.orgAlert.message}
          </p>
        </div>
      )}
    </motion.div>
  );
}
