// Runtime configuration - this file can be customized at deployment time
window.__APP_CONFIG__ = {
  // API URL - can be overridden via environment variable at runtime
  VITE_API_URL: window.__ENV__?.VITE_API_URL || 'http://localhost:5001/api/v1'
};
