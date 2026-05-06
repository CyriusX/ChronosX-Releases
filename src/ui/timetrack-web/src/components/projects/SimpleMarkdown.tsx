/**
 * SimpleMarkdown — minimal, safe markdown renderer.
 *
 * Handles what Linear and humans actually write in task descriptions without
 * pulling in a full markdown dependency:
 *   • Headings (# ## ###)
 *   • Bullet lists (- * +) and numbered lists (1. 2.)
 *   • **bold**, *italic*, `inline code`, [text](url)
 *   • Blank-line separated paragraphs with preserved inline line breaks
 *
 * All URLs are escaped to only render http/https links; the component never
 * sets innerHTML so there is no XSS surface.
 */

import React from 'react';
import { LinkEmbed } from './LinkEmbed';

type Block =
  | { kind: 'heading'; level: 1 | 2 | 3; text: string }
  | { kind: 'bullet'; items: string[] }
  | { kind: 'ordered'; items: string[] }
  | { kind: 'paragraph'; text: string }
  | { kind: 'embed'; url: string };

// A paragraph counts as "standalone link" if, once trimmed, it is only a URL
// or only a single `[text](url)` — in which case we render a rich LinkEmbed
// instead of inline text.
const STANDALONE_URL = /^https?:\/\/\S+$/i;
const STANDALONE_MD_LINK = /^\[[^\]]+\]\((https?:\/\/[^\s)]+)\)$/i;

function asStandaloneEmbedUrl(paragraphText: string): string | null {
  const t = paragraphText.trim();
  if (STANDALONE_URL.test(t)) return t;
  const m = t.match(STANDALONE_MD_LINK);
  if (m) return m[1];
  return null;
}

export function SimpleMarkdown({ source }: { source: string }) {
  if (!source?.trim()) return null;

  const blocks = parseBlocks(source);

  return (
    <div className="space-y-2.5 text-[12px] leading-relaxed text-[rgba(245,247,251,0.85)]">
      {blocks.map((b, i) => renderBlock(b, i))}
    </div>
  );
}

// ── Block parser ───────────────────────────────────────────────────────────

function parseBlocks(source: string): Block[] {
  const lines = source.replace(/\r\n/g, '\n').split('\n');
  const blocks: Block[] = [];

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    if (!trimmed) {
      i++;
      continue;
    }

    // Heading
    const headingMatch = trimmed.match(/^(#{1,3})\s+(.+)$/);
    if (headingMatch) {
      const level = headingMatch[1].length as 1 | 2 | 3;
      blocks.push({ kind: 'heading', level, text: headingMatch[2] });
      i++;
      continue;
    }

    // Bullet list — gather consecutive items
    if (/^[-*+]\s+/.test(trimmed)) {
      const items: string[] = [];
      while (i < lines.length) {
        const m = lines[i].trim().match(/^[-*+]\s+(.+)$/);
        if (!m) break;
        items.push(m[1]);
        i++;
      }
      blocks.push({ kind: 'bullet', items });
      continue;
    }

    // Ordered list — gather consecutive items
    if (/^\d+\.\s+/.test(trimmed)) {
      const items: string[] = [];
      while (i < lines.length) {
        const m = lines[i].trim().match(/^\d+\.\s+(.+)$/);
        if (!m) break;
        items.push(m[1]);
        i++;
      }
      blocks.push({ kind: 'ordered', items });
      continue;
    }

    // Paragraph — accumulate until blank line or next block marker
    const paraLines: string[] = [line];
    i++;
    while (i < lines.length) {
      const next = lines[i];
      if (!next.trim()) break;
      if (/^(#{1,3}\s|[-*+]\s|\d+\.\s)/.test(next.trim())) break;
      paraLines.push(next);
      i++;
    }
    const paragraphText = paraLines.join('\n');
    const embedUrl = asStandaloneEmbedUrl(paragraphText);
    if (embedUrl) {
      blocks.push({ kind: 'embed', url: embedUrl });
    } else {
      blocks.push({ kind: 'paragraph', text: paragraphText });
    }
  }

  return blocks;
}

// ── Block renderers ────────────────────────────────────────────────────────

function renderBlock(block: Block, key: number): React.ReactNode {
  switch (block.kind) {
    case 'heading': {
      const cls =
        block.level === 1
          ? 'text-[14px] font-semibold text-[#f5f7fb]'
          : block.level === 2
          ? 'text-[13px] font-semibold text-[#f5f7fb]'
          : 'text-[12px] font-semibold text-[rgba(245,247,251,0.9)]';
      const Tag = `h${block.level}` as 'h1' | 'h2' | 'h3';
      return React.createElement(Tag, { key, className: cls }, renderInline(block.text));
    }
    case 'bullet':
      return (
        <ul key={key} className="list-disc list-outside pl-5 space-y-1">
          {block.items.map((item, j) => (
            <li key={j}>{renderInline(item)}</li>
          ))}
        </ul>
      );
    case 'ordered':
      return (
        <ol key={key} className="list-decimal list-outside pl-5 space-y-1">
          {block.items.map((item, j) => (
            <li key={j}>{renderInline(item)}</li>
          ))}
        </ol>
      );
    case 'paragraph':
      // Preserve soft line breaks inside a paragraph
      return (
        <p key={key} className="whitespace-pre-wrap break-words">
          {renderInline(block.text)}
        </p>
      );
    case 'embed':
      return <LinkEmbed key={key} url={block.url} />;
  }
}

// ── Inline tokenizer ───────────────────────────────────────────────────────
// Walks the text once and emits React nodes. Supports bold, italic, code, links.

type Token =
  | { kind: 'text'; value: string }
  | { kind: 'bold'; value: string }
  | { kind: 'italic'; value: string }
  | { kind: 'code'; value: string }
  | { kind: 'link'; text: string; href: string };

function renderInline(text: string): React.ReactNode[] {
  const tokens = tokenizeInline(text);
  return tokens.map((t, i) => {
    switch (t.kind) {
      case 'text':
        return <React.Fragment key={i}>{t.value}</React.Fragment>;
      case 'bold':
        return <strong key={i} className="font-semibold text-[#f5f7fb]">{t.value}</strong>;
      case 'italic':
        return <em key={i} className="italic">{t.value}</em>;
      case 'code':
        return (
          <code key={i} className="px-1 py-0.5 rounded bg-[rgba(255,255,255,0.06)] font-mono text-[11px] text-[#c4b5fd]">
            {t.value}
          </code>
        );
      case 'link':
        return (
          <a
            key={i}
            href={t.href}
            target="_blank"
            rel="noreferrer"
            className="text-[#c4b5fd] underline underline-offset-2 hover:text-[#a78bfa]"
          >
            {t.text}
          </a>
        );
    }
  });
}

function tokenizeInline(text: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  let buffer = '';

  const flush = () => {
    if (buffer) {
      tokens.push({ kind: 'text', value: buffer });
      buffer = '';
    }
  };

  while (i < text.length) {
    // Inline code: `...`
    if (text[i] === '`') {
      const end = text.indexOf('`', i + 1);
      if (end > i) {
        flush();
        tokens.push({ kind: 'code', value: text.slice(i + 1, end) });
        i = end + 1;
        continue;
      }
    }

    // Bare URL autolink: http(s)://... stopping at whitespace or common terminators.
    // Must be at start of string or preceded by whitespace/punctuation so URLs
    // embedded inside other tokens aren't partially consumed.
    if ((text[i] === 'h' || text[i] === 'H') && (i === 0 || /[\s([{<]/.test(text[i - 1]))) {
      const match = text.slice(i).match(/^https?:\/\/[^\s<>"']+/i);
      if (match) {
        let href = match[0];
        // Strip trailing punctuation that is almost certainly not part of the URL.
        while (/[.,;:!?)\]]$/.test(href)) href = href.slice(0, -1);
        if (href.length > 8) {
          flush();
          tokens.push({ kind: 'link', text: href, href });
          i += href.length;
          continue;
        }
      }
    }

    // Link: [text](http(s)://url)
    if (text[i] === '[') {
      const closeBracket = text.indexOf(']', i + 1);
      if (closeBracket > i && text[closeBracket + 1] === '(') {
        const closeParen = text.indexOf(')', closeBracket + 2);
        if (closeParen > closeBracket) {
          const linkText = text.slice(i + 1, closeBracket);
          const rawHref = text.slice(closeBracket + 2, closeParen).trim();
          if (/^https?:\/\//i.test(rawHref)) {
            flush();
            tokens.push({ kind: 'link', text: linkText, href: rawHref });
            i = closeParen + 1;
            continue;
          }
        }
      }
    }

    // Bold: **text** or __text__
    if ((text[i] === '*' && text[i + 1] === '*') || (text[i] === '_' && text[i + 1] === '_')) {
      const marker = text.slice(i, i + 2);
      const end = text.indexOf(marker, i + 2);
      if (end > i) {
        flush();
        tokens.push({ kind: 'bold', value: text.slice(i + 2, end) });
        i = end + 2;
        continue;
      }
    }

    // Italic: *text* or _text_ (single char, and not part of a bold marker)
    if ((text[i] === '*' || text[i] === '_') && text[i + 1] !== text[i]) {
      const marker = text[i];
      // Find matching marker that isn't part of **/__
      let j = i + 1;
      while (j < text.length) {
        if (text[j] === marker && text[j - 1] !== '\\' && text[j + 1] !== marker) {
          break;
        }
        j++;
      }
      if (j < text.length && j > i + 1) {
        flush();
        tokens.push({ kind: 'italic', value: text.slice(i + 1, j) });
        i = j + 1;
        continue;
      }
    }

    buffer += text[i];
    i++;
  }

  flush();
  return tokens;
}
