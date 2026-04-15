/**
 * DescriptionEditor — textarea wrapper with a compact toolbar, Cmd+K link
 * popover, and smart paste. Produces plain markdown text that flows through
 * the existing SimpleMarkdown renderer unchanged.
 */

import type { FormEvent, KeyboardEvent as ReactKeyboardEvent, ClipboardEvent as ReactClipboardEvent, ReactNode } from 'react';
import { useRef, useState, useCallback, useEffect } from 'react';
import { Bold, Italic, Link as LinkIcon, List, Code } from 'lucide-react';

interface Props {
  value: string;
  onChange: (value: string) => void;
  maxLength?: number;
  rows?: number;
  placeholder?: string;
  autoFocus?: boolean;
  className?: string;
}

const URL_ONLY = /^https?:\/\/\S+$/i;

export function DescriptionEditor({
  value,
  onChange,
  maxLength = 5000,
  rows = 4,
  placeholder,
  autoFocus,
  className,
}: Props) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [linkPopover, setLinkPopover] = useState<{ start: number; end: number } | null>(null);

  const surroundSelection = useCallback(
    (before: string, after: string = before) => {
      const el = textareaRef.current;
      if (!el) return;
      const start = el.selectionStart;
      const end = el.selectionEnd;
      const selected = value.slice(start, end);
      const next = value.slice(0, start) + before + selected + after + value.slice(end);
      if (next.length > maxLength) return;
      onChange(next);
      requestAnimationFrame(() => {
        if (!textareaRef.current) return;
        textareaRef.current.focus();
        const newStart = start + before.length;
        const newEnd = newStart + selected.length;
        textareaRef.current.setSelectionRange(newStart, newEnd);
      });
    },
    [value, onChange, maxLength],
  );

  const prefixLines = useCallback(
    (prefix: string) => {
      const el = textareaRef.current;
      if (!el) return;
      const start = el.selectionStart;
      const end = el.selectionEnd;
      const lineStart = value.lastIndexOf('\n', Math.max(start - 1, 0)) + 1;
      const lineEnd = value.indexOf('\n', end);
      const segmentEnd = lineEnd === -1 ? value.length : lineEnd;
      const segment = value.slice(lineStart, segmentEnd);
      const prefixed = segment
        .split('\n')
        .map((l) => (l.startsWith(prefix) ? l : prefix + l))
        .join('\n');
      const next = value.slice(0, lineStart) + prefixed + value.slice(segmentEnd);
      if (next.length > maxLength) return;
      onChange(next);
    },
    [value, onChange, maxLength],
  );

  const openLinkPopover = useCallback(() => {
    const el = textareaRef.current;
    if (!el) return;
    setLinkPopover({ start: el.selectionStart, end: el.selectionEnd });
  }, []);

  const insertLink = useCallback(
    (url: string, label: string) => {
      if (!linkPopover) return;
      const { start, end } = linkPopover;
      const text = label.trim() || url;
      const snippet = `[${text}](${url})`;
      const next = value.slice(0, start) + snippet + value.slice(end);
      if (next.length > maxLength) return;
      onChange(next);
      setLinkPopover(null);
      requestAnimationFrame(() => {
        textareaRef.current?.focus();
        const caret = start + snippet.length;
        textareaRef.current?.setSelectionRange(caret, caret);
      });
    },
    [value, onChange, maxLength, linkPopover],
  );

  const handleKeyDown = (e: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      openLinkPopover();
      return;
    }
    if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'b') {
      e.preventDefault();
      surroundSelection('**');
      return;
    }
    if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'i') {
      e.preventDefault();
      surroundSelection('*');
      return;
    }
  };

  const handlePaste = (e: ReactClipboardEvent<HTMLTextAreaElement>) => {
    const pasted = e.clipboardData.getData('text/plain');
    if (!pasted || !URL_ONLY.test(pasted.trim())) return;
    const url = pasted.trim();
    const el = textareaRef.current;
    if (!el) return;
    const start = el.selectionStart;
    const end = el.selectionEnd;
    const selected = value.slice(start, end);

    if (selected) {
      e.preventDefault();
      const snippet = `[${selected}](${url})`;
      const next = value.slice(0, start) + snippet + value.slice(end);
      if (next.length > maxLength) return;
      onChange(next);
      requestAnimationFrame(() => {
        textareaRef.current?.focus();
        const caret = start + snippet.length;
        textareaRef.current?.setSelectionRange(caret, caret);
      });
    } else {
      // No selection: insert the URL on its own line so it embeds in preview.
      e.preventDefault();
      const before = value.slice(0, start);
      const needsLeadingNewline = before.length > 0 && !/\n\n?$/.test(before);
      const after = value.slice(end);
      const needsTrailingNewline = after.length > 0 && !/^\n/.test(after);
      const snippet = `${needsLeadingNewline ? '\n' : ''}${url}${needsTrailingNewline ? '\n' : ''}`;
      const next = before + snippet + after;
      if (next.length > maxLength) return;
      onChange(next);
      requestAnimationFrame(() => {
        textareaRef.current?.focus();
        const caret = start + snippet.length;
        textareaRef.current?.setSelectionRange(caret, caret);
      });
    }
  };

  return (
    <div className={`relative ${className ?? ''}`}>
      <div className="flex items-center gap-0.5 mb-1.5">
        <ToolbarButton title="Bold (Cmd+B)" onClick={() => surroundSelection('**')}>
          <Bold className="w-3 h-3" />
        </ToolbarButton>
        <ToolbarButton title="Italic (Cmd+I)" onClick={() => surroundSelection('*')}>
          <Italic className="w-3 h-3" />
        </ToolbarButton>
        <ToolbarButton title="Inline code" onClick={() => surroundSelection('`')}>
          <Code className="w-3 h-3" />
        </ToolbarButton>
        <ToolbarButton title="Bullet list" onClick={() => prefixLines('- ')}>
          <List className="w-3 h-3" />
        </ToolbarButton>
        <ToolbarButton title="Insert link (Cmd+K)" onClick={openLinkPopover}>
          <LinkIcon className="w-3 h-3" />
        </ToolbarButton>
        <div className="ml-auto text-[9px] text-[rgba(245,247,251,0.35)] tabular-nums pr-0.5">
          {value.length}/{maxLength}
        </div>
      </div>
      <textarea
        ref={textareaRef}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
        maxLength={maxLength}
        rows={rows}
        placeholder={placeholder}
        autoFocus={autoFocus}
        className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)] resize-none"
      />
      {linkPopover && (
        <LinkPopover
          initialLabel={value.slice(linkPopover.start, linkPopover.end)}
          onInsert={insertLink}
          onClose={() => setLinkPopover(null)}
        />
      )}
    </div>
  );
}

function ToolbarButton({
  title,
  onClick,
  children,
}: {
  title: string;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      className="p-1.5 rounded text-[rgba(245,247,251,0.5)] hover:text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
    >
      {children}
    </button>
  );
}

function LinkPopover({
  initialLabel,
  onInsert,
  onClose,
}: {
  initialLabel: string;
  onInsert: (url: string, label: string) => void;
  onClose: () => void;
}) {
  const [url, setUrl] = useState('');
  const [label, setLabel] = useState(initialLabel);
  const urlRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    urlRef.current?.focus();
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  const submit = (e?: FormEvent) => {
    e?.preventDefault();
    const trimmed = url.trim();
    if (!/^https?:\/\//i.test(trimmed)) return;
    onInsert(trimmed, label);
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-start justify-center pt-[20vh] bg-black/40 backdrop-blur-sm" onClick={onClose}>
      <form
        onSubmit={submit}
        onClick={(e) => e.stopPropagation()}
        className="w-[360px] max-w-[92vw] bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl p-4 space-y-3"
      >
        <div>
          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">
            URL
          </label>
          <input
            ref={urlRef}
            type="url"
            placeholder="https://example.com"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          />
        </div>
        <div>
          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">
            Label (optional)
          </label>
          <input
            type="text"
            placeholder="Display text"
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          />
        </div>
        <div className="flex items-center justify-end gap-2 pt-1">
          <button
            type="button"
            onClick={onClose}
            className="px-3 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.06)]"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={!/^https?:\/\//i.test(url.trim())}
            className="px-3 py-1.5 rounded-lg text-[11px] font-semibold bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Insert
          </button>
        </div>
      </form>
    </div>
  );
}
