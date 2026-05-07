import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';

const desktopSrc = path.resolve(__dirname, '../timetrack-ui/src');
const webSrc = path.resolve(__dirname, './src');

function norm(...parts: string[]): string {
  return path.join(...parts).replace(/\\/g, '/');
}

function overrideForDemo(): Plugin {
  const overrides: Record<string, string> = {
    // Web app stubs
    [norm(webSrc, 'stores/authStore')]: norm(webSrc, 'demo/stubs/authStore.ts'),
    [norm(webSrc, 'hooks/useTeamStatus')]: norm(webSrc, 'demo/stubs/useTeamStatus.ts'),
    // Hide portal navigation chrome in LP demo
    [norm(webSrc, 'components/WebSidebar')]: norm(webSrc, 'demo/stubs/WebSidebar.tsx'),

    // Desktop service stubs (used by @desktop components like MemberCard/Drawer)
    [norm(desktopSrc, 'services/apiClient')]: norm(desktopSrc, 'demo/stubs/apiClient.ts'),
    [norm(desktopSrc, 'services/memberApi')]: norm(desktopSrc, 'demo/stubs/memberApi.ts'),
    [norm(desktopSrc, 'services/projectsApi')]: norm(desktopSrc, 'demo/stubs/projectsApi.ts'),
    [norm(desktopSrc, 'services/reportApi')]: norm(desktopSrc, 'demo/stubs/reportApi.ts'),
    // Avoid external icon fetches (cdn.simpleicons.org) in portal demo too
    [norm(desktopSrc, 'components/dashboard/shared/AppIcon')]: norm(desktopSrc, 'demo/stubs/AppIcon.tsx'),
  };

  return {
    name: 'override-web-demo-imports',
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
  server: {
    port: 5174,
    strictPort: true,
  },
  resolve: {
    alias: {
      '@': webSrc,
      '@desktop': desktopSrc,
    },
    dedupe: ['react', 'react-dom', 'react-router-dom', 'i18next', 'react-i18next'],
  },
  build: {
    outDir: 'dist-demo',
    emptyOutDir: true,
    rollupOptions: {
      input: {
        index: path.resolve(__dirname, 'demo.html'),
      },
    },
  },
});
