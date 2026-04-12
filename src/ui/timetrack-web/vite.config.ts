import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

const desktopSrc = path.resolve(__dirname, '../timetrack-ui/src')
const webSrc = path.resolve(__dirname, './src')

/**
 * Custom Vite plugin that intercepts imports from desktop code and redirects
 * stores, services, and hooks to the web versions.
 *
 * Why: Desktop components use relative imports like `../../stores/authStore`.
 * Vite's built-in alias only matches raw import specifiers, not resolved paths.
 * This plugin hooks into Vite's `resolveId` to intercept AFTER path resolution.
 */
function overrideDesktopImports(): Plugin {
  // Map desktop file paths → web file paths
  const overrides: Record<string, string> = {
    // Stores
    [norm(desktopSrc, 'stores/authStore')]: norm(webSrc, 'stores/authStore.ts'),
    // Services
    [norm(desktopSrc, 'services/apiClient')]: norm(webSrc, 'services/apiClient.ts'),
    [norm(desktopSrc, 'services/memberApi')]: norm(webSrc, 'services/memberApi.ts'),
    [norm(desktopSrc, 'services/policyApi')]: norm(webSrc, 'services/policyApi.ts'),
    [norm(desktopSrc, 'services/reportApi')]: norm(webSrc, 'services/reportApi.ts'),
    [norm(desktopSrc, 'services/appCategoriesApi')]: norm(webSrc, 'services/appCategoriesApi.ts'),// Hooks
    [norm(desktopSrc, 'hooks/useIpc')]: norm(webSrc, 'hooks/useIpc.ts'),
    [norm(desktopSrc, 'hooks/useLanguage')]: norm(webSrc, 'hooks/useLanguage.ts'),
    [norm(desktopSrc, 'hooks/usePermissions')]: norm(webSrc, 'hooks/usePermissions.ts'),
    [norm(desktopSrc, 'hooks/useMembers')]: norm(webSrc, 'hooks/useMembers.ts'),
    [norm(desktopSrc, 'hooks/useTeamStatus')]: norm(webSrc, 'hooks/useTeamStatus.ts'),
  }

  return {
    name: 'override-desktop-imports',
    enforce: 'pre',
    resolveId(source, importer) {
      if (!importer) return null

      // Only intercept imports originating from desktop code
      const normImporter = importer.replace(/\\/g, '/')
      if (!normImporter.includes('timetrack-ui/src/')) return null

      // Resolve the import relative to the importer
      let resolved: string
      if (source.startsWith('.')) {
        resolved = path.resolve(path.dirname(importer), source).replace(/\\/g, '/')
      } else {
        return null // Only handle relative imports
      }

      // Strip extension for matching
      const stripped = resolved.replace(/\.(ts|tsx|js|jsx)$/, '')

      // Check if this matches any override
      for (const [from, to] of Object.entries(overrides)) {
        if (stripped === from) {
          return to
        }
      }

      return null
    },
  }
}

/** Normalize a path join with forward slashes */
function norm(...parts: string[]): string {
  return path.join(...parts).replace(/\\/g, '/')
}

export default defineConfig({
  plugins: [overrideDesktopImports(), react(), tailwindcss()],
  base: '/',
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
    outDir: 'dist',
  },
})
