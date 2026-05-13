import { useState, useEffect, useCallback } from 'react';
import {
  getPendingClassifications,
  reviewClassification,
  acceptAllClassifications,
  type AiClassificationItem,
  type ReviewClassificationRequest,
  type DecisionType,
} from '../../services/aiClassificationsApi';

export type ConfidenceFilter = 'all' | 'high' | 'medium' | 'low';
export type DecisionTypeFilter = 'all' | DecisionType;

interface UseAiClassificationsReturn {
  items: AiClassificationItem[];
  total: number;
  page: number;
  pageSize: number;
  confidenceFilter: ConfidenceFilter;
  decisionTypeFilter: DecisionTypeFilter;
  isLoading: boolean;
  isActionLoading: boolean;
  error: string | null;

  setPage: (page: number) => void;
  setConfidenceFilter: (filter: ConfidenceFilter) => void;
  setDecisionTypeFilter: (filter: DecisionTypeFilter) => void;
  refresh: () => Promise<void>;
  review: (decisionId: string, request: ReviewClassificationRequest) => Promise<void>;
  acceptAll: () => Promise<number>;
}

const CONFIDENCE_MAP: Record<ConfidenceFilter, string | undefined> = {
  all: undefined,
  high: '0.90',
  medium: '0.70',
  low: '0.50',
};

export function useAiClassifications(): UseAiClassificationsReturn {
  const [items, setItems] = useState<AiClassificationItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [confidenceFilter, setConfidenceFilter] = useState<ConfidenceFilter>('all');
  const [decisionTypeFilter, setDecisionTypeFilter] = useState<DecisionTypeFilter>('all');
  const [isLoading, setIsLoading] = useState(true);
  const [isActionLoading, setIsActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const confidence = CONFIDENCE_MAP[confidenceFilter];
      const dtFilter = decisionTypeFilter === 'all' ? undefined : decisionTypeFilter;
      const response = await getPendingClassifications(page, pageSize, confidence, dtFilter);
      setItems(response.items);
      setTotal(response.total);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load classifications');
    } finally {
      setIsLoading(false);
    }
  }, [page, pageSize, confidenceFilter, decisionTypeFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const review = useCallback(
    async (decisionId: string, request: ReviewClassificationRequest) => {
      setIsActionLoading(true);
      try {
        await reviewClassification(decisionId, request);
        await fetchData();
      } finally {
        setIsActionLoading(false);
      }
    },
    [fetchData]
  );

  const acceptAll = useCallback(async (): Promise<number> => {
    setIsActionLoading(true);
    try {
      const result = await acceptAllClassifications();
      await fetchData();
      return result.accepted;
    } finally {
      setIsActionLoading(false);
    }
  }, [fetchData]);

  return {
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
    refresh: fetchData,
    review,
    acceptAll,
  };
}
