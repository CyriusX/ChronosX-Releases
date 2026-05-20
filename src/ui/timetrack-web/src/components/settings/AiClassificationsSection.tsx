/**
 * AI Classifications Section - Web Admin Portal
 *
 * Allows managers/admins to review AI-generated app classification suggestions.
 * Supports two types of decisions:
 * - app_usage_suggestion: Based on real team usage patterns
 * - app_classification: Global AI-powered classification
 */

import { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Brain,
  Check,
  X,
  Edit3,
  ChevronLeft,
  ChevronRight,
  Zap,
  RefreshCw,
  Loader2,
  AlertTriangle,
  Shield,
  Tag,
  Users,
  ArrowRight,
  Clock,
} from 'lucide-react';
import { usePermissions } from '../../hooks/usePermissions';
import { useAiClassifications, type ConfidenceFilter, type DecisionTypeFilter } from '../../hooks/useAiClassifications';
import type { AiClassificationItem } from '../../services/aiClassificationsApi';

const CATEGORY_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  productive: {
    bg: 'rgba(34,197,94,0.1)',
    text: '#22c55e',
    border: 'rgba(34,197,94,0.2)',
  },
  neutral: {
    bg: 'rgba(139,92,246,0.1)',
    text: '#8B5CF6',
    border: 'rgba(139,92,246,0.2)',
  },
  distraction: {
    bg: 'rgba(239,68,68,0.1)',
    text: '#ef4444',
    border: 'rgba(239,68,68,0.2)',
  },
};

function ConfidenceBadge({ confidence }: { confidence: number | null }) {
  if (confidence == null) return null;
  const pct = Math.round(confidence * 100);
  const color =
    confidence >= 0.9 ? '#22c55e' : confidence >= 0.7 ? '#eab308' : '#ef4444';
  return (
    <span
      className="text-[11px] font-mono font-medium px-2 py-0.5 rounded-md"
      style={{ color, background: `${color}15` }}
    >
      {pct}%
    </span>
  );
}

function CategoryBadge({ category }: { category: string }) {
  const colors = CATEGORY_COLORS[category] ?? CATEGORY_COLORS.neutral;
  return (
    <span
      className="text-[11px] font-medium px-2 py-0.5 rounded-md capitalize"
      style={{
        color: colors.text,
        background: colors.bg,
        border: `1px solid ${colors.border}`,
      }}
    >
      {category}
    </span>
  );
}

function UsageBadge() {
  return (
    <span className="text-[10px] font-medium px-2 py-0.5 rounded-md bg-[rgba(139,92,246,0.12)] text-[#8B5CF6] border border-[rgba(139,92,246,0.2)]">
      Por uso real
    </span>
  );
}

function CorrectModal({
  isOpen,
  onClose,
  onSubmit,
  isSubmitting,
}: {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (category: string, subcategory?: string) => void;
  isSubmitting: boolean;
}) {
  const [category, setCategory] = useState('productive');
  const [subcategory, setSubcategory] = useState('');

  if (!isOpen) return null;

  const handleSubmit = () => {
    onSubmit(category, subcategory || undefined);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} />
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="relative bg-[#1a1d2e] border border-[rgba(255,255,255,0.08)] rounded-2xl p-6 w-full max-w-md mx-4 shadow-xl"
      >
        <h3 className="text-[16px] font-semibold text-[#f5f7fb] mb-4">
          Corrigir classificação
        </h3>

        <div className="space-y-4">
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-1.5 block">
              Categoria
            </label>
            <div className="flex gap-2">
              {(['productive', 'neutral', 'distraction'] as const).map((cat) => (
                <button
                  key={cat}
                  onClick={() => setCategory(cat)}
                  className={`flex-1 px-3 py-2 rounded-lg text-[12px] font-medium capitalize transition-colors ${
                    category === cat
                      ? 'bg-[rgba(139,92,246,0.2)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)]'
                      : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
                  }`}
                >
                  {cat === 'productive' && 'Produtiva'}
                  {cat === 'neutral' && 'Neutra'}
                  {cat === 'distraction' && 'Distração'}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-1.5 block">
              Subcategoria (opcional)
            </label>
            <input
              type="text"
              value={subcategory}
              onChange={(e) => setSubcategory(e.target.value)}
              placeholder="ex: desenvolvimento, comunicação..."
              className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder:text-[rgba(245,247,251,0.3)] outline-none focus:border-[rgba(139,92,246,0.3)]"
            />
          </div>
        </div>

        <div className="flex justify-end gap-3 mt-6">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-[13px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] transition-colors"
          >
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={isSubmitting}
            className="px-4 py-2 rounded-lg bg-[#8B5CF6] text-white text-[13px] font-medium hover:bg-[#7c3aed] disabled:opacity-50 transition-colors flex items-center gap-2"
          >
            {isSubmitting && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
            Salvar correção
          </button>
        </div>
      </motion.div>
    </div>
  );
}

// Action buttons shared between both card types
function CardActions({
  itemId,
  onAccept,
  onCorrect,
  onReject,
  disabled,
}: {
  itemId: string;
  onAccept: (id: string) => void;
  onCorrect: (id: string) => void;
  onReject: (id: string) => void;
  disabled: boolean;
}) {
  return (
    <div className="flex items-center gap-1.5 flex-shrink-0">
      <button
        onClick={() => onAccept(itemId)}
        disabled={disabled}
        title="Aceitar"
        className="w-8 h-8 rounded-lg bg-[rgba(34,197,94,0.1)] border border-[rgba(34,197,94,0.15)] flex items-center justify-center hover:bg-[rgba(34,197,94,0.2)] transition-colors disabled:opacity-50"
      >
        <Check className="w-4 h-4 text-[#22c55e]" />
      </button>
      <button
        onClick={() => onCorrect(itemId)}
        disabled={disabled}
        title="Corrigir"
        className="w-8 h-8 rounded-lg bg-[rgba(234,179,8,0.1)] border border-[rgba(234,179,8,0.15)] flex items-center justify-center hover:bg-[rgba(234,179,8,0.2)] transition-colors disabled:opacity-50"
      >
        <Edit3 className="w-4 h-4 text-[#eab308]" />
      </button>
      <button
        onClick={() => onReject(itemId)}
        disabled={disabled}
        title="Rejeitar"
        className="w-8 h-8 rounded-lg bg-[rgba(239,68,68,0.1)] border border-[rgba(239,68,68,0.15)] flex items-center justify-center hover:bg-[rgba(239,68,68,0.2)] transition-colors disabled:opacity-50"
      >
        <X className="w-4 h-4 text-[#ef4444]" />
      </button>
    </div>
  );
}

// Card for usage-based suggestions (app_usage_suggestion)
function UsageSuggestionCard({
  item,
  onAccept,
  onCorrect,
  onReject,
  isActionLoading,
}: {
  item: AiClassificationItem;
  onAccept: (id: string) => void;
  onCorrect: (id: string) => void;
  onReject: (id: string) => void;
  isActionLoading: boolean;
}) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, x: -20 }}
      className="bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(139,92,246,0.12)] rounded-xl p-4"
    >
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          {/* Header: exeName + badges */}
          <div className="flex items-center gap-2 mb-2">
            <Tag className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)] flex-shrink-0" />
            <span className="text-[14px] font-medium text-[#f5f7fb] truncate">
              {item.exeName}
            </span>
            <UsageBadge />
            <ConfidenceBadge confidence={item.confidence} />
          </div>

          {/* Category transition arrow */}
          <div className="flex items-center gap-2 mb-2.5">
            {item.currentCategory && (
              <>
                <CategoryBadge category={item.currentCategory} />
                <ArrowRight className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)]" />
              </>
            )}
            <CategoryBadge category={item.suggestedCategory} />
            {item.suggestedSubcategory && (
              <span className="text-[11px] text-[rgba(245,247,251,0.4)] capitalize">
                {item.suggestedSubcategory.replace(/_/g, ' ')}
              </span>
            )}
          </div>

          {/* Team usage */}
          {item.totalOrgHours != null && (
            <div className="flex items-center gap-1.5 mb-2">
              <Clock className="w-3 h-3 text-[rgba(245,247,251,0.3)]" />
              <span className="text-[11px] text-[rgba(245,247,251,0.5)]">
                {item.totalOrgHours.toFixed(1)}h/semana na equipe
              </span>
            </div>
          )}

          {/* Top users */}
          {item.topUsers && item.topUsers.length > 0 && (
            <div className="mb-2.5">
              <div className="flex items-center gap-1.5 mb-1">
                <Users className="w-3 h-3 text-[rgba(245,247,251,0.3)]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.4)]">Top colaboradores</span>
              </div>
              <div className="flex flex-wrap gap-1.5">
                {item.topUsers.slice(0, 4).map((user, idx) => (
                  <span
                    key={user.userId ?? idx}
                    className="text-[11px] px-2 py-0.5 rounded-md bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.6)]"
                  >
                    {user.userName ?? 'Desconhecido'} — {user.hours.toFixed(1)}h
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Window titles */}
          {item.sampleWindowTitles && item.sampleWindowTitles.length > 0 && (
            <div className="mb-2">
              <span className="text-[10px] text-[rgba(245,247,251,0.3)]">Títulos: </span>
              <span className="text-[10px] text-[rgba(245,247,251,0.25)]">
                {item.sampleWindowTitles.slice(0, 3).join(', ')}
              </span>
            </div>
          )}

          {/* Reasoning */}
          {item.reasoning && (
            <p className="text-[11px] text-[rgba(245,247,251,0.35)] line-clamp-2">
              {item.reasoning}
            </p>
          )}
        </div>

        <CardActions
          itemId={item.id}
          onAccept={onAccept}
          onCorrect={onCorrect}
          onReject={onReject}
          disabled={isActionLoading}
        />
      </div>
    </motion.div>
  );
}

// Card for global classification suggestions (app_classification)
function GlobalClassificationCard({
  item,
  onAccept,
  onCorrect,
  onReject,
  isActionLoading,
}: {
  item: AiClassificationItem;
  onAccept: (id: string) => void;
  onCorrect: (id: string) => void;
  onReject: (id: string) => void;
  isActionLoading: boolean;
}) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, x: -20 }}
      className="bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4"
    >
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1.5">
            <Tag className="w-3.5 h-3.5 text-[rgba(245,247,251,0.3)] flex-shrink-0" />
            <span className="text-[14px] font-medium text-[#f5f7fb] truncate">
              {item.exeName}
            </span>
          </div>

          <div className="flex items-center gap-2 mb-2">
            <CategoryBadge category={item.suggestedCategory} />
            {item.suggestedSubcategory && (
              <span className="text-[11px] text-[rgba(245,247,251,0.4)] capitalize">
                {item.suggestedSubcategory.replace(/_/g, ' ')}
              </span>
            )}
            <ConfidenceBadge confidence={item.confidence} />
          </div>

          {item.reasoning && (
            <p className="text-[11px] text-[rgba(245,247,251,0.35)] line-clamp-2">
              {item.reasoning}
            </p>
          )}
        </div>

        <CardActions
          itemId={item.id}
          onAccept={onAccept}
          onCorrect={onCorrect}
          onReject={onReject}
          disabled={isActionLoading}
        />
      </div>
    </motion.div>
  );
}

const DECISION_TYPE_TABS: { value: DecisionTypeFilter; label: string }[] = [
  { value: 'all', label: 'Todas' },
  { value: 'app_usage_suggestion', label: 'Por uso real' },
  { value: 'app_classification', label: 'Classificação global' },
];

export function AiClassificationsSection() {
  const { canEditOrgPolicies } = usePermissions();
  const [correctingId, setCorrectingId] = useState<string | null>(null);
  const [notification, setNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  // Check permissions - only Admins and Managers can access
  if (!canEditOrgPolicies) {
    return (
      <div className="flex flex-col items-center justify-center py-16">
        <Shield className="w-12 h-12 text-[rgba(245,247,251,0.2)] mb-4" />
        <h3 className="text-[16px] font-semibold text-[#f5f7fb] mb-2">
          Acesso restrito
        </h3>
        <p className="text-[13px] text-[rgba(245,247,251,0.5)]">
          Apenas Administradores e Gestores podem acessar esta funcionalidade
        </p>
      </div>
    );
  }

  const {
    items,
    total,
    page,
    pageSize,
    confidenceFilter,
    decisionTypeFilter,
    isLoading,
    isActionLoading,
    error,
    setPage,
    setConfidenceFilter,
    setDecisionTypeFilter,
    refresh,
    review,
    acceptAll,
  } = useAiClassifications();

  const totalPages = Math.ceil(total / pageSize);

  const showNotification = (type: 'success' | 'error', message: string) => {
    setNotification({ type, message });
    setTimeout(() => setNotification(null), 3000);
  };

  const handleAccept = async (decisionId: string) => {
    try {
      await review(decisionId, { outcome: 'accepted' });
      showNotification('success', 'Classificação aceita');
    } catch (err) {
      showNotification('error', err instanceof Error ? err.message : 'Falha ao aceitar');
    }
  };

  const handleReject = async (decisionId: string) => {
    try {
      await review(decisionId, { outcome: 'rejected' });
      showNotification('success', 'Classificação rejeitada');
    } catch (err) {
      showNotification('error', err instanceof Error ? err.message : 'Falha ao rejeitar');
    }
  };

  const handleCorrect = async (category: string, subcategory?: string) => {
    if (!correctingId) return;
    try {
      await review(correctingId, {
        outcome: 'corrected',
        correctCategory: category,
        correctSubcategory: subcategory,
      });
      showNotification('success', 'Classificação corrigida');
    } catch (err) {
      showNotification('error', err instanceof Error ? err.message : 'Falha ao corrigir');
    } finally {
      setCorrectingId(null);
    }
  };

  const handleAcceptAll = async () => {
    if (!window.confirm('Aceitar todas as sugestões com confiança >= 90%?')) return;
    try {
      const count = await acceptAll();
      showNotification('success', `${count} classificações aprovadas automaticamente`);
    } catch (err) {
      showNotification('error', err instanceof Error ? err.message : 'Falha ao aprovar todas');
    }
  };

  return (
    <div className="space-y-6">
      {/* Notification */}
      <AnimatePresence>
        {notification && (
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            className={`px-4 py-3 rounded-lg border ${
              notification.type === 'success'
                ? 'bg-[rgba(34,197,94,0.1)] border-[rgba(34,197,94,0.2)] text-[#22c55e]'
                : 'bg-[rgba(239,68,68,0.1)] border-[rgba(239,68,68,0.2)] text-[#ef4444]'
            } text-[13px]`}
          >
            {notification.message}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <Brain className="w-5 h-5 text-[#8B5CF6]" />
        </div>
        <div>
          <h2 className="text-[18px] font-semibold text-[#f5f7fb]">
            Classificações IA
          </h2>
          <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
            Revise e gerencie sugestões de classificação de apps geradas pela IA
          </p>
        </div>
      </div>

      {/* Stats & Actions */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)]">
          <Shield className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
          <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Pendentes</span>
          <span className="text-[12px] font-medium text-[#f5f7fb]">{total}</span>
        </div>

        {total > 0 && (
          <button
            onClick={handleAcceptAll}
            disabled={isActionLoading}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(34,197,94,0.1)] border border-[rgba(34,197,94,0.2)] text-[11px] text-[#22c55e] hover:bg-[rgba(34,197,94,0.15)] transition-colors disabled:opacity-50"
          >
            <Zap className="w-3.5 h-3.5" />
            Aceitar todas (≥90%)
          </button>
        )}

        <button
          onClick={refresh}
          disabled={isLoading}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] transition-colors"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          Atualizar
        </button>
      </div>

      {/* Decision Type Filter */}
      <div className="flex gap-2">
        {DECISION_TYPE_TABS.map((tab) => (
          <button
            key={tab.value}
            onClick={() => {
              setDecisionTypeFilter(tab.value);
              setPage(1);
            }}
            className={`px-3 py-1.5 rounded-lg text-[11px] font-medium transition-colors ${
              decisionTypeFilter === tab.value
                ? 'bg-[rgba(139,92,246,0.2)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)]'
                : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Confidence Filter */}
      <div className="flex gap-2">
        {(['all', 'high', 'medium', 'low'] as ConfidenceFilter[]).map((filter) => (
          <button
            key={filter}
            onClick={() => {
              setConfidenceFilter(filter);
              setPage(1);
            }}
            className={`px-3 py-1.5 rounded-lg text-[11px] font-medium transition-colors ${
              confidenceFilter === filter
                ? 'bg-[rgba(139,92,246,0.2)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)]'
                : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
            }`}
          >
            {filter === 'all' && 'Todas'}
            {filter === 'high' && '≥90%'}
            {filter === 'medium' && '≥70%'}
            {filter === 'low' && '≥50%'}
          </button>
        ))}
      </div>

      {/* Content */}
      {error && (
        <div className="bg-gradient-to-br from-[rgba(239,68,68,0.1)] to-[rgba(239,68,68,0.05)] border border-[rgba(239,68,68,0.2)] rounded-xl p-4 flex items-center gap-3">
          <AlertTriangle className="w-4 h-4 text-[#ef4444] flex-shrink-0" />
          <p className="text-[13px] text-[#ef4444]">{error}</p>
        </div>
      )}

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="h-[88px] rounded-xl bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] animate-pulse"
            />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="text-center py-12">
          <Brain className="w-10 h-10 text-[rgba(245,247,251,0.15)] mx-auto mb-3" />
          <p className="text-[14px] text-[rgba(245,247,251,0.4)]">
            Nenhuma classificação pendente
          </p>
          <p className="text-[12px] text-[rgba(245,247,251,0.25)] mt-1">
            As sugestões da IA aparecerão aqui com base nos dados de uso da equipe
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          <AnimatePresence>
            {items.map((item) =>
              item.decisionType === 'app_usage_suggestion' ? (
                <UsageSuggestionCard
                  key={item.id}
                  item={item}
                  onAccept={handleAccept}
                  onCorrect={setCorrectingId}
                  onReject={handleReject}
                  isActionLoading={isActionLoading}
                />
              ) : (
                <GlobalClassificationCard
                  key={item.id}
                  item={item}
                  onAccept={handleAccept}
                  onCorrect={setCorrectingId}
                  onReject={handleReject}
                  isActionLoading={isActionLoading}
                />
              )
            )}
          </AnimatePresence>
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <span className="text-[12px] text-[rgba(245,247,251,0.4)]">
            {total} resultado{total !== 1 ? 's' : ''}
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage(page - 1)}
              disabled={page <= 1 || isLoading}
              className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] disabled:opacity-30 transition-colors"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="text-[12px] text-[rgba(245,247,251,0.5)]">
              {page} / {totalPages}
            </span>
            <button
              onClick={() => setPage(page + 1)}
              disabled={page >= totalPages || isLoading}
              className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.08)] disabled:opacity-30 transition-colors"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}

      {/* Correct Modal */}
      <CorrectModal
        isOpen={correctingId !== null}
        onClose={() => setCorrectingId(null)}
        onSubmit={handleCorrect}
        isSubmitting={isActionLoading}
      />
    </div>
  );
}
