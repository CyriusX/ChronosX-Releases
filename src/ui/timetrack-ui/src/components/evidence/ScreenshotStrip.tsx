import { useState, useEffect, useRef } from 'react';
import { Camera } from 'lucide-react';
import { getBatchDownloadUrls } from '../../services/evidenceApi';
import type { EvidenceItem } from '../../types/evidence';

interface ScreenshotStripProps {
  evidenceItems: EvidenceItem[];
  onThumbnailClick: (index: number) => void;
}

const THUMB_WIDTH = 120;
const THUMB_HEIGHT = 68;
const MAX_VISIBLE = 30;

export function ScreenshotStrip({ evidenceItems, onThumbnailClick }: ScreenshotStripProps) {
  const [urlCache, setUrlCache] = useState<Map<string, string>>(new Map());
  const [isLoading, setIsLoading] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const prevIdsRef = useRef<Set<string>>(new Set());

  const visibleItems = showAll ? evidenceItems : evidenceItems.slice(0, MAX_VISIBLE);
  const hasMore = evidenceItems.length > MAX_VISIBLE;

  useEffect(() => {
    if (evidenceItems.length === 0) return;

    const currentIds = new Set(evidenceItems.map(e => e.id));
    const newIds = evidenceItems.filter(e => !prevIdsRef.current.has(e.id)).map(e => e.id);

    if (newIds.length === 0 && prevIdsRef.current.size > 0) return;

    setIsLoading(true);
    getBatchDownloadUrls(newIds.length > 0 ? newIds : evidenceItems.map(e => e.id))
      .then(results => {
        setUrlCache(prev => {
          const next = new Map(prev);
          for (const r of results) {
            if (r.downloadUrl) next.set(r.evidenceId, r.downloadUrl);
          }
          return next;
        });
      })
      .catch(() => {})
      .finally(() => {
        setIsLoading(false);
        prevIdsRef.current = currentIds;
      });
  }, [evidenceItems]);

  if (evidenceItems.length === 0) return null;

  return (
    <div className="mt-2">
      <div className="flex items-center gap-1.5 mb-1">
        <Camera className="w-2.5 h-2.5 text-[rgba(245,247,251,0.35)]" />
        <span className="text-[8px] uppercase tracking-wider text-[rgba(245,247,251,0.3)] font-semibold">
          Screenshots
        </span>
        {isLoading && (
          <span className="text-[8px] text-[rgba(245,247,251,0.25)]">...</span>
        )}
      </div>

      <div
        className="flex gap-1.5 overflow-x-auto pb-1"
        style={{ scrollbarWidth: 'thin', scrollbarColor: 'rgba(255,255,255,0.08) transparent' }}
      >
        {visibleItems.map((item, idx) => {
          const url = urlCache.get(item.id);
          const time = new Date(item.capturedAt).toLocaleTimeString(undefined, {
            hour: '2-digit',
            minute: '2-digit',
          });

          return (
            <button
              key={item.id}
              onClick={() => onThumbnailClick(idx)}
              className="flex-shrink-0 rounded-md overflow-hidden cursor-pointer group relative border border-[rgba(255,255,255,0.06)] hover:border-[rgba(139,92,246,0.4)] transition-colors bg-[rgba(255,255,255,0.03)]"
              style={{ width: THUMB_WIDTH, height: THUMB_HEIGHT }}
            >
              {url ? (
                <img
                  src={url}
                  alt={`${item.appName} screenshot`}
                  width={THUMB_WIDTH}
                  height={THUMB_HEIGHT}
                  loading="lazy"
                  decoding="async"
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="w-full h-full flex items-center justify-center">
                  <Camera className="w-4 h-4 text-[rgba(245,247,251,0.1)]" />
                </div>
              )}

              <div className="absolute bottom-0 left-0 right-0 px-1.5 py-0.5 bg-gradient-to-t from-black/80 to-transparent pointer-events-none">
                <p className="text-[8px] font-medium text-white truncate leading-tight">
                  {item.appName}
                </p>
                <p className="text-[7px] text-[rgba(255,255,255,0.5)] leading-tight">
                  {time}
                </p>
              </div>
            </button>
          );
        })}
      </div>

      {hasMore && !showAll && (
        <button
          onClick={() => setShowAll(true)}
          className="mt-1 text-[9px] text-[#a78bfa] hover:text-[#c4b5fd] transition-colors"
        >
          Show all {evidenceItems.length} screenshots
        </button>
      )}
    </div>
  );
}
