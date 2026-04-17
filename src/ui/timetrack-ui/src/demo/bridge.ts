import i18n from 'i18next';

type Incoming =
  | { type: 'setLang'; lang: 'en-US' | 'pt-BR' }
  | { type: 'navigate'; to: string }
  | { type: 'ready?' };

type Outgoing = { type: 'ready' };

declare global {
  interface Window {
    __timetrackDemoNavigate?: (to: string) => void;
  }
}

export function installLpBridge() {
  if (typeof window === 'undefined') return;

  const onMessage = (event: MessageEvent) => {
    // Same-origin only; ignore anything else.
    if (!event.origin || event.origin !== window.location.origin) return;
    if (!event.data || typeof event.data !== 'object') return;

    const msg = event.data as Incoming;

    if (msg.type === 'ready?') {
      window.parent?.postMessage({ type: 'ready' } satisfies Outgoing, window.location.origin);
      return;
    }

    if (msg.type === 'setLang') {
      void i18n.changeLanguage(msg.lang);
      try {
        localStorage.setItem('timetrack-web-language', msg.lang);
      } catch {
        // ignore
      }
      return;
    }

    if (msg.type === 'navigate') {
      window.__timetrackDemoNavigate?.(msg.to);
    }
  };

  window.addEventListener('message', onMessage);

  // Let the parent know we can receive messages.
  try {
    window.parent?.postMessage({ type: 'ready' } satisfies Outgoing, window.location.origin);
  } catch {
    // ignore
  }
}

