import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { inviteLinkApi, type InviteLinkInfoResponse } from '../services/inviteLinkApi';
import { useAuthStore } from '../stores/authStore';
import { shakeX, SPRING, TIMING } from '@desktop/lib/animation';
import logoImg from '@desktop/assets/logo-128.png';

export default function JoinViaLink() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { token = '' } = useParams<{ token: string }>();
  const { setUser, setTokens } = useAuthStore();

  const [linkInfo, setLinkInfo] = useState<InviteLinkInfoResponse | null>(null);
  const [linkError, setLinkError] = useState<string | null>(null);
  const [isLoadingInfo, setIsLoadingInfo] = useState(true);

  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    inviteLinkApi.getInfo(token)
      .then(setLinkInfo)
      .catch(() => setLinkError(t('auth.inviteExpired')))
      .finally(() => setIsLoadingInfo(false));
  }, [token, t]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (password !== confirmPassword) {
      setFormError(t('auth.passwordsMismatch'));
      return;
    }

    if (password.length < 8) {
      setFormError(t('auth.passwordTooShort'));
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await inviteLinkApi.registerViaLink(token, { displayName, email, password });
      setUser({
        id: response.userId,
        email: response.email ?? email,
        displayName: response.displayName,
        role: response.role ?? 'Colaborador',
        isPlatformAdmin: response.isPlatformAdmin ?? false,
        orgId: response.orgId,
        orgName: response.orgName,
        passwordMustChange: response.passwordMustChange ?? false,
        onboardingCompleted: false,
      });
      setTokens({
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        expiresAt: Date.now() + response.expiresIn * 1000,
      });
      navigate('/');
    } catch (err) {
      setFormError(err instanceof Error ? err.message : t('auth.joiningOrg'));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoadingInfo) {
    return (
      <div className="min-h-screen bg-[#0b0d14] flex items-center justify-center">
        <svg className="animate-spin h-8 w-8 text-blue-500" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
        </svg>
      </div>
    );
  }

  if (linkError || (linkInfo && !linkInfo.isValid)) {
    return (
      <div className="min-h-screen bg-[#0b0d14] flex items-center justify-center p-4">
        <div className="w-full max-w-md text-center">
          <div className="bg-[#12141c] rounded-xl p-8 shadow-xl">
            <div className="w-12 h-12 bg-red-500/10 rounded-full flex items-center justify-center mx-auto mb-4">
              <svg className="w-6 h-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>
            <h2 className="text-lg font-semibold text-white mb-2">{t('auth.joinOrgTitle')}</h2>
            <p className="text-zinc-400 text-sm">{linkError || t('auth.inviteExpired')}</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#0b0d14] flex items-center justify-center p-4 py-8">
      <div className="w-full max-w-md">
        <div className="text-center mb-5">
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
            {t('auth.joinOrgTitle')}
          </motion.h1>
          {linkInfo && (
            <motion.p
              className="text-zinc-400 text-sm"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.5, delay: 0.3 }}
            >
              {linkInfo.orgName} · {linkInfo.role}
            </motion.p>
          )}
        </div>

        <motion.form
          onSubmit={handleSubmit}
          className="bg-[#12141c] rounded-xl p-5 shadow-xl"
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ ...SPRING.gentle, delay: 0.3 }}
        >
          <AnimatePresence>
            {formError && (
              <motion.div
                className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-lg overflow-hidden"
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto', ...shakeX }}
                exit={{ opacity: 0, height: 0 }}
                transition={{ duration: TIMING.normal }}
              >
                <p className="text-red-400 text-sm">{formError}</p>
              </motion.div>
            )}
          </AnimatePresence>

          <div className="mb-3">
            <label htmlFor="displayName" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.name')}
            </label>
            <input
              type="text"
              id="displayName"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
              autoFocus
              className="w-full px-4 py-2.5 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder={t('auth.namePlaceholder')}
            />
          </div>

          <div className="mb-3">
            <label htmlFor="email" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.email')}
            </label>
            <input
              type="email"
              id="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full px-4 py-2.5 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder={t('auth.emailPlaceholder')}
            />
          </div>

          <div className="mb-3">
            <label htmlFor="password" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.password')}
            </label>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              className="w-full px-4 py-2.5 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder="••••••••"
            />
            <p className="text-xs text-zinc-500 mt-1">{t('auth.passwordHint')}</p>
          </div>

          <div className="mb-4">
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
              className="w-full px-4 py-2.5 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder="••••••••"
            />
          </div>

          <motion.button
            type="submit"
            disabled={isSubmitting}
            className="w-full py-2.5 px-4 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-600/50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors flex items-center justify-center gap-2"
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
          >
            {isSubmitting ? (
              <>
                <svg className="animate-spin h-5 w-5" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                </svg>
                {t('auth.joiningOrg')}
              </>
            ) : (
              t('auth.joinOrgTitle')
            )}
          </motion.button>
        </motion.form>
      </div>
    </div>
  );
}
