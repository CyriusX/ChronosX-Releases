import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: './', // Use relative paths for file:// protocol support
  server: {
    port: 5173,
    strictPort: true, // Fail if port is already in use
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
