# DesktopHost (WebUI) CORS / Load Failed — Investigation & Fix Tracker

## Goal
Ensure the embedded Web UI (Windows WebView2 + macOS WKWebView) never calls the production API directly (which should keep strict CORS), and instead uses a same-origin proxy so login/reports load reliably.

## Symptoms
- DesktopHost shows **Load failed** / login cannot reach API.
- Browser console shows CORS errors like:
  - `Origin http://localhost:<port> is not allowed by Access-Control-Allow-Origin`
  - `Fetch API cannot load https://<prod-api>/api/v1/auth/login due to access control checks`

## Root cause
The UI previously resolved `VITE_API_URL` at module import time. Host-injected config (`window.__APP_CONFIG__.VITE_API_URL`) could be set later (via WebView user scripts), but the UI had already cached the wrong base URL and continued calling production directly, triggering CORS.

## Fixes implemented
- UI now resolves API base **lazily** at call time (not at module import).
- macOS DesktopHost injects `window.__APP_CONFIG__.VITE_API_URL = '/api/v1'` and serves the UI from a localhost origin with an `/api/*` proxy.
- Windows DesktopHost injects `window.__APP_CONFIG__.VITE_API_URL = '/api/v1'` for `https://app.local` and proxies `https://app.local/api/*` to `Agent:Sync:BackendUrl` via `WebResourceRequested`.

## Validation checklist
1. DesktopHost loads without black screen / infinite spinner.
2. Login accepts typing and authenticates successfully.
3. Network requests in DevTools show API calls to:
   - `https://app.local/api/v1/...` (Windows) or `http://localhost:<port>/api/v1/...` (macOS)
   - not directly to `https://chronosx-timetrack-api...`
4. Reports page loads without CORS errors.

