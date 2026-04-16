/**
 * LinkEmbed — renders a rich card for a URL inside a task description.
 *
 * Provider detection is URL-only (no network calls, no OpenGraph). Supported:
 *   • YouTube / Vimeo / Loom  → video card with inline lightbox (iframe).
 *   • Direct images          → inline preview with click-to-zoom.
 *   • Figma / GitHub / Notion / Google Drive → branded card with deep-link.
 *   • Anything else          → rendered as a plain inline link (caller's job).
 */

import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { ExternalLink, Play, Image as ImageIcon, FileText, X } from 'lucide-react';

type Provider =
  | { kind: 'youtube'; videoId: string; embedUrl: string; thumbnailUrl: string }
  | { kind: 'vimeo'; videoId: string; embedUrl: string }
  | { kind: 'loom'; videoId: string; embedUrl: string }
  | { kind: 'image' }
  | { kind: 'figma'; fileSlug: string }
  | { kind: 'github'; ownerRepo: string; subPath: string | null }
  | { kind: 'notion'; title: string }
  | { kind: 'gdrive'; service: 'Docs' | 'Sheets' | 'Slides' | 'Drive' };

export function detectProvider(rawUrl: string): Provider | null {
  let url: URL;
  try {
    url = new URL(rawUrl);
  } catch {
    return null;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return null;

  const host = url.hostname.toLowerCase().replace(/^www\./, '');
  const path = url.pathname;

  // YouTube
  if (host === 'youtube.com' || host === 'm.youtube.com') {
    const v = url.searchParams.get('v');
    if (v) return youtubeProvider(v);
    const shorts = path.match(/^\/shorts\/([\w-]{6,})/);
    if (shorts) return youtubeProvider(shorts[1]);
    const embed = path.match(/^\/embed\/([\w-]{6,})/);
    if (embed) return youtubeProvider(embed[1]);
  }
  if (host === 'youtu.be') {
    const id = path.replace(/^\//, '').split(/[?#]/)[0];
    if (id) return youtubeProvider(id);
  }

  // Vimeo
  if (host === 'vimeo.com') {
    const m = path.match(/^\/(\d{5,})/);
    if (m) return { kind: 'vimeo', videoId: m[1], embedUrl: `https://player.vimeo.com/video/${m[1]}` };
  }

  // Loom
  if (host === 'loom.com') {
    const m = path.match(/^\/share\/([a-f0-9]{16,})/i);
    if (m) return { kind: 'loom', videoId: m[1], embedUrl: `https://www.loom.com/embed/${m[1]}` };
  }

  // Direct image
  if (/\.(png|jpe?g|gif|webp|svg)(\?|$)/i.test(path)) {
    return { kind: 'image' };
  }

  // Figma
  if (host === 'figma.com' || host === 'www.figma.com') {
    const m = path.match(/^\/(file|design|proto|board)\/[^/]+\/([^/?#]+)/);
    if (m) return { kind: 'figma', fileSlug: decodeURIComponent(m[2]).replace(/-/g, ' ') };
    return { kind: 'figma', fileSlug: 'Figma' };
  }

  // GitHub
  if (host === 'github.com') {
    const m = path.match(/^\/([^/]+)\/([^/]+)(?:\/(.+))?$/);
    if (m) {
      const ownerRepo = `${m[1]}/${m[2]}`;
      let subPath: string | null = null;
      if (m[3]) {
        const parts = m[3].split('/');
        if (parts[0] === 'pull' && parts[1]) subPath = `PR #${parts[1]}`;
        else if (parts[0] === 'issues' && parts[1]) subPath = `Issue #${parts[1]}`;
        else if (parts[0] === 'commit' && parts[1]) subPath = `commit ${parts[1].slice(0, 7)}`;
        else if (parts[0] === 'blob' || parts[0] === 'tree') subPath = parts.slice(2).join('/') || null;
        else subPath = m[3];
      }
      return { kind: 'github', ownerRepo, subPath };
    }
  }

  // Notion
  if (host === 'notion.so' || host.endsWith('.notion.site')) {
    const last = path.split('/').filter(Boolean).pop() ?? 'Notion page';
    // Strip trailing 32-char hex id; turn kebab into spaces; title-case.
    const slug = last.replace(/-?[a-f0-9]{32}$/i, '').replace(/-/g, ' ').trim();
    return { kind: 'notion', title: slug || 'Notion page' };
  }

  // Google Drive / Docs / Sheets / Slides
  if (host === 'docs.google.com') {
    if (path.startsWith('/document')) return { kind: 'gdrive', service: 'Docs' };
    if (path.startsWith('/spreadsheets')) return { kind: 'gdrive', service: 'Sheets' };
    if (path.startsWith('/presentation')) return { kind: 'gdrive', service: 'Slides' };
    return { kind: 'gdrive', service: 'Drive' };
  }
  if (host === 'drive.google.com') return { kind: 'gdrive', service: 'Drive' };

  return null;
}

function youtubeProvider(id: string): Provider {
  return {
    kind: 'youtube',
    videoId: id,
    embedUrl: `https://www.youtube.com/embed/${id}?autoplay=1`,
    thumbnailUrl: `https://i.ytimg.com/vi/${id}/hqdefault.jpg`,
  };
}

// ── Component ──────────────────────────────────────────────────────────────

export function LinkEmbed({ url }: { url: string }) {
  const provider = detectProvider(url);
  if (!provider) {
    // Fallback: plain link card (generic external).
    return <GenericCard url={url} />;
  }

  switch (provider.kind) {
    case 'youtube':
      return <VideoCard url={url} embedUrl={provider.embedUrl} thumbnailUrl={provider.thumbnailUrl} label="YouTube" />;
    case 'vimeo':
      return <VideoCard url={url} embedUrl={provider.embedUrl} label="Vimeo" />;
    case 'loom':
      return <VideoCard url={url} embedUrl={provider.embedUrl} label="Loom" />;
    case 'image':
      return <ImageCard url={url} />;
    case 'figma':
      return <BrandedCard url={url} icon={<FigmaIcon />} title={provider.fileSlug} subtitle="Figma" />;
    case 'github':
      return (
        <BrandedCard
          url={url}
          icon={<GitHubIcon />}
          title={provider.ownerRepo}
          subtitle={provider.subPath ?? 'GitHub'}
        />
      );
    case 'notion':
      return <BrandedCard url={url} icon={<NotionIcon />} title={provider.title} subtitle="Notion" />;
    case 'gdrive':
      return (
        <BrandedCard
          url={url}
          icon={<GoogleIcon service={provider.service} />}
          title={`Google ${provider.service}`}
          subtitle={hostOf(url)}
        />
      );
  }
}

function hostOf(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return url;
  }
}

// ── Card primitives ────────────────────────────────────────────────────────

function CardFrame({
  children,
  onClick,
  href,
}: {
  children: ReactNode;
  onClick?: () => void;
  href?: string;
}) {
  const className =
    'group flex items-center gap-3 px-3 py-2.5 my-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.06)] hover:border-[rgba(255,255,255,0.14)] transition-colors cursor-pointer no-underline text-left w-full';
  if (onClick) {
    return (
      <button type="button" onClick={onClick} className={className}>
        {children}
      </button>
    );
  }
  return (
    <a href={href} target="_blank" rel="noreferrer" className={className}>
      {children}
    </a>
  );
}

function GenericCard({ url }: { url: string }) {
  return (
    <CardFrame href={url}>
      <div className="w-8 h-8 rounded-md bg-[rgba(255,255,255,0.06)] flex items-center justify-center flex-shrink-0">
        <FileText className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[12px] font-medium text-[#f5f7fb] truncate">{hostOf(url)}</div>
        <div className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">{url}</div>
      </div>
      <ExternalLink className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] group-hover:text-[#f5f7fb] flex-shrink-0" />
    </CardFrame>
  );
}

function BrandedCard({
  url,
  icon,
  title,
  subtitle,
}: {
  url: string;
  icon: ReactNode;
  title: string;
  subtitle: string;
}) {
  return (
    <CardFrame href={url}>
      <div className="w-8 h-8 rounded-md bg-[rgba(255,255,255,0.06)] flex items-center justify-center flex-shrink-0 overflow-hidden">
        {icon}
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[12px] font-medium text-[#f5f7fb] truncate">{title}</div>
        <div className="text-[10px] text-[rgba(245,247,251,0.5)] truncate">{subtitle}</div>
      </div>
      <ExternalLink className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] group-hover:text-[#f5f7fb] flex-shrink-0" />
    </CardFrame>
  );
}

function VideoCard({
  url,
  embedUrl,
  thumbnailUrl,
  label,
}: {
  url: string;
  embedUrl: string;
  thumbnailUrl?: string;
  label: string;
}) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="group relative block my-2 w-full overflow-hidden rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] hover:border-[rgba(255,255,255,0.18)] transition-colors"
      >
        <div className="relative aspect-video w-full bg-black">
          {thumbnailUrl ? (
            <img
              src={thumbnailUrl}
              alt={`${label} preview`}
              loading="lazy"
              className="w-full h-full object-cover opacity-80 group-hover:opacity-100 transition-opacity"
              onError={(e) => { (e.currentTarget.style.display = 'none'); }}
            />
          ) : (
            <div className="absolute inset-0 flex items-center justify-center text-[rgba(245,247,251,0.35)] text-[11px]">
              {label}
            </div>
          )}
          <div className="absolute inset-0 flex items-center justify-center">
            <div className="w-12 h-12 rounded-full bg-black/60 backdrop-blur-sm flex items-center justify-center group-hover:bg-[#8B5CF6] group-hover:scale-110 transition-all">
              <Play className="w-5 h-5 text-white fill-white ml-0.5" />
            </div>
          </div>
          <div className="absolute bottom-2 left-2 px-2 py-0.5 rounded bg-black/70 text-[9px] font-semibold uppercase tracking-wider text-white">
            {label}
          </div>
        </div>
      </button>
      {open && <VideoLightbox embedUrl={embedUrl} originalUrl={url} label={label} onClose={() => setOpen(false)} />}
    </>
  );
}

function VideoLightbox({
  embedUrl,
  originalUrl,
  label,
  onClose,
}: {
  embedUrl: string;
  originalUrl: string;
  label: string;
  onClose: () => void;
}) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-[60] bg-black/80 backdrop-blur-sm flex items-center justify-center p-4"
      onClick={onClose}
    >
      <div className="relative w-full max-w-4xl aspect-video" onClick={(e) => e.stopPropagation()}>
        <iframe
          src={embedUrl}
          title={`${label} player`}
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; fullscreen"
          allowFullScreen
          className="w-full h-full rounded-lg bg-black"
        />
        <div className="absolute -top-10 right-0 flex items-center gap-3">
          <a
            href={originalUrl}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1.5 text-[11px] text-[rgba(245,247,251,0.7)] hover:text-white"
          >
            Open in {label}
            <ExternalLink className="w-3 h-3" />
          </a>
          <button
            onClick={onClose}
            aria-label="Close"
            className="p-1.5 rounded-lg bg-[rgba(255,255,255,0.1)] hover:bg-[rgba(255,255,255,0.2)] text-white"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}

function ImageCard({ url }: { url: string }) {
  const [open, setOpen] = useState(false);
  const [failed, setFailed] = useState(false);

  if (failed) {
    return (
      <CardFrame href={url}>
        <div className="w-8 h-8 rounded-md bg-[rgba(255,255,255,0.06)] flex items-center justify-center flex-shrink-0">
          <ImageIcon className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
        </div>
        <div className="flex-1 min-w-0 text-[12px] text-[#f5f7fb] truncate">{url}</div>
        <ExternalLink className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)] flex-shrink-0" />
      </CardFrame>
    );
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="block my-2 w-full overflow-hidden rounded-lg bg-[rgba(0,0,0,0.3)] border border-[rgba(255,255,255,0.08)] hover:border-[rgba(255,255,255,0.18)] transition-colors"
      >
        <img
          src={url}
          alt=""
          loading="lazy"
          onError={() => setFailed(true)}
          className="w-full max-h-[240px] object-contain"
        />
      </button>
      {open && (
        <div
          className="fixed inset-0 z-[60] bg-black/85 backdrop-blur-sm flex items-center justify-center p-4"
          onClick={() => setOpen(false)}
        >
          <img
            src={url}
            alt=""
            className="max-w-full max-h-full object-contain rounded-lg"
            onClick={(e) => e.stopPropagation()}
          />
          <button
            onClick={() => setOpen(false)}
            aria-label="Close"
            className="absolute top-4 right-4 p-2 rounded-lg bg-[rgba(255,255,255,0.1)] hover:bg-[rgba(255,255,255,0.2)] text-white"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}
    </>
  );
}

// ── Brand icons (inline SVG, small) ────────────────────────────────────────

function FigmaIcon() {
  return (
    <svg viewBox="0 0 38 57" className="w-4 h-4" aria-hidden>
      <path fill="#1abcfe" d="M19 28.5a9.5 9.5 0 1 1 19 0 9.5 9.5 0 0 1-19 0Z" />
      <path fill="#0acf83" d="M0 47.5A9.5 9.5 0 0 1 9.5 38H19v9.5a9.5 9.5 0 1 1-19 0Z" />
      <path fill="#ff7262" d="M19 0v19h9.5a9.5 9.5 0 1 0 0-19H19Z" />
      <path fill="#f24e1e" d="M0 9.5A9.5 9.5 0 0 0 9.5 19H19V0H9.5A9.5 9.5 0 0 0 0 9.5Z" />
      <path fill="#a259ff" d="M0 28.5A9.5 9.5 0 0 0 9.5 38H19V19H9.5A9.5 9.5 0 0 0 0 28.5Z" />
    </svg>
  );
}

function GitHubIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4" fill="currentColor" aria-hidden>
      <path
        className="text-[#f5f7fb]"
        fill="currentColor"
        d="M12 .3a12 12 0 0 0-3.8 23.4c.6.1.8-.3.8-.6v-2.2c-3.3.7-4-1.4-4-1.4-.6-1.4-1.4-1.8-1.4-1.8-1.1-.8.1-.8.1-.8 1.2.1 1.8 1.3 1.8 1.3 1.1 1.8 2.8 1.3 3.5 1 .1-.8.4-1.3.8-1.6-2.7-.3-5.5-1.3-5.5-6 0-1.3.5-2.4 1.2-3.2-.1-.3-.5-1.5.1-3.2 0 0 1-.3 3.3 1.2a11.5 11.5 0 0 1 6 0c2.3-1.5 3.3-1.2 3.3-1.2.7 1.7.2 2.9.1 3.2.8.8 1.2 1.9 1.2 3.2 0 4.6-2.8 5.6-5.5 5.9.5.4.8 1.1.8 2.2v3.3c0 .3.2.7.8.6A12 12 0 0 0 12 .3Z"
      />
    </svg>
  );
}

function NotionIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4" aria-hidden>
      <rect width="24" height="24" rx="4" fill="#ffffff" />
      <path fill="#000" d="M6 7v10l2 .5V9.2L15 18l3-.3V7h-2v7L9.5 7Z" />
    </svg>
  );
}

function GoogleIcon({ service }: { service: 'Docs' | 'Sheets' | 'Slides' | 'Drive' }) {
  const color =
    service === 'Docs' ? '#4285f4' : service === 'Sheets' ? '#0f9d58' : service === 'Slides' ? '#f4b400' : '#8b8b8b';
  const letter = service[0];
  return (
    <div
      className="w-4 h-4 rounded-sm flex items-center justify-center text-[9px] font-bold text-white"
      style={{ backgroundColor: color }}
    >
      {letter}
    </div>
  );
}
