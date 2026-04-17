import type {
  AppProductivityCategory,
  ApplicationSummary,
  CategorySummary,
  TodaySummaryResponse,
  WeeklyHistoryItem,
} from '../types/ipc';
import type { TeamMemberStatus, MemberSummaryResponse } from '../types/member';
import type { ProjectItem, Task, NotificationItem } from '../services/projectsApi';

function pad2(n: number) {
  return String(n).padStart(2, '0');
}

function isoDate(d: Date) {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

function dayShort(d: Date) {
  return d.toLocaleDateString(undefined, { weekday: 'short' });
}

export const demoTodaySummary: TodaySummaryResponse = (() => {
  const totalDuration = 4 * 3600 + 18 * 60; // 4h18m
  const productiveTime = 3 * 3600 + 12 * 60; // 3h12m
  const idleTime = 18 * 60; // 18m
  const focusTime = 2 * 3600 + 5 * 60; // 2h05m

  const categories: CategorySummary[] = [
    { name: 'Development', duration: 2 * 3600 + 22 * 60, percentage: 55, color: '#05df72', productivity: 'productive' as AppProductivityCategory, subcategory: 'development' },
    { name: 'Meetings', duration: 48 * 60, percentage: 19, color: '#8B5CF6', productivity: 'neutral' as AppProductivityCategory, subcategory: 'meetings' },
    { name: 'Research', duration: 32 * 60, percentage: 13, color: '#22D3EE', productivity: 'productive' as AppProductivityCategory, subcategory: 'research' },
    { name: 'Messaging', duration: 36 * 60, percentage: 13, color: '#f59e0b', productivity: 'neutral' as AppProductivityCategory, subcategory: 'communication' },
  ];

  const topApplications: ApplicationSummary[] = [
    { name: 'VS Code', duration: 2 * 3600 + 1 * 60, percentage: 46, productivity: 'productive' as AppProductivityCategory, subcategory: 'development' },
    { name: 'Chrome', duration: 58 * 60, percentage: 22, productivity: 'neutral' as AppProductivityCategory, subcategory: 'browsing' },
    { name: 'Linear', duration: 31 * 60, percentage: 12, productivity: 'productive' as AppProductivityCategory, subcategory: 'planning' },
    { name: 'Slack', duration: 24 * 60, percentage: 9, productivity: 'neutral' as AppProductivityCategory, subcategory: 'communication' },
  ];

  const topProjects = [
    { name: 'ChronosX TimeTrack', duration: 2 * 3600 + 18 * 60, percentage: 53 },
    { name: 'Client Ops', duration: 56 * 60, percentage: 21 },
    { name: 'Internal', duration: 44 * 60, percentage: 16 },
  ];

  const weeklyHistory: WeeklyHistoryItem[] = Array.from({ length: 7 }).map((_, i) => {
    const d = new Date();
    d.setDate(d.getDate() - (6 - i));
    const hours = [6.7, 7.4, 6.1, 8.0, 7.2, 3.1, 4.3][i] ?? 6.5;
    return {
      date: isoDate(d),
      dayName: dayShort(d),
      hours,
      isToday: i === 6,
    };
  });

  return {
    totalDuration,
    productiveTime,
    idleTime,
    focusTime,
    focusScore: 78,
    sessionsCount: 9,
    topProjects,
    topApplications,
    categories,
    weeklyHistory,
  };
})();

export const demoProjectsForIpc = [
  { id: 'p-chronos', name: 'ChronosX TimeTrack', color: '#8B5CF6', client: 'Internal' },
  { id: 'p-client', name: 'Client Ops', color: '#22D3EE', client: 'Client' },
];

export const demoFocusActivities = (() => {
  const now = new Date();
  const base = new Date(now);
  base.setHours(Math.max(9, now.getHours() - 3), 10, 0, 0);

  const blocks = [
    { name: 'VS Code', subcategory: 'development', color: '#05df72', minutes: 38 },
    { name: 'Linear', subcategory: 'planning', color: '#8B5CF6', minutes: 18 },
    { name: 'Chrome', subcategory: 'browsing', color: '#22D3EE', minutes: 22 },
    { name: 'Slack', subcategory: 'communication', color: '#f59e0b', minutes: 12 },
  ];

  let cursor = base.getTime();
  return blocks.map((b) => {
    const start = new Date(cursor);
    const end = new Date(cursor + b.minutes * 60_000);
    cursor = end.getTime() + 4 * 60_000;
    return {
      // FocusDayTimeline expects these fields
      name: b.name,
      startUtc: start.toISOString(),
      endUtc: end.toISOString(),
      duration: b.minutes * 60,
      subcategory: b.subcategory,
      color: b.color,
      // Older dashboards sometimes expect processName/windowTitle too
      processName: b.name,
      windowTitle: b.subcategory,
      exePath: '',
      id: `${b.name}-${start.toISOString()}`,
      startedAt: start.toISOString(),
      endedAt: end.toISOString(),
    };
  });
})();

export const demoLinearProject: ProjectItem = {
  id: 'linear-demo',
  name: 'LP Demo (Linear)',
  description: 'A demo project synced from Linear (mock data).',
  color: '#a78bfa',
  status: 'Active',
  createdAt: new Date(Date.now() - 14 * 86400_000).toISOString(),
  updatedAt: new Date().toISOString(),
  isBillable: false,
  currency: null,
  hourlyRate: null,
  syncSource: 'Linear',
  linearProjectId: 'lin_prj_demo',
  lastSyncedAt: new Date(Date.now() - 25 * 60_000).toISOString(),
};

export const demoLinearTasks: Task[] = [
  {
    id: 't-1',
    projectId: demoLinearProject.id,
    projectName: demoLinearProject.name,
    projectColor: demoLinearProject.color,
    title: 'Implement waitlist API route',
    description: 'Capture lead email + audience + language.',
    status: 'InProgress',
    createdByUserId: 'u-demo',
    assignedUserId: 'u-demo',
    assignedUserDisplayName: 'Demo User',
    priority: 'High',
    dueDate: null,
    position: 1024,
    createdAt: new Date(Date.now() - 3 * 86400_000).toISOString(),
    updatedAt: new Date(Date.now() - 2 * 3600_000).toISOString(),
    movedToInProgressAt: new Date(Date.now() - 6 * 3600_000).toISOString(),
    completedAt: null,
    totalSecondsWorked: 3 * 3600 + 22 * 60,
    rowVersion: 1,
    isRunning: true,
    runningSeconds: 12 * 60,
    isLinearSourced: true,
    linearIssueIdentifier: 'TT-128',
    linearUrl: 'https://linear.app/',
    linearStateName: 'In Progress',
  },
  {
    id: 't-2',
    projectId: demoLinearProject.id,
    projectName: demoLinearProject.name,
    projectColor: demoLinearProject.color,
    title: 'Polish dashboard cards',
    description: null,
    status: 'Todo',
    createdByUserId: 'u-demo',
    assignedUserId: 'u-demo',
    assignedUserDisplayName: 'Demo User',
    priority: 'Medium',
    dueDate: null,
    position: 2048,
    createdAt: new Date(Date.now() - 2 * 86400_000).toISOString(),
    updatedAt: null,
    movedToInProgressAt: null,
    completedAt: null,
    totalSecondsWorked: 0,
    rowVersion: 1,
    isRunning: false,
    runningSeconds: null,
    isLinearSourced: true,
    linearIssueIdentifier: 'TT-133',
    linearUrl: 'https://linear.app/',
    linearStateName: 'Todo',
  },
  {
    id: 't-3',
    projectId: demoLinearProject.id,
    projectName: demoLinearProject.name,
    projectColor: demoLinearProject.color,
    title: 'Release v0.1 demo',
    description: null,
    status: 'InReview',
    createdByUserId: 'u-demo',
    assignedUserId: 'u-demo',
    assignedUserDisplayName: 'Demo User',
    priority: 'Low',
    dueDate: null,
    position: 3072,
    createdAt: new Date(Date.now() - 5 * 86400_000).toISOString(),
    updatedAt: null,
    movedToInProgressAt: new Date(Date.now() - 3 * 86400_000).toISOString(),
    completedAt: null,
    totalSecondsWorked: 52 * 60,
    rowVersion: 1,
    isRunning: false,
    runningSeconds: null,
    isLinearSourced: true,
    linearIssueIdentifier: 'TT-140',
    linearUrl: 'https://linear.app/',
    linearStateName: 'In Review',
  },
  {
    id: 't-4',
    projectId: demoLinearProject.id,
    projectName: demoLinearProject.name,
    projectColor: demoLinearProject.color,
    title: 'Ship landing page refresh',
    description: null,
    status: 'Done',
    createdByUserId: 'u-demo',
    assignedUserId: 'u-demo',
    assignedUserDisplayName: 'Demo User',
    priority: 'None',
    dueDate: null,
    position: 4096,
    createdAt: new Date(Date.now() - 8 * 86400_000).toISOString(),
    updatedAt: new Date(Date.now() - 4 * 86400_000).toISOString(),
    movedToInProgressAt: new Date(Date.now() - 7 * 86400_000).toISOString(),
    completedAt: new Date(Date.now() - 4 * 86400_000).toISOString(),
    totalSecondsWorked: 5 * 3600 + 12 * 60,
    rowVersion: 1,
    isRunning: false,
    runningSeconds: null,
    isLinearSourced: true,
    linearIssueIdentifier: 'TT-101',
    linearUrl: 'https://linear.app/',
    linearStateName: 'Done',
  },
];

export const demoTeamMembers: TeamMemberStatus[] = [
  {
    userId: 'u-demo',
    displayName: 'Demo User',
    role: 'Gestor',
    status: 'Active',
    todayDurationSeconds: demoTodaySummary.totalDuration,
    todayDurationFormatted: '4h 18m',
    isTracking: true,
    lastSyncAt: new Date(Date.now() - 6 * 60_000).toISOString(),
    productivityRatio: 0.74,
  } as any,
  {
    userId: 'u-ana',
    displayName: 'Ana Silva',
    role: 'Colaborador',
    status: 'Active',
    todayDurationSeconds: 3 * 3600 + 2 * 60,
    todayDurationFormatted: '3h 02m',
    isTracking: true,
    lastSyncAt: new Date(Date.now() - 2 * 60_000).toISOString(),
    productivityRatio: 0.68,
  } as any,
  {
    userId: 'u-bruno',
    displayName: 'Bruno Lima',
    role: 'Colaborador',
    status: 'Active',
    todayDurationSeconds: 2 * 3600 + 18 * 60,
    todayDurationFormatted: '2h 18m',
    isTracking: false,
    lastSyncAt: new Date(Date.now() - 24 * 60_000).toISOString(),
    productivityRatio: 0.56,
  } as any,
];

export const demoMemberSummaryByUserId: Record<string, MemberSummaryResponse> = {
  'u-demo': {
    totalDuration: demoTodaySummary.totalDuration,
    productiveTime: demoTodaySummary.productiveTime,
    idleTime: demoTodaySummary.idleTime,
    focusTime: demoTodaySummary.focusTime,
    focusScore: demoTodaySummary.focusScore,
    sessionsCount: demoTodaySummary.sessionsCount,
    topProjects: demoTodaySummary.topProjects,
    topApplications: demoTodaySummary.topApplications,
    categories: demoTodaySummary.categories,
    weeklyHistory: demoTodaySummary.weeklyHistory,
  } as any,
  'u-ana': {
    totalDuration: 3 * 3600 + 2 * 60,
    productiveTime: 2 * 3600 + 10 * 60,
    idleTime: 12 * 60,
    focusTime: 86 * 60,
    focusScore: 73,
    sessionsCount: 8,
    topProjects: demoTodaySummary.topProjects,
    topApplications: demoTodaySummary.topApplications,
    categories: demoTodaySummary.categories,
    weeklyHistory: demoTodaySummary.weeklyHistory,
  } as any,
  'u-bruno': {
    totalDuration: 2 * 3600 + 18 * 60,
    productiveTime: 1 * 3600 + 22 * 60,
    idleTime: 24 * 60,
    focusTime: 52 * 60,
    focusScore: 58,
    sessionsCount: 6,
    topProjects: demoTodaySummary.topProjects,
    topApplications: demoTodaySummary.topApplications,
    categories: demoTodaySummary.categories,
    weeklyHistory: demoTodaySummary.weeklyHistory,
  } as any,
};

export const demoNotifications: NotificationItem[] = [
  {
    id: 'n-1',
    kind: 'TaskAssigned',
    title: 'Task assigned',
    body: '“Implement waitlist API route” is now assigned to you.',
    metadataJson: null,
    createdAt: new Date(Date.now() - 42 * 60_000).toISOString(),
    readAt: null,
  },
  {
    id: 'n-2',
    kind: 'DeadlineToday',
    title: 'Due today',
    body: '“Polish dashboard cards” is due today.',
    metadataJson: null,
    createdAt: new Date(Date.now() - 3 * 3600_000).toISOString(),
    readAt: new Date(Date.now() - 2 * 3600_000).toISOString(),
  },
];
