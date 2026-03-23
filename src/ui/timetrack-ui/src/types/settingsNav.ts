import type { LucideIcon } from 'lucide-react';

export type SettingsSection =
  | 'profile'
  | 'general'
  | 'notifications'
  | 'focus-timer'
  | 'team'
  | 'organization'
  | 'about';

export type SettingsGroup = 'personal' | 'management' | 'system';

export interface SettingsSectionDef {
  id: SettingsSection;
  label: string;
  icon: LucideIcon;
  group: SettingsGroup;
  requiresPermission?: 'canManageTeam' | 'canViewOrgPolicies';
}
