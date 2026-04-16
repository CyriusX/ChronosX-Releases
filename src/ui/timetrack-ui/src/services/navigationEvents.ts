export const NAVIGATE_EVENT = 'timetrack:navigate';

export type NavigateDetail = {
  to: string;
  replace?: boolean;
};

export function dispatchNavigate(to: string, replace: boolean = true) {
  try {
    window.dispatchEvent(new CustomEvent<NavigateDetail>(NAVIGATE_EVENT, { detail: { to, replace } }));
  } catch {
    // Non-browser / webview edge case — ignore
  }
}

