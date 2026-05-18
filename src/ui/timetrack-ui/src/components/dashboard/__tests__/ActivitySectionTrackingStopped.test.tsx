import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render } from '@testing-library/react';
import { ActivitySection } from '../ActivitySection';
import { useTrackingStore } from '../../../stores/trackingStore';

vi.mock('../../../hooks/useIpc', () => ({
  useIpc: () => ({
    sendQuery: vi.fn(async () => ({ success: true, data: { activities: [] } })),
    subscribeToEvent: () => () => {},
    isConnected: true,
  }),
}));

vi.mock('../../../stores/hiddenAppsStore', () => ({
  useHiddenAppsStore: (selector: any) => selector({ hiddenApps: [] }),
}));

vi.mock('../../../services/reportApi', () => ({
  getDailyActivities: vi.fn(async () => ({ sessions: [], idlePeriods: [] })),
}));

vi.mock('../../../services/projectsApi', () => ({
  getMyTaskEntries: vi.fn(async () => ({ entries: [] })),
  getUserTaskEntries: vi.fn(async () => ({ entries: [] })),
  closeMyOpenTaskTimer: vi.fn(async () => {}),
}));

describe('ActivitySection', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-17T12:00:00Z'));
    useTrackingStore.setState({ isTracking: false, isPaused: false });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('stretches the latest "Tracking Stopped" placeholder while stopped', () => {
    const activities = [
      {
        id: 'ts-1',
        kind: 'activity',
        name: 'Tracking Stopped',
        startUtc: '2026-05-17T11:55:00.000Z',
        endUtc: '2026-05-17T11:55:01.000Z',
        duration: 1,
        productivity: 'neutral',
        subcategory: 'system_event',
        color: '#f87171',
        tabs: [],
      },
    ];

    const { container } = render(<ActivitySection activities={activities as any} />);

    const stoppedBlock = container.querySelector('.opacity-60') as HTMLElement | null;
    expect(stoppedBlock).toBeTruthy();

    const widthPct = parseFloat((stoppedBlock!.style.width || '0').replace('%', ''));
    expect(widthPct).toBeGreaterThan(0.15);
  });
});
