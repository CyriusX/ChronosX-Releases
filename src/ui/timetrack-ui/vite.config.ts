import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

/**
 * Fixes script tags for IIFE bundles loaded via file:// in WebView2:
 * 1. Strips `type="module"` — module scripts are blocked over file://
 * 2. Adds `defer` — without type="module" (which defers by default), the script
 *    executes before <div id="root"> is parsed, so React can't mount.
 */
function fixScriptTags(): Plugin {
  return {
    name: 'fix-script-tags',
    enforce: 'post',
    transformIndexHtml(html) {
      return html.replace(/<script type="module" crossorigin/g, '<script defer')
    },
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss(), fixScriptTags()],
  base: './', // Use relative paths for file:// protocol support
  server: {
    port: 5173,
    strictPort: true, // Fail if port is already in use
  },
  // Pin the dep scan to the real entry; otherwise Vite discovers stale
  // gitignored build artifacts (e.g. dist-demo/assets/*.js) and fails to
  // resolve their embedded dependencies.
  optimizeDeps: {
    entries: ['index.html'],
  },
  build: {
    outDir: 'dist',
    modulePreload: false,
    rollupOptions: {
      output: {
        format: 'iife', // Use IIFE for file:// protocol compatibility
      },
    },
  },
})
