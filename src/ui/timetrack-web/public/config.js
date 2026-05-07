// Runtime configuration - this file can be customized at deployment time
// The __VITE_API_URL__ placeholder is replaced by config-loader.sh at container startup
window.__APP_CONFIG__ = {
  VITE_API_URL: '__VITE_API_URL__' !== '__VITE_API_URL__'
    ? '__VITE_API_URL__'
    : (window.__ENV__?.VITE_API_URL || 'https://chronosx-dev-timetrack-api.gpoda0.easypanel.host/api/v1')
};
