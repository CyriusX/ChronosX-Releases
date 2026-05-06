import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource-variable/inter';
import '../i18n/i18n';
import '../index.css';
import i18n from 'i18next';
import { DemoApp } from './DemoApp';
import { installLpBridge } from './bridge';
import type { DemoMode } from './demoMode';

function getInitialLang(): 'en-US' | 'pt-BR' {
  try {
    const url = new URL(window.location.href);
    const lang = url.searchParams.get('lang');
    if (lang === 'pt-BR' || lang === 'en-US') return lang;
  } catch {
    // ignore
  }
  return 'en-US';
}

function getDemoModeFromUrl(): DemoMode {
  try {
    const url = new URL(window.location.href);
    const mode = url.searchParams.get('mode');
    if (mode === 'mobile') return 'mobile';
  } catch {
    // ignore
  }
  return 'embed';
}

const lang = getInitialLang();
void i18n.changeLanguage(lang);
try {
  localStorage.setItem('timetrack-web-language', lang);
} catch {
  // ignore
}

const demoMode = getDemoModeFromUrl();
window.__timetrackDemoMode = demoMode;
document.documentElement.dataset.demoMode = demoMode;
document.body.dataset.demoMode = demoMode;

if (demoMode === 'mobile') {
  const style = document.createElement('style');
  style.setAttribute('data-timetrack-demo', 'mobile-guards');
  style.textContent = `
    html[data-demo-mode="mobile"], body[data-demo-mode="mobile"] {
      height: 100% !important;
      min-height: 100% !important;
      overflow-y: hidden !important;
      overflow-x: hidden !important;
      overscroll-behavior-x: none;
    }
    #root {
      height: 100dvh;
      min-height: 100dvh;
      max-width: 100vw;
      overflow-y: auto !important;
      overscroll-behavior-x: none;
      -webkit-overflow-scrolling: touch;
      touch-action: auto;
    }
    html[data-demo-mode="mobile"] .flex,
    html[data-demo-mode="mobile"] .grid {
      min-width: 0;
    }
  `;
  document.head.appendChild(style);
}

installLpBridge();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <DemoApp />
  </StrictMode>,
);
