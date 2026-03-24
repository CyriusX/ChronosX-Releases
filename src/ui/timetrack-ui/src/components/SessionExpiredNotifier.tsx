/**
 * SessionExpiredNotifier - Listens for session expired events and shows toast
 *
 * This component should be mounted at the app root level.
 * It listens for the SESSION_EXPIRED_EVENT dispatched by apiClient
 * and shows a notification to the user.
 */

import { useEffect, useState } from 'react';
import { SESSION_EXPIRED_EVENT } from '../services/apiClient';

interface SessionExpiredDetail {
  reason: string;
}

export function SessionExpiredNotifier() {
  const [showNotification, setShowNotification] = useState(false);
  const [reason, setReason] = useState('');

  useEffect(() => {
    const handleSessionExpired = (event: CustomEvent<SessionExpiredDetail>) => {
      setReason(event.detail?.reason || 'Sua sessão expirou');
      setShowNotification(true);
    };

    window.addEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired as EventListener);

    return () => {
      window.removeEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired as EventListener);
    };
  }, []);

  // Auto-hide after redirect (component will unmount)
  useEffect(() => {
    if (showNotification) {
      const timer = setTimeout(() => {
        setShowNotification(false);
      }, 5000);
      return () => clearTimeout(timer);
    }
  }, [showNotification]);

  if (!showNotification) return null;

  return (
    <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="bg-[#1a1d2e] border border-[rgba(255,107,107,0.3)] rounded-2xl p-6 max-w-md mx-4 shadow-2xl">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-[rgba(255,107,107,0.15)] flex items-center justify-center">
            <svg
              className="w-5 h-5 text-[#ff6b6b]"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
          <h3 className="text-lg font-semibold text-[#f5f7fb]">Sessão Expirada</h3>
        </div>
        <p className="text-[14px] text-[rgba(245,247,251,0.7)] mb-4">
          {reason}
        </p>
        <p className="text-[12px] text-[rgba(245,247,251,0.5)]">
          Redirecionando para a tela de login...
        </p>
      </div>
    </div>
  );
}
