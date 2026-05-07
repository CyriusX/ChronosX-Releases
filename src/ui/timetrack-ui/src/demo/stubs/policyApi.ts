import type { OrgPolicyResponse } from '../../types/settings';

export async function getOrgPolicy(_orgId: string): Promise<OrgPolicyResponse> {
  return {
    focusMode: {
      enabled: true,
      defaultMode: 'Pomodoro',
      allowUserOverride: true,
      pomodoro: {
        focusMinutes: 25,
        shortBreakMinutes: 5,
        longBreakMinutes: 15,
        cyclesBeforeLongBreak: 4,
      },
      ultradian: {
        focusMinutes: 90,
        shortBreakMinutes: 20,
        wavesPerSession: 3,
      },
    },
  } as any;
}

export async function updateOrgPolicy(_orgId: string, request: any): Promise<OrgPolicyResponse> {
  return {
    focusMode: request?.focusMode ?? null,
  } as any;
}

