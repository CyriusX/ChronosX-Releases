export type DemoMode = 'embed' | 'mobile';

export function getDemoMode(): DemoMode {
  if (typeof window === 'undefined') return 'embed';
  return window.__timetrackDemoMode === 'mobile' ? 'mobile' : 'embed';
}

declare global {
  interface Window {
    __timetrackDemoMode?: DemoMode;
  }
}

