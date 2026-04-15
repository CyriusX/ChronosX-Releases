import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import ptBR from './locales/pt-BR.json';
import enUS from './locales/en-US.json';
import frFR from './locales/fr-FR.json';
import esES from './locales/es-ES.json';

export const resources = {
  'pt-BR': { translation: ptBR },
  'en-US': { translation: enUS },
  'fr-FR': { translation: frFR },
  'es-ES': { translation: esES },
} as const;

type AppLanguage = 'pt-BR' | 'en-US' | 'fr-FR' | 'es-ES';
const VALID_LANGUAGES: AppLanguage[] = ['pt-BR', 'en-US', 'fr-FR', 'es-ES'];

/**
 * Determine the initial language.
 * - Web: reads from localStorage (key used by useLanguage.ts web hook)
 * - Desktop: starts with 'en-US'; the useLanguage hook updates it after IPC loads settings
 */
function getInitialLanguage(): AppLanguage {
  try {
    const saved = localStorage.getItem('timetrack-web-language');
    if (saved && VALID_LANGUAGES.includes(saved as AppLanguage)) {
      return saved as AppLanguage;
    }
  } catch { /* WebView2 / SSR environment — skip */ }
  return 'en-US';
}

i18n.use(initReactI18next).init({
  resources,
  lng: getInitialLanguage(),
  fallbackLng: 'en-US',
  interpolation: { escapeValue: false },
});

export default i18n;
