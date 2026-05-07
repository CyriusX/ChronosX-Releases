/**
 * Types for Local Settings and Organization Policies
 *
 * SOLID:
 * - ISP: Interfaces segregadas por responsabilidade
 * - DIP: Contratos para comunicação com backend
 */

// ============================================================================
// LANGUAGE
// ============================================================================

export type AppLanguage = 'pt-BR' | 'en-US' | 'fr-FR' | 'es-ES';

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

  /** Idioma da interface */
  language: AppLanguage;

  /** Limiar de inatividade em segundos (60–3600). null = padrão do Agent (300s) */
  idleThresholdSeconds: number | null;

  /** Meta diária de trabalho em segundos (1800–86400). null = padrão 28800 (8h) */
  workGoalSeconds: number | null;

  /** Whether DevTools is enabled for the desktop WebView host (admin-controlled). */
  devToolsEnabled: boolean;

  /** Optional UTC expiry for DevTools access. */
  devToolsEnabledUntilUtc: string | null;

  /** Timestamp da última atualização */
  updatedAt: string;
}

/**
 * Request para atualização parcial das configurações
 */
export interface UpdateLocalSettingsRequest {
  autoResumeNotificationEnabled?: boolean;
  notificationSoundsEnabled?: boolean;
  language?: AppLanguage;
  idleThresholdSeconds?: number;
  workGoalSeconds?: number;
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

// ============================================================================
// FOCUS MODE POLICY (Pomodoro / Ultradian)
// ============================================================================

/**
 * Pomodoro technique configuration
 */
export interface PomodoroConfig {
  /** Duration of focus blocks in minutes (10-180) */
  focusMinutes: number;

  /** Duration of short breaks in minutes (5-60) */
  shortBreakMinutes: number;

  /** Duration of long breaks in minutes (5-60) */
  longBreakMinutes: number;

  /** Number of cycles before a long break (2-8) */
  cyclesBeforeLongBreak: number;
}

/**
 * Ultradian rhythm configuration
 */
export interface UltradianConfig {
  /** Duration of focus blocks in minutes (10-180) */
  focusMinutes: number;

  /** Duration of recovery breaks in minutes (5-60) */
  breakMinutes: number;
}

/**
 * Focus mode configuration
 */
export interface FocusModePolicy {
  /** Whether focus mode is enabled for the organization */
  enabled: boolean;

  /** Focus mode type: "pomodoro", "ultradian", or "none" */
  mode: 'pomodoro' | 'ultradian' | 'none';

  /** Whether users can manually start/stop focus cycles */
  allowUserOverride: boolean;

  /** Pomodoro-specific configuration */
  pomodoro?: PomodoroConfig;

  /** Ultradian-specific configuration */
  ultradian?: UltradianConfig;
}

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

  /** Prompt threshold for optional idle justifications. null = disabled */
  idleJustificationPromptThresholdSeconds: number | null;

  /** Data retention period in days */
  retentionDays: number;

  /** Focus mode configuration (Pomodoro / Ultradian) */
  focusMode: FocusModePolicy;

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
  idleJustificationPromptThresholdSeconds?: number | null;
  retentionDays?: number;
  focusMode?: {
    enabled?: boolean;
    mode?: 'pomodoro' | 'ultradian' | 'none';
    allowUserOverride?: boolean;
    pomodoro?: {
      focusMinutes?: number;
      shortBreakMinutes?: number;
      longBreakMinutes?: number;
      cyclesBeforeLongBreak?: number;
    };
    ultradian?: {
      focusMinutes?: number;
      breakMinutes?: number;
    };
  };
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
      enabled: policy.focusMode?.enabled ?? false,
      mode: policy.focusMode?.mode === 'none' || !policy.focusMode?.enabled
        ? null
        : (policy.focusMode?.mode ?? null),
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
  idleThresholdSeconds: null,
  workGoalSeconds: null,
  devToolsEnabled: false,
  devToolsEnabledUntilUtc: null,
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

export const DEFAULT_FOCUS_MODE: FocusModePolicy = {
  enabled: false,
  mode: 'none',
  allowUserOverride: true,
  pomodoro: {
    focusMinutes: 25,
    shortBreakMinutes: 5,
    longBreakMinutes: 15,
    cyclesBeforeLongBreak: 4,
  },
  ultradian: {
    focusMinutes: 90,
    breakMinutes: 20,
  },
};
