import { useState, useCallback } from 'react';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest, DayOfWeek, FocusModePolicy } from '../../types/settings';

interface WorkHoursState {
  startTime: string;
  endTime: string;
  days: DayOfWeek[];
}

interface UsePolicyCardsState {
  editingCard: string | null;
  isSaving: boolean;
  workHours: WorkHoursState;
  idleThresholdMinutes: number;
  idleJustificationPromptThresholdMinutes: number | null;
  retentionDays: number;
  focusMode: FocusModePolicy;
}

interface UsePolicyCardsReturn extends UsePolicyCardsState {
  setEditingCard: (card: string | null) => void;
  setWorkHours: (value: WorkHoursState | ((prev: WorkHoursState) => WorkHoursState)) => void;
  setIdleThresholdMinutes: (value: number) => void;
  setIdleJustificationPromptThresholdMinutes: (value: number | null) => void;
  setRetentionDays: (value: number) => void;
  setFocusMode: (value: FocusModePolicy | ((prev: FocusModePolicy) => FocusModePolicy)) => void;
  handleToggleDay: (day: DayOfWeek) => void;
  handleSave: (cardType: string, onUpdate?: (request: UpdateOrgPolicyRequest) => Promise<void>) => Promise<void>;
  handleCancel: (cardType: string) => void;
  resetToPolicy: (policy: OrgPolicyResponse) => void;
}

/**
 * usePolicyCards - Hook para gerenciar estado dos cards de política
 *
 * SRP: Apenas gerencia estado e handlers dos cards de política
 */
export function usePolicyCards(policy: OrgPolicyResponse): UsePolicyCardsReturn {
  const [editingCard, setEditingCard] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const [workHours, setWorkHours] = useState({
    startTime: policy.workHours.startTime,
    endTime: policy.workHours.endTime,
    days: policy.workHours.days,
  });

  const [idleThresholdMinutes, setIdleThresholdMinutes] = useState(
    Math.floor(policy.idleThresholdSeconds / 60)
  );
  const [idleJustificationPromptThresholdMinutes, setIdleJustificationPromptThresholdMinutes] = useState<number | null>(
    policy.idleJustificationPromptThresholdSeconds == null
      ? null
      : Math.floor(policy.idleJustificationPromptThresholdSeconds / 60)
  );

  const [retentionDays, setRetentionDays] = useState(policy.retentionDays);

  const [focusMode, setFocusMode] = useState<FocusModePolicy>(
    policy.focusMode || {
      enabled: false,
      mode: 'none',
      allowUserOverride: true,
    }
  );

  const handleToggleDay = useCallback((day: DayOfWeek) => {
    setWorkHours((prev) => ({
      ...prev,
      days: prev.days.includes(day)
        ? prev.days.filter((d) => d !== day)
        : [...prev.days, day],
    }));
  }, []);

  const handleSave = useCallback(
    async (cardType: string, onUpdate?: (request: UpdateOrgPolicyRequest) => Promise<void>) => {
      if (!onUpdate) return;

      setIsSaving(true);
      try {
        let request: UpdateOrgPolicyRequest = {};

        switch (cardType) {
          case 'workHours':
            request = {
              workHours: {
                startTime: workHours.startTime,
                endTime: workHours.endTime,
                days: workHours.days,
              },
            };
            break;
          case 'idleThreshold':
            request = {
              idleThresholdSeconds: idleThresholdMinutes * 60,
            };
            break;
          case 'idleJustificationThreshold':
            request = {
              idleJustificationPromptThresholdSeconds: idleJustificationPromptThresholdMinutes == null
                ? null
                : idleJustificationPromptThresholdMinutes * 60,
            };
            break;
          case 'retention':
            request = {
              retentionDays: retentionDays,
            };
            break;
          case 'focusMode':
            request = {
              focusMode: {
                enabled: focusMode.enabled,
                mode: focusMode.mode,
                allowUserOverride: focusMode.allowUserOverride,
                pomodoro: focusMode.pomodoro,
                ultradian: focusMode.ultradian,
              },
            };
            break;
        }

        await onUpdate(request);
        setEditingCard(null);
      } catch (error) {
        console.error('Failed to update policy:', error);
      } finally {
        setIsSaving(false);
      }
    },
    [workHours, idleThresholdMinutes, idleJustificationPromptThresholdMinutes, retentionDays, focusMode]
  );

  const handleCancel = useCallback(
    (cardType: string) => {
      switch (cardType) {
        case 'workHours':
          setWorkHours({
            startTime: policy.workHours.startTime,
            endTime: policy.workHours.endTime,
            days: policy.workHours.days,
          });
          break;
        case 'idleThreshold':
          setIdleThresholdMinutes(Math.floor(policy.idleThresholdSeconds / 60));
          break;
        case 'idleJustificationThreshold':
          setIdleJustificationPromptThresholdMinutes(
            policy.idleJustificationPromptThresholdSeconds == null
              ? null
              : Math.floor(policy.idleJustificationPromptThresholdSeconds / 60)
          );
          break;
        case 'retention':
          setRetentionDays(policy.retentionDays);
          break;
        case 'focusMode':
          setFocusMode(
            policy.focusMode || {
              enabled: false,
              mode: 'none',
              allowUserOverride: true,
            }
          );
          break;
      }
      setEditingCard(null);
    },
    [policy]
  );

  const resetToPolicy = useCallback((newPolicy: OrgPolicyResponse) => {
    setWorkHours({
      startTime: newPolicy.workHours.startTime,
      endTime: newPolicy.workHours.endTime,
      days: newPolicy.workHours.days,
    });
    setIdleThresholdMinutes(Math.floor(newPolicy.idleThresholdSeconds / 60));
    setIdleJustificationPromptThresholdMinutes(
      newPolicy.idleJustificationPromptThresholdSeconds == null
        ? null
        : Math.floor(newPolicy.idleJustificationPromptThresholdSeconds / 60)
    );
    setRetentionDays(newPolicy.retentionDays);
    setFocusMode(
      newPolicy.focusMode || {
        enabled: false,
        mode: 'none',
        allowUserOverride: true,
      }
    );
    setEditingCard(null);
  }, []);

  return {
    editingCard,
    isSaving,
    workHours,
    idleThresholdMinutes,
    idleJustificationPromptThresholdMinutes,
    retentionDays,
    focusMode,
    setEditingCard,
    setWorkHours,
    setIdleThresholdMinutes,
    setIdleJustificationPromptThresholdMinutes,
    setRetentionDays,
    setFocusMode,
    handleToggleDay,
    handleSave,
    handleCancel,
    resetToPolicy,
  };
}
