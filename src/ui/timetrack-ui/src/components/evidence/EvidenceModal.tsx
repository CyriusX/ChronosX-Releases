import { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { createPortal } from 'react-dom';
import { X, ChevronLeft, ChevronRight, Download, Camera, ZoomIn, ZoomOut } from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { getEvidenceDownloadUrl } from '../../services/evidenceApi';
import type { EvidenceItem } from '../../types/evidence';

interface EvidenceModalProps {
  items: EvidenceItem[];
  initialIndex: number;
  onClose: () => void;
}

export function EvidenceModal({ items, initialIndex, onClose }: EvidenceModalProps) {
  const { t } = useTranslation();
  const role = useAuthStore(s => s.user?.role);
  const canDownload = role === 'Admin' || role === 'Gestor';

  const [currentIndex, setCurrentIndex] = useState(initialIndex);
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const dragStart = useRef({ x: 0, y: 0, panX: 0, panY: 0 });
  const containerRef = useRef<HTMLDivElement>(null);

  const current = items[currentIndex];

  const loadImage = useCallback(async () => {
    if (!current) return;
    setLoading(true);
    setError(false);
    setZoom(1);
    setPan({ x: 0, y: 0 });

    try {
      const result = await getEvidenceDownloadUrl(current.id);
      if (result.downloadUrl) {
        setImageUrl(result.downloadUrl);
      } else {
        setError(true);
      }
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [current]);

  useEffect(() => { loadImage(); }, [loadImage]);

  const goNext = useCallback(() => {
    if (items.length <= 1) return;
    setCurrentIndex(i => (i + 1) % items.length);
  }, [items.length]);

  const goPrev = useCallback(() => {
    if (items.length <= 1) return;
    setCurrentIndex(i => (i - 1 + items.length) % items.length);
  }, [items.length]);

  const handleDownload = useCallback(async () => {
    if (!current || !canDownload) return;
    try {
      const result = await getEvidenceDownloadUrl(current.id);
      const link = document.createElement('a');
      link.href = result.downloadUrl;
      link.download = `evidence-${current.id}.jpg`;
      link.target = '_blank';
      link.click();
    } catch { /* ignore */ }
  }, [current, canDownload]);

  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    setZoom(z => {
      const next = z + (e.deltaY < 0 ? 0.15 : -0.15);
      return Math.max(0.5, Math.min(5, next));
    });
  }, []);

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    switch (e.key) {
      case 'Escape': onClose(); break;
      case 'ArrowLeft': goPrev(); break;
      case 'ArrowRight': goNext(); break;
    }
  }, [onClose, goPrev, goNext]);

  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = '';
    };
  }, [handleKeyDown]);

  const handleDragStart = useCallback((e: React.MouseEvent) => {
    if (zoom <= 1) return;
    setIsDragging(true);
    dragStart.current = { x: e.clientX, y: e.clientY, panX: pan.x, panY: pan.y };
  }, [zoom, pan]);

  const handleDragMove = useCallback((e: React.MouseEvent) => {
    if (!isDragging) return;
    setPan({
      x: dragStart.current.panX + (e.clientX - dragStart.current.x),
      y: dragStart.current.panY + (e.clientY - dragStart.current.y),
    });
  }, [isDragging]);

  const handleDragEnd = useCallback(() => { setIsDragging(false); }, []);

  return createPortal(
    <div className="fixed inset-0 z-[99999] flex flex-col bg-[rgba(6,8,14,0.95)] backdrop-blur-md">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-[rgba(255,255,255,0.08)]">
        <div className="flex items-center gap-3">
          <button
            onClick={goPrev}
            disabled={items.length <= 1}
            className="flex items-center gap-1 text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] transition-colors disabled:opacity-30"
          >
            <ChevronLeft className="w-4 h-4" />
            {t('evidence.prev')}
          </button>
          <span className="text-[11px] font-medium text-[rgba(245,247,251,0.7)]">
            {t('evidence.screenshot')} — {current ? new Date(current.capturedAt).toLocaleString() : ''}
          </span>
          <button
            onClick={goNext}
            disabled={items.length <= 1}
            className="flex items-center gap-1 text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] transition-colors disabled:opacity-30"
          >
            {t('evidence.next')}
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-[rgba(245,247,251,0.35)]">
            {currentIndex + 1} {t('evidence.of')} {items.length}
          </span>
          <button onClick={onClose} className="p-1 rounded-md hover:bg-[rgba(255,255,255,0.08)] transition-colors">
            <X className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
          </button>
        </div>
      </div>

      {/* Image area */}
      <div
        ref={containerRef}
        className="flex-1 flex items-center justify-center overflow-hidden cursor-grab active:cursor-grabbing"
        onWheel={handleWheel}
        onMouseDown={handleDragStart}
        onMouseMove={handleDragMove}
        onMouseUp={handleDragEnd}
        onMouseLeave={handleDragEnd}
      >
        {loading ? (
          <div className="flex flex-col items-center gap-3">
            <div className="w-12 h-12 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] animate-pulse" />
            <span className="text-[11px] text-[rgba(245,247,251,0.35)]">{t('evidence.loading')}</span>
          </div>
        ) : error ? (
          <div className="flex flex-col items-center gap-3">
            <Camera className="w-10 h-10 text-[rgba(245,247,251,0.15)]" />
            <span className="text-[11px] text-[rgba(245,247,251,0.35)]">{t('evidence.unavailable')}</span>
          </div>
        ) : imageUrl ? (
          <img
            src={imageUrl}
            alt="Screenshot evidence"
            className="max-w-[90vw] max-h-[70vh] object-contain rounded-lg shadow-2xl select-none"
            style={{
              transform: `scale(${zoom}) translate(${pan.x / zoom}px, ${pan.y / zoom}px)`,
              transition: isDragging ? 'none' : 'transform 0.2s ease',
            }}
            draggable={false}
          />
        ) : null}
      </div>

      {/* Zoom controls */}
      <div className="absolute bottom-16 left-1/2 -translate-x-1/2 flex items-center gap-1 px-2 py-1 rounded-lg bg-[rgba(0,0,0,0.5)] border border-[rgba(255,255,255,0.08)]">
        <button
          onClick={() => setZoom(z => Math.max(0.5, z - 0.25))}
          className="p-1 rounded hover:bg-[rgba(255,255,255,0.1)] transition-colors"
        >
          <ZoomOut className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
        </button>
        <span className="text-[10px] text-[rgba(245,247,251,0.5)] w-10 text-center">{Math.round(zoom * 100)}%</span>
        <button
          onClick={() => setZoom(z => Math.min(5, z + 0.25))}
          className="p-1 rounded hover:bg-[rgba(255,255,255,0.1)] transition-colors"
        >
          <ZoomIn className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
        </button>
      </div>

      {/* Footer metadata */}
      <div className="flex items-center justify-between px-4 py-3 border-t border-[rgba(255,255,255,0.08)]">
        <div className="flex items-center gap-4 text-[11px] text-[rgba(245,247,251,0.5)]">
          {current?.appName && (
            <span>
              <span className="text-[rgba(245,247,251,0.35)]">{t('evidence.app')}: </span>
              {current.appName}
            </span>
          )}
          {current?.capturedAt && (
            <span>
              <span className="text-[rgba(245,247,251,0.35)]">{t('evidence.capturedAt')}: </span>
              {new Date(current.capturedAt).toLocaleString()}
            </span>
          )}
        </div>
        {canDownload && (
          <button
            onClick={handleDownload}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[rgba(139,92,246,0.12)] border border-[rgba(139,92,246,0.2)] text-[11px] font-medium text-[#a78bfa] hover:bg-[rgba(139,92,246,0.2)] transition-colors"
          >
            <Download className="w-3.5 h-3.5" />
            {t('evidence.download')}
          </button>
        )}
      </div>
    </div>,
    document.body
  );
}
