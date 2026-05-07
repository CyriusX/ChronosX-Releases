import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { inviteLinkApi } from '../services/inviteLinkApi';
import { shakeX, SPRING, TIMING } from '@desktop/lib/animation';
import logoImg from '@desktop/assets/logo-128.png';

export default function AcceptInvite() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const emailParam = searchParams.get('email') ?? '';

  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (password !== confirmPassword) {
      setError(t('auth.passwordsMismatch'));
      return;
    }

    if (password.length < 8) {
      setError(t('auth.passwordTooShort'));
      return;
    }

    setIsLoading(true);
    try {
      await inviteLinkApi.resetPassword(token, password);
      navigate('/login?invited=true');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.inviteExpired'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#0b0d14] flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <motion.img
            src={logoImg}
            alt="ChronosX"
            className="w-14 h-14 mx-auto mb-2 drop-shadow-[0_0_20px_rgba(139,92,246,0.3)]"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ type: 'spring', stiffness: SPRING.gentle.stiffness, damping: SPRING.gentle.damping, delay: 0.1 }}
          />
          <motion.h1
            className="text-2xl font-bold text-white mb-1"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, delay: 0.2 }}
          >
            {t('auth.acceptInviteTitle')}
          </motion.h1>
          <motion.p
            className="text-zinc-400 text-sm"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, delay: 0.3 }}
          >
            {t('auth.acceptInviteSubtitle')}
          </motion.p>
        </div>

        <motion.form
          onSubmit={handleSubmit}
          className="bg-[#12141c] rounded-xl p-6 shadow-xl"
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ ...SPRING.gentle, delay: 0.3 }}
        >
          <AnimatePresence>
            {error && (
              <motion.div
                className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-lg overflow-hidden"
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto', ...shakeX }}
                exit={{ opacity: 0, height: 0 }}
                transition={{ duration: TIMING.normal }}
              >
                <p className="text-red-400 text-sm">{error}</p>
              </motion.div>
            )}
          </AnimatePresence>

          {emailParam && (
            <div className="mb-4">
              <label className="block text-sm font-medium text-zinc-300 mb-2">
                {t('auth.email')}
              </label>
              <input
                type="email"
                value={emailParam}
                disabled
                className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-700 rounded-lg text-zinc-400 cursor-not-allowed"
              />
            </div>
          )}

          <div className="mb-4">
            <label htmlFor="password" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.newPassword')}
            </label>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              autoFocus
              className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder="••••••••"
            />
            <p className="text-xs text-zinc-500 mt-1">{t('auth.passwordHint')}</p>
          </div>

          <div className="mb-6">
            <label htmlFor="confirmPassword" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.confirmPassword')}
            </label>
            <input
              type="password"
              id="confirmPassword"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              minLength={8}
              className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder="••••••••"
            />
          </div>

          <motion.button
            type="submit"
            disabled={isLoading || !token}
            className="w-full py-3 px-4 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-600/50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors flex items-center justify-center gap-2"
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
          >
            {isLoading ? (
              <>
                <svg className="animate-spin h-5 w-5" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                </svg>
                {t('auth.settingPassword')}
              </>
            ) : (
              t('auth.setPassword')
            )}
          </motion.button>
        </motion.form>
      </div>
    </div>
  );
}
