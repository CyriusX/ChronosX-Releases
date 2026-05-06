import { isDesktopRuntime } from '../lib/runtime';

const PROD_API_BASE = 'https://chronosx-dev-timetrack-api.gpoda0.easypanel.host/api/v1';
const LOCAL_API_BASE = 'http://localhost:5000/api/v1';

export function getApiBaseUrl(): string {
  if (typeof window !== 'undefined') {
    const configured = (window as any).__APP_CONFIG__?.VITE_API_URL as string | undefined;
    if (configured) return configured;
  }

  const explicit = import.meta.env.VITE_API_URL;
  if (explicit) return explicit;

  if (typeof window !== 'undefined') {
    // Desktop host (WebView2 / WKWebView). We do NOT run a local API in production,
    // so default to the deployed API when no env override is provided.
    if (window.location?.protocol === 'timetrack:' || isDesktopRuntime()) {
      return PROD_API_BASE;
    }
  }

  // Web/local dev default
  return LOCAL_API_BASE;
}
