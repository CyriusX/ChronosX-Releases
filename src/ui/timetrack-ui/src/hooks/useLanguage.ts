import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import type { AppLanguage } from '../types/settings';
import { useIpc } from './useIpc';

export function useLanguage() {
  const { i18n } = useTranslation();
  const { sendCommand } = useIpc();

  const changeLanguage = useCallback(async (lang: AppLanguage) => {
    await i18n.changeLanguage(lang);
    await sendCommand('updateSettings', { language: lang });
  }, [i18n, sendCommand]);

  return {
    language: i18n.language as AppLanguage,
    changeLanguage,
  };
}
