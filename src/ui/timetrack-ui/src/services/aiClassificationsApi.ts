import { api } from './apiClient';

// ============================================================================
// TYPES
// ============================================================================

export type DecisionType = 'app_classification' | 'app_usage_suggestion';

export interface UserUsageInfo {
  userId: string | null;
  userName: string | null;
  hours: number;
}

export interface AiClassificationItem {
  id: string;
  decisionType: DecisionType;
  exeName: string;
  currentCategory: string | null;
  suggestedCategory: string;
  suggestedSubcategory: string | null;
  confidence: number | null;
  reasoning: string | null;
  topUsers: UserUsageInfo[] | null;
  totalOrgHours: number | null;
  sampleWindowTitles: string[] | null;
  createdAt: string;
}

export interface AiClassificationsResponse {
  items: AiClassificationItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ReviewClassificationRequest {
  outcome: 'accepted' | 'corrected' | 'rejected';
  correctCategory?: string;
  correctSubcategory?: string;
}

export interface ReviewClassificationResponse {
  decisionId: string;
  outcome: string;
}

export interface AcceptAllResponse {
  accepted: number;
}

// ============================================================================
// API CALLS
// ============================================================================

const BASE_PATH = '/admin/ai/classifications';

export async function getPendingClassifications(
  page: number = 1,
  pageSize: number = 20,
  confidence?: string,
  decisionType?: DecisionType
): Promise<AiClassificationsResponse> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  if (confidence) {
    params.append('confidence', confidence);
  }
  if (decisionType) {
    params.append('decisionType', decisionType);
  }
  return api.get<AiClassificationsResponse>(`${BASE_PATH}?${params.toString()}`);
}

export async function reviewClassification(
  decisionId: string,
  request: ReviewClassificationRequest
): Promise<ReviewClassificationResponse> {
  return api.post<ReviewClassificationResponse>(`${BASE_PATH}/${decisionId}/review`, request);
}

export async function acceptAllClassifications(): Promise<AcceptAllResponse> {
  return api.post<AcceptAllResponse>(`${BASE_PATH}/accept-all`);
}
