import { useEffect, useRef, useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import { useAuthStore } from '../stores/authStore';
import { shakeX, SPRING, TIMING } from '../lib/animation';
import logoImg from '../assets/logo-128.png';
import { isDesktopRuntime } from '../lib/runtime';

export default function Register() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { register, isLoading, error, setError } = useAuthStore();
  const shouldAnimate = !isDesktopRuntime();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [organizationName, setOrganizationName] = useState('');
  const [success, setSuccess] = useState(false);
  const displayNameRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    const focus = () => displayNameRef.current?.focus();
    const t1 = window.setTimeout(focus, 0);
    const t2 = window.setTimeout(focus, 150);
    const t3 = window.setTimeout(focus, 600);
    return () => {
      window.clearTimeout(t1);
      window.clearTimeout(t2);
      window.clearTimeout(t3);
    };
  }, []);

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

    const response = await register(email, password, displayName, organizationName);
    if (response) {
      setSuccess(true);
      setTimeout(() => {
        navigate('/login');
      }, 2000);
    }
  };

  if (success) {
    return (
      <div className="min-h-screen bg-[#0b0d14] flex items-center justify-center p-4">
        <motion.div
          className="w-full max-w-md text-center"
          initial={shouldAnimate ? { opacity: 0, scale: 0.95 } : false}
          animate={{ opacity: 1, scale: 1 }}
          transition={SPRING.gentle}
        >
          <div className="bg-[#12141c] rounded-xl p-8 shadow-xl">
            <div className="w-16 h-16 bg-green-500/10 rounded-full flex items-center justify-center mx-auto mb-4">
              <svg className="w-8 h-8 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h2 className="text-xl font-semibold text-white mb-2">{t('auth.accountCreated')}</h2>
            <p className="text-zinc-400">{t('auth.redirectingToLogin')}</p>
          </div>
        </motion.div>
      </div>
    );
  }

  return (
    <div
      className="min-h-screen bg-[#0b0d14] flex items-center justify-center p-4"
      onMouseDown={() => displayNameRef.current?.focus()}
      onTouchStart={() => displayNameRef.current?.focus()}
    >
      <div className="w-full max-w-md">
        {/* Logo/Title */}
        <div className="text-center mb-8">
          <motion.img
            src={logoImg}
            alt="ChronosX"
            className="w-20 h-20 mx-auto mb-4 drop-shadow-[0_0_20px_rgba(139,92,246,0.3)]"
            initial={shouldAnimate ? { opacity: 0, scale: 0.8 } : false}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ type: 'spring', stiffness: SPRING.gentle.stiffness, damping: SPRING.gentle.damping, delay: 0.1 }}
          />
          <motion.h1
            className="text-3xl font-bold text-white mb-2"
            initial={shouldAnimate ? { opacity: 0, y: 20 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, delay: 0.2 }}
          >
            ChronosX
          </motion.h1>
          <motion.p
            className="text-zinc-400"
            initial={shouldAnimate ? { opacity: 0, y: 20 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, delay: 0.3 }}
          >
            {t('auth.registerSubtitle')}
          </motion.p>
        </div>

        {/* Register Form */}
        <motion.form
          onSubmit={handleSubmit}
          className="bg-[#12141c] rounded-xl p-6 shadow-xl"
          initial={shouldAnimate ? { opacity: 0, y: 30 } : false}
          animate={{ opacity: 1, y: 0 }}
          transition={{ ...SPRING.gentle, delay: 0.3 }}
        >
          {/* Error Message */}
          <AnimatePresence>
            {error && (
              <motion.div
                className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-lg overflow-hidden"
                initial={shouldAnimate ? { opacity: 0, height: 0 } : false}
                animate={{ opacity: 1, height: 'auto', ...shakeX }}
                exit={{ opacity: 0, height: 0 }}
                transition={{ duration: TIMING.normal }}
              >
                <p className="text-red-400 text-sm">{error}</p>
              </motion.div>
            )}
          </AnimatePresence>

          {/* Name Field */}
          <motion.div
            className="mb-4"
            initial={shouldAnimate ? { opacity: 0, y: 12 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: TIMING.normal, delay: 0.38 }}
          >
            <label htmlFor="displayName" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.name')}
            </label>
            <input
              type="text"
              id="displayName"
              ref={displayNameRef}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
              autoFocus
              className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder={t('auth.namePlaceholder')}
            />
          </motion.div>

          {/* Organization Name Field */}
          <motion.div
            className="mb-4"
            initial={shouldAnimate ? { opacity: 0, y: 12 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: TIMING.normal, delay: 0.46 }}
          >
            <label htmlFor="organizationName" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.orgName')}
            </label>
            <input
              type="text"
              id="organizationName"
              value={organizationName}
              onChange={(e) => setOrganizationName(e.target.value)}
              required
              className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder={t('auth.orgPlaceholder')}
            />
          </motion.div>

          {/* Email Field */}
          <motion.div
            className="mb-4"
            initial={shouldAnimate ? { opacity: 0, y: 12 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: TIMING.normal, delay: 0.54 }}
          >
            <label htmlFor="email" className="block text-sm font-medium text-zinc-300 mb-2">
              {t('auth.email')}
            </label>
            <input
              type="email"
              id="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder={t('auth.emailPlaceholder')}
            />
          </motion.div>

          {/* Password Field */}
          <motion.div
            className="mb-4"
            initial={shouldAnimate ? { opacity: 0, y: 12 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: TIMING.normal, delay: 0.62 }}
          >
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
              className="w-full px-4 py-3 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              placeholder="••••••••"
            />
            <p className="text-xs text-zinc-500 mt-1">{t('auth.passwordHint')}</p>
          </motion.div>

          {/* Confirm Password Field */}
          <motion.div
            className="mb-6"
            initial={shouldAnimate ? { opacity: 0, y: 12 } : false}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: TIMING.normal, delay: 0.7 }}
          >
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
          </motion.div>

          {/* Submit Button */}
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
                  <circle
                    className="opacity-25"
                    cx="12"
                    cy="12"
                    r="10"
                    stroke="currentColor"
                    strokeWidth="4"
                    fill="none"
                  />
                  <path
                    className="opacity-75"
                    fill="currentColor"
                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                  />
                </svg>
                {t('auth.registering')}
              </>
            ) : (
              t('auth.register')
            )}
          </motion.button>
        </motion.form>

        {/* Footer */}
        <motion.p
          className="text-center text-zinc-500 text-sm mt-6"
          initial={shouldAnimate ? { opacity: 0, y: 12 } : false}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: TIMING.normal, delay: 0.8 }}
        >
          {t('auth.hasAccount')}{' '}
          <Link to="/login" className="text-blue-400 hover:text-blue-300 transition-colors">
            {t('auth.goToLogin')}
          </Link>
        </motion.p>
      </div>
    </div>
  );
}
