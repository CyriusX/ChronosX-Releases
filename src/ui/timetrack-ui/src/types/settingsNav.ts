import type { LucideIcon } from 'lucide-react';

export type SettingsSection =
  | 'profile'
  | 'general'
  | 'notifications'
  | 'timeline'
  | 'focus-timer'
  | 'integrations'
  | 'team'
  | 'organization'
  | 'weekly-report'
  | 'ai-classifications'
  | 'billing'
  | 'maintenance'
  | 'agent-status'
  | 'about';

export type SettingsGroup = 'personal' | 'management' | 'system';

export interface SettingsSectionDef {
  id: SettingsSection;
  label: string;
  icon: LucideIcon;
  group: SettingsGroup;
  requiresPermission?: 'canManageTeam' | 'canViewOrgPolicies';
}
