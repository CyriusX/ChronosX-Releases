/**
 * Types for Local Settings and Organization Policies
 *
 * SOLID:
 * - ISP: Interfaces segregadas por responsabilidade
 * - DIP: Contratos para comunicação com backend
 */

// ============================================================================
// LOCAL SETTINGS (Colaborador Preferences)
// ============================================================================

/**
 * Configurações locais do colaborador (preferências pessoais)
 */
export interface LocalSettings {
  /** Toggle para notificação de auto-resume quando tracking pausado por muito tempo */
  autoResumeNotificationEnabled: boolean;

  /** Toggle para sons de notificação */
  notificationSoundsEnabled: boolean;

  /** Idioma da interface (pt-BR, en-US) */
  language: 'pt-BR' | 'en-US';

  /** Timestamp da última atualização */
  updatedAt: string;
}

/**
 * Request para atualização parcial das configurações
 */
export interface UpdateLocalSettingsRequest {
  autoResumeNotificationEnabled?: boolean;
  notificationSoundsEnabled?: boolean;
  language?: 'pt-BR' | 'en-US';
}

// ============================================================================
// ORGANIZATION POLICIES (Backend API - Admin configurable)
// ============================================================================

/**
 * Work hours configuration
 */
export interface WorkHoursPolicy {
  /** IANA timezone identifier (e.g., "America/Sao_Paulo") */
  timezone: string;

  /** Days of the week when work hours apply */
  days: DayOfWeek[];

  /** Start time in HH:mm format (24-hour) */
  startTime: string;

  /** End time in HH:mm format (24-hour) */
  endTime: string;
}

/**
 * Day of week enum values
 */
export type DayOfWeek = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday';

/**
 * Organization policy response from backend
 */
export interface OrgPolicyResponse {
  /** Unique identifier of the policy */
  id: string;

  /** Organization ID */
  orgId: string;

  /** Version number, incremented on each update */
  version: number;

  /** Work hours configuration */
  workHours: WorkHoursPolicy;

  /** List of excluded app executable hashes */
  appExclusions: string[];

  /** Idle threshold in seconds (60-3600) */
  idleThresholdSeconds: number;

  /** Data retention period in days */
  retentionDays: number;

  /** When the policy was created */
  createdAt: string;

  /** When the policy was last updated */
  updatedAt: string | null;
}

/**
 * Request to update organization policy
 */
export interface UpdateOrgPolicyRequest {
  workHours?: {
    timezone?: string;
    days?: DayOfWeek[];
    startTime?: string;
    endTime?: string;
  };
  appExclusions?: string[];
  idleThresholdSeconds?: number;
  retentionDays?: number;
}

// ============================================================================
// LEGACY COMPATIBILITY (for gradual migration)
// ============================================================================

/**
 * @deprecated Use OrgPolicyResponse instead
 * Legacy format for backward compatibility during migration
 */
export interface OrgPolicies {
  workHours: {
    startHour: number;
    endHour: number;
    workDays: number[];
    timezone: string;
  };
  idleThresholdSeconds: number;
  focusMode: {
    enabled: boolean;
    mode: 'pomodoro' | 'ultradian' | null;
  };
}

/**
 * Converts OrgPolicyResponse to legacy OrgPolicies format
 */
export function toLegacyFormat(policy: OrgPolicyResponse): OrgPolicies {
  const dayNameToNumber: Record<DayOfWeek, number> = {
    sunday: 0,
    monday: 1,
    tuesday: 2,
    wednesday: 3,
    thursday: 4,
    friday: 5,
    saturday: 6,
  };

  const parseHour = (time: string): number => {
    const [hours] = time.split(':');
    return parseInt(hours, 10);
  };

  return {
    workHours: {
      startHour: parseHour(policy.workHours.startTime),
      endHour: parseHour(policy.workHours.endTime),
      workDays: policy.workHours.days.map(d => dayNameToNumber[d]),
      timezone: policy.workHours.timezone,
    },
    idleThresholdSeconds: policy.idleThresholdSeconds,
    focusMode: {
      enabled: false,
      mode: null,
    },
  };
}

// ============================================================================
// DEFAULTS
// ============================================================================

export const DEFAULT_LOCAL_SETTINGS: LocalSettings = {
  autoResumeNotificationEnabled: true,
  notificationSoundsEnabled: true,
  language: 'pt-BR',
  updatedAt: new Date().toISOString(),
};

export const DEFAULT_ORG_POLICIES: OrgPolicies = {
  workHours: {
    startHour: 9,
    endHour: 18,
    workDays: [1, 2, 3, 4, 5], // Mon-Fri
    timezone: 'America/Sao_Paulo',
  },
  idleThresholdSeconds: 180, // 3 minutes
  focusMode: {
    enabled: false,
    mode: null,
  },
};

export const DEFAULT_WORK_HOURS: WorkHoursPolicy = {
  timezone: 'America/Sao_Paulo',
  days: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday'],
  startTime: '08:00',
  endTime: '18:00',
};
