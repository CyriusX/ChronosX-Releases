import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '../stores/authStore';
import { inviteLinkApi, type GenerateInviteLinkResponse } from '../services/inviteLinkApi';
import { api } from '../services/apiClient';
import { SPRING, TIMING } from '@desktop/lib/animation';

type Step = 1 | 2 | 3 | 4;

export default function Onboarding() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const setOnboardingComplete = useAuthStore((state) => state.setOnboardingComplete);

  const [step, setStep] = useState<Step>(1);

  // Step 2 — invite by email
  const [inviteEmail, setInviteEmail] = useState('');
  const [isInviting, setIsInviting] = useState(false);
  const [inviteStatus, setInviteStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [invitedEmails, setInvitedEmails] = useState<string[]>([]);

  // Step 3 — invite link
  const [inviteLink, setInviteLink] = useState<GenerateInviteLinkResponse | null>(null);
  const [isLoadingLink, setIsLoadingLink] = useState(false);
  const [copied, setCopied] = useState(false);

  // Step 4 — finishing
  const [isFinishing, setIsFinishing] = useState(false);

  useEffect(() => {
    if (user?.onboardingCompleted === true) {
      navigate('/');
    }
  }, [user, navigate]);

  const handleInvite = async () => {
    if (!inviteEmail) return;
    setIsInviting(true);
    const displayName = inviteEmail.split('@')[0];
    try {
      await api.post('/auth/invite', { email: inviteEmail, displayName, role: 'Colaborador' });
      setInvitedEmails((prev) => [...prev, inviteEmail]);
      setInviteStatus('success');
      setInviteEmail('');
    } catch {
      setInviteStatus('error');
    } finally {
      setIsInviting(false);
    }
  };

  const handleGenerateLink = async () => {
    setIsLoadingLink(true);
    try {
      const existing = await inviteLinkApi.list();
      if (existing.links.length > 0) {
        const link = existing.links[0];
        setInviteLink({ id: link.id, token: '', linkUrl: '' });
        // Re-generate to get fresh URL
        const fresh = await inviteLinkApi.generate();
        setInviteLink(fresh);
      } else {
        const fresh = await inviteLinkApi.generate();
        setInviteLink(fresh);
      }
    } catch {
      // ignore
    } finally {
      setIsLoadingLink(false);
    }
  };

  const handleCopy = async () => {
    if (!inviteLink?.linkUrl) return;
    await navigator.clipboard.writeText(inviteLink.linkUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleFinish = async () => {
    setIsFinishing(true);
    try {
      await api.post('/onboarding/complete', {});
      setOnboardingComplete(true);
      navigate('/');
    } catch {
      setOnboardingComplete(true);
      navigate('/');
    } finally {
      setIsFinishing(false);
    }
  };

  const stepTitles: Record<Step, string> = {
    1: t('onboarding.step1Title'),
    2: t('onboarding.step2Title'),
    3: t('onboarding.step3Title'),
    4: t('onboarding.step4Title'),
  };

  return (
    <div className="min-h-screen bg-[#0b0d14] flex items-center justify-center p-4">
      <div className="w-full max-w-lg">
        {/* Progress dots */}
        <div className="flex items-center justify-center gap-2 mb-8">
          {([1, 2, 3, 4] as Step[]).map((s) => (
            <div
              key={s}
              className={`rounded-full transition-all duration-300 ${
                s === step ? 'w-6 h-2 bg-blue-500' : s < step ? 'w-2 h-2 bg-blue-400' : 'w-2 h-2 bg-zinc-700'
              }`}
            />
          ))}
        </div>

        <AnimatePresence mode="wait">
          <motion.div
            key={step}
            initial={{ opacity: 0, x: 20 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -20 }}
            transition={{ ...SPRING.gentle }}
          >
            <div className="bg-[#12141c] rounded-2xl p-8 shadow-xl">
              <p className="text-xs text-zinc-500 uppercase tracking-wider mb-2">
                {t('onboarding.title')} · {step}/4
              </p>
              <h2 className="text-xl font-semibold text-white mb-6">{stepTitles[step]}</h2>

              {/* Step 1 — Welcome */}
              {step === 1 && (
                <div>
                  <p className="text-zinc-300 mb-2">
                    {t('common.hello', { name: user?.displayName ?? '' })}
                  </p>
                  <p className="text-zinc-400 text-sm mb-8">
                    {user?.orgName && (
                      <span className="font-medium text-zinc-300">{user.orgName}</span>
                    )}
                  </p>
                  <button
                    onClick={() => setStep(2)}
                    className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
                  >
                    {t('onboarding.next')}
                  </button>
                </div>
              )}

              {/* Step 2 — Invite team */}
              {step === 2 && (
                <div>
                  {invitedEmails.length > 0 && (
                    <div className="mb-4 space-y-1">
                      {invitedEmails.map((email) => (
                        <div key={email} className="flex items-center gap-2 text-sm text-green-400">
                          <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                          </svg>
                          <span className="truncate">{email}</span>
                        </div>
                      ))}
                    </div>
                  )}
                  <div className="mb-4">
                    <label className="block text-sm font-medium text-zinc-300 mb-2">
                      {t('onboarding.inviteEmailPlaceholder')}
                    </label>
                    <input
                      type="email"
                      value={inviteEmail}
                      onChange={(e) => { setInviteEmail(e.target.value); setInviteStatus('idle'); }}
                      onKeyDown={(e) => e.key === 'Enter' && inviteEmail && handleInvite()}
                      className="w-full px-4 py-2.5 bg-[#0b0d14] border border-zinc-800 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                      placeholder="colaborador@empresa.com"
                    />
                  </div>
                  {inviteStatus === 'error' && (
                    <p className="text-red-400 text-sm mb-3">{t('common.error')}</p>
                  )}
                  <div className="flex gap-3">
                    <button
                      onClick={handleInvite}
                      disabled={isInviting || !inviteEmail}
                      className="flex-1 py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-600/50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors"
                    >
                      {isInviting ? t('onboarding.inviting') : t('onboarding.inviteButton')}
                    </button>
                    <button
                      onClick={() => setStep(3)}
                      className="px-5 py-3 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 font-medium rounded-lg transition-colors text-sm"
                    >
                      {t('onboarding.skip')}
                    </button>
                  </div>
                  {invitedEmails.length > 0 && (
                    <button
                      onClick={() => setStep(3)}
                      className="w-full mt-3 py-2.5 text-sm text-zinc-400 hover:text-zinc-200 transition-colors"
                    >
                      {t('onboarding.next')} →
                    </button>
                  )}
                </div>
              )}

              {/* Step 3 — Invite link */}
              {step === 3 && (
                <div>
                  <p className="text-zinc-400 text-sm mb-6">{t('inviteLink.description')}</p>
                  {inviteLink ? (
                    <div className="mb-6">
                      <div className="flex items-center gap-2 bg-[#0b0d14] border border-zinc-800 rounded-lg px-3 py-2.5">
                        <span className="flex-1 text-xs text-zinc-300 truncate">{inviteLink.linkUrl}</span>
                        <button
                          onClick={handleCopy}
                          className="text-xs text-blue-400 hover:text-blue-300 font-medium shrink-0"
                        >
                          {copied ? t('onboarding.copied') : t('onboarding.copyLink')}
                        </button>
                      </div>
                    </div>
                  ) : (
                    <button
                      onClick={handleGenerateLink}
                      disabled={isLoadingLink}
                      className="w-full mb-6 py-3 bg-zinc-800 hover:bg-zinc-700 disabled:opacity-50 text-zinc-200 font-medium rounded-lg transition-colors"
                    >
                      {isLoadingLink ? t('inviteLink.generating') : t('inviteLink.generate')}
                    </button>
                  )}
                  <button
                    onClick={() => setStep(4)}
                    className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
                  >
                    {t('onboarding.next')}
                  </button>
                </div>
              )}

              {/* Step 4 — Done */}
              {step === 4 && (
                <div className="text-center">
                  <div className="w-16 h-16 bg-green-500/10 rounded-full flex items-center justify-center mx-auto mb-4">
                    <svg className="w-8 h-8 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                  </div>
                  <p className="text-zinc-300 mb-8">{t('onboarding.allSet')}</p>
                  <button
                    onClick={handleFinish}
                    disabled={isFinishing}
                    className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-600/50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors flex items-center justify-center gap-2"
                  >
                    {isFinishing ? (
                      <>
                        <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                        </svg>
                        {t('common.loading')}
                      </>
                    ) : (
                      t('onboarding.finish')
                    )}
                  </button>
                </div>
              )}
            </div>
          </motion.div>
        </AnimatePresence>

        <motion.p
          className="text-center text-zinc-600 text-xs mt-6"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: TIMING.normal, delay: 0.5 }}
        >
          {t('onboarding.title')}
        </motion.p>
      </div>
    </div>
  );
}
