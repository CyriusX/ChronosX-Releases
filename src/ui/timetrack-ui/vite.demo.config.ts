import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';

const desktopSrc = path.resolve(__dirname, './src');
const stubsSrc = path.resolve(__dirname, './src/demo/stubs');
const demoPagesSrc = path.resolve(__dirname, './src/demo/pages');

function norm(...parts: string[]): string {
  return path.join(...parts).replace(/\\/g, '/');
}

function overrideForDemo(): Plugin {
  const overrides: Record<string, string> = {
    [norm(desktopSrc, 'hooks/useIpc')]: norm(stubsSrc, 'useIpc.ts'),
    [norm(desktopSrc, 'hooks/useTeamStatus')]: norm(stubsSrc, 'useTeamStatus.ts'),
    [norm(desktopSrc, 'stores/authStore')]: norm(stubsSrc, 'authStore.ts'),
    [norm(desktopSrc, 'services/apiClient')]: norm(stubsSrc, 'apiClient.ts'),
    [norm(desktopSrc, 'services/memberApi')]: norm(stubsSrc, 'memberApi.ts'),
    [norm(desktopSrc, 'services/projectsApi')]: norm(stubsSrc, 'projectsApi.ts'),
    [norm(desktopSrc, 'services/reportApi')]: norm(stubsSrc, 'reportApi.ts'),
    [norm(desktopSrc, 'services/policyApi')]: norm(stubsSrc, 'policyApi.ts'),
    [norm(desktopSrc, 'services/integrationsApi')]: norm(stubsSrc, 'integrationsApi.ts'),
    [norm(desktopSrc, 'lib/runtime')]: norm(stubsSrc, 'runtime.ts'),
    // Avoid external icon fetches (cdn.simpleicons.org) in demo mode
    [norm(desktopSrc, 'components/dashboard/shared/AppIcon')]: norm(stubsSrc, 'AppIcon.tsx'),
    // Hide in-app navigation chrome in LP demo (page layout remains real)
    [norm(desktopSrc, 'components/dashboard')]: norm(stubsSrc, 'dashboardIndex.ts'),
    [norm(desktopSrc, 'components/dashboard/Sidebar')]: norm(stubsSrc, 'Sidebar.tsx'),
    // Focus timer demo: hide the day timeline/calendar strip
    [norm(desktopSrc, 'components/timer/FocusDayTimeline')]: norm(stubsSrc, 'FocusDayTimeline.tsx'),
    // Page-level demo simplifications
    [norm(desktopSrc, 'pages/Timer')]: norm(demoPagesSrc, 'TimerDemo.tsx'),
    [norm(desktopSrc, 'pages/Activities')]: norm(demoPagesSrc, 'ActivitiesDemo.tsx'),
    [norm(desktopSrc, 'pages/Reports')]: norm(demoPagesSrc, 'ReportsDemo.tsx'),
    [norm(desktopSrc, 'pages/Teams')]: norm(demoPagesSrc, 'TeamsDemo.tsx'),
    [norm(desktopSrc, 'pages/ProjectBoard')]: norm(demoPagesSrc, 'ProjectBoardDemo.tsx'),
  };

  return {
    name: 'override-desktop-demo-imports',
    enforce: 'pre',
    resolveId(source, importer) {
      if (!importer) return null;
      if (!source.startsWith('.')) return null;

      const resolved = path.resolve(path.dirname(importer), source).replace(/\\/g, '/');
      const stripped = resolved.replace(/\.(ts|tsx|js|jsx)$/, '');
      const mapped = overrides[stripped];
      return mapped ?? null;
    },
  };
}

export default defineConfig({
  plugins: [overrideForDemo(), react(), tailwindcss()],
  base: './',
  build: {
    outDir: 'dist-demo',
    emptyOutDir: true,
    modulePreload: false,
    rollupOptions: {
      input: {
        index: path.resolve(__dirname, 'demo.html'),
      },
    },
  },
});
