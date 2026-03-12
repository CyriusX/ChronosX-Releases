/**
 * Types for Local Settings
 *
 * SOLID:
 * - ISP: Interfaces segregadas por responsabilidade
 * - DIP: Contratos para comunicação com backend
 */

// ============================================================================
// SETTINGS TYPES
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

/**
 * Políticas da organização (read-only para o colaborador)
 */
export interface OrgPolicies {
  /** Horário de trabalho configurado pelo Admin */
  workHours: {
    startHour: number; // 0-23
    endHour: number;   // 0-23
    workDays: number[]; // 0=Sunday, 6=Saturday
    timezone: string;
  };

  /** Threshold de idle configurado (segundos) */
  idleThresholdSeconds: number;

  /** Modo de foco ativo */
  focusMode: {
    enabled: boolean;
    mode: 'pomodoro' | 'ultradian' | null;
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
