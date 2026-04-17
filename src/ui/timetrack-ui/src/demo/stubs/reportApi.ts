import type {
  CategoryDistributionResponse,
  DailySummaryResponse,
  DailySummaryRangeResponse,
  DistractionStatsResponse,
  GroupByOption,
  ProductivityTrendResponse,
  ReportsBundleResponse,
  TopAppsResponse,
  TopFoldersResponse,
  TopPathsResponse,
} from '../../types/reports';

function clamp(n: number, min: number, max: number) {
  return Math.max(min, Math.min(max, n));
}

function eachDay(startDate: string, endDate: string) {
  const start = new Date(startDate + 'T00:00:00');
  const end = new Date(endDate + 'T00:00:00');
  const days: string[] = [];
  const cursor = new Date(start);
  while (cursor <= end) {
    days.push(cursor.toISOString().slice(0, 10));
    cursor.setDate(cursor.getDate() + 1);
  }
  return days;
}

function formatIsoDate(date: string, hour: number, minute: number) {
  const d = new Date(date + 'T00:00:00');
  d.setHours(hour, minute, 0, 0);
  return d.toISOString();
}

export async function getDailyActivities(_date: string, _userId?: string) {
  const date = _date || new Date().toISOString().slice(0, 10);
  const sessions = [
    {
      processName: 'VS Code',
      windowTitle: 'ChronosX TimeTrack',
      appCategory: 'development',
      startedAt: formatIsoDate(date, 9, 5),
      endedAt: formatIsoDate(date, 9, 43),
      durationSeconds: 38 * 60,
    },
    {
      processName: 'Linear',
      windowTitle: 'Sprint board',
      appCategory: 'planning',
      startedAt: formatIsoDate(date, 9, 46),
      endedAt: formatIsoDate(date, 10, 6),
      durationSeconds: 20 * 60,
    },
    {
      processName: 'Chrome',
      windowTitle: 'Docs',
      appCategory: 'browsing',
      startedAt: formatIsoDate(date, 10, 10),
      endedAt: formatIsoDate(date, 10, 30),
      durationSeconds: 20 * 60,
    },
  ];

  return { date, sessions };
}

export async function getDailySummary(date: string, _userId?: string): Promise<DailySummaryResponse> {
  const totalActiveSeconds = 6 * 3600 + 25 * 60;
  const totalIdleSeconds = 48 * 60;
  return {
    date,
    totalActiveSeconds,
    totalIdleSeconds,
    firstActivity: formatIsoDate(date, 9, 5),
    lastActivity: formatIsoDate(date, 17, 40),
    apps: [
      { displayName: 'VS Code', totalSeconds: 2 * 3600 + 12 * 60, sessionCount: 22, appCategory: 'development' },
      { displayName: 'Chrome', totalSeconds: 58 * 60, sessionCount: 18, appCategory: 'browsing' },
      { displayName: 'Linear', totalSeconds: 31 * 60, sessionCount: 7, appCategory: 'planning' },
      { displayName: 'Slack', totalSeconds: 24 * 60, sessionCount: 14, appCategory: 'messaging' },
    ],
  };
}

export async function getDailySummaryRange(startDate: string, endDate: string, _userId?: string): Promise<DailySummaryRangeResponse> {
  const dates = eachDay(startDate, endDate);
  const days = dates.map((date, idx) => {
    const d = new Date(date + 'T00:00:00');
    const day = d.getDay(); // 0..6
    const weekday = day !== 0 && day !== 6;
    const baseActive = weekday ? 7.2 * 3600 : 3.1 * 3600;
    const wobble = (((idx % 7) - 3) * 13 * 60);
    const totalActiveSeconds = Math.max(0, Math.round(baseActive + wobble));
    const totalIdleSeconds = Math.round(totalActiveSeconds * 0.12);
    const productivityRatio = weekday ? 0.62 : 0.54;
    const focusScore = clamp(Math.round((productivityRatio * 100) - (weekday ? 8 : 14)), 0, 100);
    return {
      date,
      totalActiveSeconds,
      totalIdleSeconds,
      productivityRatio,
      focusScore,
    };
  });

  const periodFocusScore = days.length
    ? Math.round(days.reduce((sum, d) => sum + d.focusScore, 0) / days.length)
    : 0;
  const periodBaseProductivity = days.length
    ? Math.round((days.reduce((sum, d) => sum + d.productivityRatio, 0) / days.length) * 100) / 100
    : 0;

  return { days, periodFocusScore, periodBaseProductivity };
}

export async function getTopApps(
  _startDate: string,
  _endDate: string,
  _limit: number = 10,
  _userId?: string
): Promise<TopAppsResponse> {
  return {
    apps: [
      { displayName: 'VS Code', totalSeconds: 2 * 3600 + 12 * 60, sessionCount: 22, productivity: 'productive', subcategory: 'development' },
      { displayName: 'Chrome', totalSeconds: 58 * 60, sessionCount: 18, productivity: 'neutral', subcategory: 'browsing' },
      { displayName: 'Linear', totalSeconds: 31 * 60, sessionCount: 7, productivity: 'productive', subcategory: 'planning' },
      { displayName: 'Slack', totalSeconds: 24 * 60, sessionCount: 14, productivity: 'neutral', subcategory: 'messaging' },
      { displayName: 'Figma', totalSeconds: 18 * 60, sessionCount: 6, productivity: 'productive', subcategory: 'design' },
      { displayName: 'YouTube', totalSeconds: 12 * 60, sessionCount: 4, productivity: 'distraction', subcategory: 'media' },
    ],
  };
}

export async function getProductivityTrend(
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  _userId?: string
): Promise<ProductivityTrendResponse> {
  const dates = eachDay(startDate, endDate);
  const maxPoints = groupBy === 'day' ? 10 : 8;
  const points = dates.slice(-maxPoints);
  return {
    periods: points.map((date, idx) => {
      const base = 6.8 * 3600;
      const wobble = (((idx % 5) - 2) * 16 * 60);
      const total = Math.max(0, Math.round(base + wobble));
      const productiveSeconds = Math.round(total * 0.58);
      const neutralSeconds = Math.round(total * 0.24);
      const distractionSeconds = Math.round(total * 0.12);
      const idleSeconds = Math.max(0, total - productiveSeconds - neutralSeconds - distractionSeconds);
      return {
        period: date.slice(5),
        productiveSeconds,
        neutralSeconds,
        distractionSeconds,
        idleSeconds,
      };
    }),
  };
}

export async function getTopPaths(
  _startDate: string,
  _endDate: string,
  _limit: number = 20,
  _userId?: string
): Promise<TopPathsResponse> {
  return {
    paths: [
      {
        title: 'ChronosX TimeTrack',
        filePath: 'C:\\\\Projects\\\\chronosx-timetrack',
        path: 'C:\\\\Projects\\\\chronosx-timetrack',
        sourceApp: 'VS Code',
        totalSeconds: 62 * 60,
        visitCount: 18,
      },
      {
        title: 'Sprint planning',
        path: 'https://linear.app/demo/team/sprint',
        sourceApp: 'Chrome',
        totalSeconds: 21 * 60,
        visitCount: 9,
      },
    ],
  };
}

export async function getTopFolders(
  _startDate: string,
  _endDate: string,
  _limit: number = 20,
  _userId?: string
): Promise<TopFoldersResponse> {
  return {
    folders: [
      { folderPath: 'C:\\\\Projects\\\\chronosx-timetrack', totalSeconds: 62 * 60, visitCount: 18 },
      { folderPath: 'C:\\\\Projects\\\\client-ops', totalSeconds: 41 * 60, visitCount: 9 },
      { folderPath: 'C:\\\\Users\\\\Demo\\\\Documents', totalSeconds: 22 * 60, visitCount: 6 },
    ],
  };
}

export async function getDistractionStats(
  startDate: string,
  endDate: string,
  _userId?: string
): Promise<DistractionStatsResponse> {
  const dates = eachDay(startDate, endDate).slice(-14);
  return {
    dailyDistractions: dates.map((date, idx) => ({
      date,
      distractionSeconds: clamp(10 * 60 + (idx % 4) * 6 * 60, 0, 90 * 60),
    })),
    topDistractions: [
      { displayName: 'YouTube', processName: 'YouTube', totalSeconds: 42 * 60, sessionCount: 5, subcategory: 'media' },
      { displayName: 'X', processName: 'X', totalSeconds: 28 * 60, sessionCount: 7, subcategory: 'social' },
    ],
  };
}

export async function getCategoryDistribution(
  _startDate: string,
  _endDate: string,
  _userId?: string
): Promise<CategoryDistributionResponse> {
  return {
    categories: [
      {
        category: 'Development',
        totalSeconds: 3 * 3600 + 20 * 60,
        percentage: 52,
        subcategories: [
          { name: 'Coding', totalSeconds: 2 * 3600 + 10 * 60, percentage: 65 },
          { name: 'Review', totalSeconds: 1 * 3600 + 10 * 60, percentage: 35 },
        ],
      },
      {
        category: 'Planning',
        totalSeconds: 1 * 3600 + 5 * 60,
        percentage: 18,
        subcategories: [{ name: 'Issues', totalSeconds: 1 * 3600 + 5 * 60, percentage: 100 }],
      },
      {
        category: 'Communication',
        totalSeconds: 52 * 60,
        percentage: 14,
        subcategories: [{ name: 'Chat', totalSeconds: 52 * 60, percentage: 100 }],
      },
      {
        category: 'Other',
        totalSeconds: 58 * 60,
        percentage: 16,
        subcategories: [{ name: 'Browsing', totalSeconds: 58 * 60, percentage: 100 }],
      },
    ],
  };
}

export async function getReportsBundle(
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  limits: { topApps?: number; topPaths?: number; topFolders?: number } = {},
  userId?: string
): Promise<ReportsBundleResponse> {
  const [dailySummaryRange, productivityTrend, topApps, topPaths, topFolders, distractionStats, categoryDistribution] =
    await Promise.all([
      getDailySummaryRange(startDate, endDate, userId),
      getProductivityTrend(startDate, endDate, groupBy, userId),
      getTopApps(startDate, endDate, limits.topApps ?? 20, userId),
      getTopPaths(startDate, endDate, limits.topPaths ?? 20, userId),
      getTopFolders(startDate, endDate, limits.topFolders ?? 20, userId),
      getDistractionStats(startDate, endDate, userId),
      getCategoryDistribution(startDate, endDate, userId),
    ]);

  return {
    dailySummaryRange,
    productivityTrend,
    topApps: {
      apps: (topApps.apps ?? []).slice(0, Math.max(1, limits.topApps ?? 20)),
    },
    topPaths: {
      paths: (topPaths.paths ?? []).slice(0, Math.max(1, limits.topPaths ?? 20)),
    },
    topFolders: {
      folders: (topFolders.folders ?? []).slice(0, Math.max(1, limits.topFolders ?? 20)),
    } as TopFoldersResponse,
    distractionStats,
    categoryDistribution,
  };
}

