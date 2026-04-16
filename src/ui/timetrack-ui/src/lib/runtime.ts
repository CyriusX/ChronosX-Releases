/**
 * Runtime environment helpers.
 *
 * We ship the same React bundle in two environments:
 * - Desktop (AgentHost): embedded in a WebView with an IPC bridge to the agent/service
 * - WebUI: runs in a normal browser (no IPC)
 *
 * These helpers let us switch behavior without relying on fragile "isConnected" checks
 * (desktop may start disconnected while the bridge is still injecting).
 */

export function isDesktopRuntime(): boolean {
  if (typeof window === 'undefined') return false;

  const w = window as unknown as {
    timeTrackBridge?: unknown;
    chrome?: { webview?: unknown };
    webkit?: { messageHandlers?: Record<string, unknown> };
  };

  // Windows WebView2 exposes window.chrome.webview.
  if (w.chrome?.webview) return true;

  // Our explicit bridge object when injected.
  if (w.timeTrackBridge) return true;

  // Some macOS WKWebView setups expose window.webkit.messageHandlers.
  // If we ever standardize a message handler name, this gives us a hook.
  if (w.webkit?.messageHandlers && Object.keys(w.webkit.messageHandlers).length > 0) return true;

  return false;
}

