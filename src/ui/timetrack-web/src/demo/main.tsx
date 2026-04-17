import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource-variable/inter';
import '@desktop/i18n/i18n';
import '../index.css';
import i18n from 'i18next';
import { DemoApp } from './DemoApp';
import { installLpBridge } from './bridge';

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

const lang = getInitialLang();
void i18n.changeLanguage(lang);
try {
  localStorage.setItem('timetrack-web-language', lang);
} catch {
  // ignore
}

installLpBridge();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <DemoApp />
  </StrictMode>,
);

