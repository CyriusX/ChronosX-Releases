import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import type { AppLanguage } from '@desktop/types/settings';

const LANGUAGE_KEY = 'timetrack-web-language';

export function useLanguage() {
  const { i18n } = useTranslation();

  const changeLanguage = useCallback(async (lang: AppLanguage) => {
    await i18n.changeLanguage(lang);
    localStorage.setItem(LANGUAGE_KEY, lang);
  }, [i18n]);

  return {
    language: i18n.language as AppLanguage,
    changeLanguage,
  };
}

export function getSavedLanguage(): AppLanguage {
  const saved = localStorage.getItem(LANGUAGE_KEY);
  if (saved === 'pt-BR' || saved === 'en-US' || saved === 'fr-FR' || saved === 'es-ES') {
    return saved;
  }
  return 'en-US';
}
