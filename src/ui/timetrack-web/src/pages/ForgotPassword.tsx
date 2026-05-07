import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { inviteLinkApi } from '../services/inviteLinkApi';
import { SPRING, TIMING } from '@desktop/lib/animation';
import logoImg from '@desktop/assets/logo-128.png';

export default function ForgotPassword() {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [sent, setSent] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    try {
      await inviteLinkApi.forgotPassword(email);
    } finally {
      setIsLoading(false);
      setSent(true);
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
            {t('auth.forgotPasswordTitle')}
          </motion.h1>
          <motion.p
            className="text-zinc-400 text-sm"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, delay: 0.3 }}
          >
            {t('auth.forgotPasswordSubtitle')}
          </motion.p>
        </div>

        <motion.div
          className="bg-[#12141c] rounded-xl p-6 shadow-xl"
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ ...SPRING.gentle, delay: 0.3 }}
        >
          {sent ? (
            <div className="text-center py-4">
              <div className="w-12 h-12 bg-green-500/10 rounded-full flex items-center justify-center mx-auto mb-4">
                <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
              </div>
              <p className="text-zinc-300 text-sm">{t('auth.resetEmailSent')}</p>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              <div className="mb-5">
                <label htmlFor="email" className="block text-sm font-medium text-zinc-300 mb-2">
                  {t('auth.email')}
                </label>
                <input
                  type="email"
                  id="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  autoFocus
                  className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                  placeholder={t('auth.emailPlaceholder')}
                />
              </div>
              <motion.button
                type="submit"
                disabled={isLoading}
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
                    {t('auth.sendingResetLink')}
                  </>
                ) : (
                  t('auth.sendResetLink')
                )}
              </motion.button>
            </form>
          )}
        </motion.div>

        <motion.p
          className="text-center text-zinc-500 text-sm mt-6"
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: TIMING.normal, delay: 0.6 }}
        >
          <Link to="/login" className="text-blue-400 hover:text-blue-300 transition-colors">
            ← {t('auth.backToLogin')}
          </Link>
        </motion.p>
      </div>
    </div>
  );
}
