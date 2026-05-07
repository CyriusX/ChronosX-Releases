import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link2, Copy, Trash2, Check } from 'lucide-react';
import { inviteLinkApi, type InviteLinkItem, type GenerateInviteLinkResponse } from '../../services/inviteLinkApi';

export function InviteLinkSection() {
  const { t } = useTranslation();
  const [activeLink, setActiveLink] = useState<(InviteLinkItem & { linkUrl?: string }) | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isRevoking, setIsRevoking] = useState(false);
  const [copied, setCopied] = useState(false);
  const [generatedUrl, setGeneratedUrl] = useState<string | null>(null);

  useEffect(() => {
    load();
  }, []);

  const load = async () => {
    setIsLoading(true);
    try {
      const res = await inviteLinkApi.list();
      if (res.links.length > 0) {
        setActiveLink(res.links[0]);
      } else {
        setActiveLink(null);
      }
    } catch {
      // ignore
    } finally {
      setIsLoading(false);
    }
  };

  const handleGenerate = async () => {
    setIsGenerating(true);
    try {
      const res: GenerateInviteLinkResponse = await inviteLinkApi.generate();
      setGeneratedUrl(res.linkUrl);
      await load();
    } catch {
      // ignore
    } finally {
      setIsGenerating(false);
    }
  };

  const handleRevoke = async () => {
    if (!activeLink) return;
    setIsRevoking(true);
    try {
      await inviteLinkApi.revoke(activeLink.id);
      setActiveLink(null);
      setGeneratedUrl(null);
    } catch {
      // ignore
    } finally {
      setIsRevoking(false);
    }
  };

  const handleCopy = async () => {
    const url = generatedUrl ?? (activeLink as { linkUrl?: string } | null)?.linkUrl;
    if (!url) return;
    await navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const displayUrl = generatedUrl ?? (activeLink as { linkUrl?: string } | null)?.linkUrl;

  return (
    <div className="mt-6 pt-6 border-t border-[rgba(255,255,255,0.06)]">
      <div className="flex items-start gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-blue-500/10 flex items-center justify-center shrink-0 mt-0.5">
          <Link2 className="w-4 h-4 text-blue-400" />
        </div>
        <div>
          <h3 className="text-[14px] font-semibold text-[#f5f7fb]">{t('inviteLink.title')}</h3>
          <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">{t('inviteLink.description')}</p>
        </div>
      </div>

      {isLoading ? (
        <div className="h-12 bg-[rgba(255,255,255,0.03)] rounded-xl animate-pulse" />
      ) : activeLink ? (
        <div className="space-y-3">
          <div className="flex items-center gap-2 bg-[#0b0d14] border border-[rgba(255,255,255,0.06)] rounded-xl px-4 py-3">
            <span className="flex-1 text-xs text-zinc-400 truncate font-mono">
              {displayUrl ?? '—'}
            </span>
            <button
              onClick={handleCopy}
              disabled={!displayUrl}
              className="flex items-center gap-1.5 text-xs text-blue-400 hover:text-blue-300 disabled:opacity-40 transition-colors shrink-0"
            >
              {copied ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
              {copied ? t('inviteLink.copied') : t('inviteLink.copyLink')}
            </button>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-zinc-500">{t('inviteLink.role')}</span>
            {activeLink.useCount > 0 && (
              <span className="text-xs text-zinc-600">
                · {t('inviteLink.usesCount', { count: activeLink.useCount })}
              </span>
            )}
            <div className="flex-1" />
            <button
              onClick={handleRevoke}
              disabled={isRevoking}
              className="flex items-center gap-1.5 text-xs text-red-400/70 hover:text-red-400 disabled:opacity-40 transition-colors"
            >
              <Trash2 className="w-3.5 h-3.5" />
              {isRevoking ? t('inviteLink.revoking') : t('inviteLink.revoke')}
            </button>
          </div>
        </div>
      ) : (
        <button
          onClick={handleGenerate}
          disabled={isGenerating}
          className="flex items-center gap-2 px-4 py-2.5 bg-[rgba(255,255,255,0.04)] hover:bg-[rgba(255,255,255,0.08)] border border-[rgba(255,255,255,0.06)] rounded-xl text-sm text-zinc-300 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Link2 className="w-4 h-4" />
          {isGenerating ? t('inviteLink.generating') : t('inviteLink.generate')}
        </button>
      )}
    </div>
  );
}
