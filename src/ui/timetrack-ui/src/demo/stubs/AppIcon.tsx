import { AppWindow } from 'lucide-react';

interface AppIconProps {
  name: string;
  size?: number;
  className?: string;
}

export function AppIcon({ name, size = 14, className = '' }: AppIconProps) {
  const initial = (name?.trim()?.[0] ?? '').toUpperCase();
  const showInitial = initial >= 'A' && initial <= 'Z';

  return (
    <span
      className={`inline-flex items-center justify-center rounded-[6px] bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.10)] text-[rgba(245,247,251,0.60)] ${className}`}
      style={{ width: size, height: size }}
      aria-label={name}
      title={name}
    >
      {showInitial ? (
        <span style={{ fontSize: Math.max(9, Math.floor(size * 0.6)), lineHeight: 1 }}>
          {initial}
        </span>
      ) : (
        <AppWindow className="w-full h-full" />
      )}
    </span>
  );
}

