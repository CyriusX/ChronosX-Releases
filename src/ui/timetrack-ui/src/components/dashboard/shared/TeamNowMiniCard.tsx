/**
 * TeamNowMiniCard - Inline team member list card for TopCards row
 */

import { MoreVertical, ChevronDown } from 'lucide-react';
import { motion } from 'motion/react';
import { Card, CardContent, CardHeader, CardTitle } from '../../ui/card';
import { cardBase, getMemberGradient } from './styles';
import { fadeUp, SPRING } from '../../../lib/animation';
import type { TeamMemberStatus } from '../../../types/member';

interface TeamNowMiniCardProps {
  members: TeamMemberStatus[];
  isLoading?: boolean;
}

export function TeamNowMiniCard({ members, isLoading }: TeamNowMiniCardProps) {
  const activeMembers = members.filter(m => m.status === 'Active').slice(0, 5);

  return (
    <motion.div variants={fadeUp} transition={{ type: 'spring', ...SPRING.gentle }} className="h-full">
      <Card className={`${cardBase} h-full`}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle className="flex items-center justify-between">
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">Equipe agora</span>
            <MoreVertical className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4">
          {/* Filter dropdown */}
          <button className="w-full flex items-center justify-between px-2.5 py-1.5 mb-2.5 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] text-[10px] hover:bg-[rgba(255,255,255,0.05)] transition-colors">
            <span className="text-[rgba(245,247,251,0.5)]">Todos os times</span>
            <ChevronDown className="w-3 h-3 text-[rgba(245,247,251,0.3)]" />
          </button>

          {isLoading ? (
            <div className="flex items-center justify-center py-4">
              <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
            </div>
          ) : activeMembers.length > 0 ? (
            <div className="space-y-2">
              {activeMembers.map((member) => {
                const gradient = getMemberGradient(member.displayName);
                return (
                  <div key={member.userId} className="flex items-center gap-2.5">
                    <div className={`w-6 h-6 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center flex-shrink-0`}>
                      <span className="text-[9px] font-semibold text-white">
                        {member.displayName.charAt(0).toUpperCase()}
                      </span>
                    </div>
                    <span className="text-[11px] text-[rgba(245,247,251,0.8)] flex-1 truncate">
                      {member.displayName}
                    </span>
                    <div className="flex items-center gap-1.5 flex-shrink-0">
                      {member.isTracking && (
                        <div className="w-1.5 h-1.5 rounded-full bg-[#05df72] animate-pulse" />
                      )}
                      <span className="text-[10px] text-[rgba(245,247,251,0.4)]">
                        {member.todayDurationFormatted}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="text-[10px] text-[rgba(245,247,251,0.4)] text-center py-3">
              Nenhum membro ativo
            </p>
          )}
        </CardContent>
      </Card>
    </motion.div>
  );
}
