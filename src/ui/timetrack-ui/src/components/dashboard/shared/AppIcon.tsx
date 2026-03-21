/**
 * AppIcon - Displays the real app/brand icon when available.
 *
 * Uses cdn.simpleicons.org for brand SVGs (VS Code, Chrome, Slack, etc.)
 * Falls back to a Lucide icon if the brand icon fails to load.
 */

import { useState, useEffect } from 'react';
import {
  AppWindow, Globe, Code2, Terminal, FileCode, MessageSquare,
  Mail, Video, Music, Play, Paintbrush, Briefcase, MonitorSmartphone
} from 'lucide-react';

// Map app names to Simple Icons slugs
// See https://simpleicons.org/ for available icons
const BRAND_SLUGS: { match: string; slug: string; color: string }[] = [
  { match: 'visual studio code', slug: 'visualstudiocode',  color: '007ACC' },
  { match: 'vs code',            slug: 'visualstudiocode',  color: '007ACC' },
  { match: 'google chrome',      slug: 'googlechrome',      color: '4285F4' },
  { match: 'chrome',             slug: 'googlechrome',      color: '4285F4' },
  { match: 'firefox',            slug: 'firefox',           color: 'FF7139' },
  { match: 'microsoft edge',     slug: 'microsoftedge',     color: '0078D7' },
  { match: 'edge',               slug: 'microsoftedge',     color: '0078D7' },
  { match: 'brave',              slug: 'brave',             color: 'FB542B' },
  { match: 'opera',              slug: 'opera',             color: 'FF1B2D' },
  { match: 'vivaldi',            slug: 'vivaldi',           color: 'EF3939' },
  { match: 'arc',                slug: 'arc',               color: 'FCBFBD' },
  { match: 'slack',              slug: 'slack',             color: '4A154B' },
  { match: 'discord',            slug: 'discord',           color: '5865F2' },
  { match: 'telegram',           slug: 'telegram',          color: '26A5E4' },
  { match: 'whatsapp',           slug: 'whatsapp',          color: '25D366' },
  { match: 'microsoft teams',    slug: 'microsoftteams',    color: '6264A7' },
  { match: 'teams',              slug: 'microsoftteams',    color: '6264A7' },
  { match: 'zoom',               slug: 'zoom',              color: '0B5CFF' },
  { match: 'spotify',            slug: 'spotify',           color: '1DB954' },
  { match: 'youtube',            slug: 'youtube',           color: 'FF0000' },
  { match: 'netflix',            slug: 'netflix',           color: 'E50914' },
  { match: 'twitch',             slug: 'twitch',            color: '9146FF' },
  { match: 'figma',              slug: 'figma',             color: 'F24E1E' },
  { match: 'notion',             slug: 'notion',            color: 'FFFFFF' },
  { match: 'obsidian',           slug: 'obsidian',          color: '7C3AED' },
  { match: 'github',             slug: 'github',            color: 'FFFFFF' },
  { match: 'gitlab',             slug: 'gitlab',            color: 'FC6D26' },
  { match: 'stack overflow',     slug: 'stackoverflow',     color: 'F58025' },
  { match: 'stackoverflow',      slug: 'stackoverflow',     color: 'F58025' },
  { match: 'reddit',             slug: 'reddit',            color: 'FF4500' },
  { match: 'twitter',            slug: 'x',                 color: 'FFFFFF' },
  { match: 'x.com',              slug: 'x',                 color: 'FFFFFF' },
  { match: 'instagram',          slug: 'instagram',         color: 'E4405F' },
  { match: 'facebook',           slug: 'facebook',          color: '0866FF' },
  { match: 'linkedin',           slug: 'linkedin',          color: '0A66C2' },
  { match: 'tiktok',             slug: 'tiktok',            color: 'FFFFFF' },
  { match: 'postman',            slug: 'postman',           color: 'FF6C37' },
  { match: 'insomnia',           slug: 'insomnia',          color: '4000BF' },
  { match: 'docker',             slug: 'docker',            color: '2496ED' },
  { match: 'outlook',            slug: 'microsoftoutlook',  color: '0078D4' },
  { match: 'gmail',              slug: 'gmail',             color: 'EA4335' },
  { match: 'rider',              slug: 'rider',             color: 'DD1265' },
  { match: 'intellij',           slug: 'intellijidea',      color: 'FE315D' },
  { match: 'webstorm',           slug: 'webstorm',          color: '00CDD7' },
  { match: 'pycharm',            slug: 'pycharm',           color: '21D789' },
  { match: 'visual studio',      slug: 'visualstudio',      color: '5C2D91' },
  { match: 'photoshop',          slug: 'adobephotoshop',    color: '31A8FF' },
  { match: 'illustrator',        slug: 'adobeillustrator',  color: 'FF9A00' },
  { match: 'canva',              slug: 'canva',             color: '00C4CC' },
  { match: 'trello',             slug: 'trello',            color: '0052CC' },
  { match: 'jira',               slug: 'jira',              color: '0052CC' },
  { match: 'linear',             slug: 'linear',            color: '5E6AD2' },
  { match: 'vercel',             slug: 'vercel',            color: 'FFFFFF' },
  { match: 'npm',                slug: 'npm',               color: 'CB3837' },
  { match: 'windows terminal',   slug: 'windowsterminal',   color: '4D4D4D' },
  { match: 'powershell',         slug: 'powershell',        color: '5391FE' },
  { match: 'git',                slug: 'git',               color: 'F05032' },
];

// Lucide fallback by subcategory / app name
const LUCIDE_FALLBACKS: { match: string; icon: React.ReactNode }[] = [
  { match: 'code',        icon: <Code2 className="w-full h-full" /> },
  { match: 'terminal',    icon: <Terminal className="w-full h-full" /> },
  { match: 'chat',        icon: <MessageSquare className="w-full h-full" /> },
  { match: 'mail',        icon: <Mail className="w-full h-full" /> },
  { match: 'video',       icon: <Video className="w-full h-full" /> },
  { match: 'music',       icon: <Music className="w-full h-full" /> },
  { match: 'design',      icon: <Paintbrush className="w-full h-full" /> },
  { match: 'browser',     icon: <Globe className="w-full h-full" /> },
  { match: 'social',      icon: <MonitorSmartphone className="w-full h-full" /> },
  { match: 'office',      icon: <Briefcase className="w-full h-full" /> },
  { match: 'play',        icon: <Play className="w-full h-full" /> },
];

// Cache loaded/failed icons to avoid repeated network requests
const iconCache = new Map<string, 'loading' | 'loaded' | 'failed'>();

interface AppIconProps {
  name: string;
  size?: number;
  className?: string;
}

export function AppIcon({ name, size = 14, className = '' }: AppIconProps) {
  const lower = name.toLowerCase();

  // Find brand slug
  const brand = BRAND_SLUGS.find(b => lower.includes(b.match));

  const [status, setStatus] = useState<'loading' | 'loaded' | 'failed'>(
    brand ? (iconCache.get(brand.slug) ?? 'loading') : 'failed'
  );

  const iconUrl = brand ? `https://cdn.simpleicons.org/${brand.slug}/${brand.color}` : '';

  useEffect(() => {
    if (!brand) { setStatus('failed'); return; }

    const cached = iconCache.get(brand.slug);
    if (cached === 'loaded' || cached === 'failed') {
      setStatus(cached);
      return;
    }

    // Preload image
    iconCache.set(brand.slug, 'loading');
    const img = new Image();
    img.onload = () => { iconCache.set(brand.slug, 'loaded'); setStatus('loaded'); };
    img.onerror = () => { iconCache.set(brand.slug, 'failed'); setStatus('failed'); };
    img.src = iconUrl;
  }, [brand, iconUrl]);

  // Show brand icon if loaded
  if (status === 'loaded' && iconUrl) {
    return (
      <img
        src={iconUrl}
        alt={name}
        width={size}
        height={size}
        className={`object-contain ${className}`}
        style={{ width: size, height: size }}
      />
    );
  }

  // Fallback to Lucide icon
  const fallback = LUCIDE_FALLBACKS.find(f => lower.includes(f.match));
  const FallbackIcon = fallback?.icon ?? <AppWindow className="w-full h-full" />;

  return (
    <span
      className={`inline-flex items-center justify-center text-[rgba(245,247,251,0.6)] ${className}`}
      style={{ width: size, height: size }}
    >
      {FallbackIcon}
    </span>
  );
}
