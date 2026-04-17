"use client";

import { Clock, ArrowUp, Mail } from "lucide-react";
import { useLandingContent } from "./content-provider";

export function Footer() {
  const { copy, lang } = useLandingContent();
  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  return (
    <footer className="relative border-t border-border-subtle bg-bg-secondary/50">
      <div className="mx-auto max-w-7xl px-5 pt-16 pb-8 md:px-8">
        <div className="flex flex-col items-start justify-between gap-10 md:flex-row md:items-center">
          {/* Brand */}
          <div>
            <a href={`?lang=${lang}#home`} className="mb-4 flex items-center gap-2">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-blue/20">
                <Clock className="h-4.5 w-4.5 text-accent-blue" />
              </div>
              <span className="font-heading text-lg font-bold text-text-primary">
                Chronos<span className="text-accent-blue">X</span>
              </span>
            </a>
            <p className="max-w-xl text-sm leading-relaxed text-text-dim">
              {copy.footer.blurb}
            </p>
            <a
              href={copy.footer.contactHref}
              className="mt-5 inline-flex items-center gap-2 text-sm font-medium text-text-muted transition-colors hover:text-text-primary"
            >
              <Mail className="h-4 w-4 text-accent-blue" />
              {copy.footer.contactLabel}
            </a>
          </div>

          {/* Quick links */}
          <div className="flex flex-wrap gap-x-8 gap-y-3">
            {copy.nav.items.map((item) => (
              <a
                key={item.href}
                href={item.href}
                className="text-sm font-medium text-text-dim transition-colors hover:text-text-muted"
              >
                {item.label}
              </a>
            ))}
          </div>
        </div>

        {/* Bottom bar */}
        <div className="mt-14 flex flex-col items-center justify-between gap-4 border-t border-border-subtle pt-6 sm:flex-row">
          <p className="text-xs text-text-dim">
            &copy; {new Date().getFullYear()} ChronosX TimeTrack. All rights
            reserved.
          </p>
          <button
            onClick={scrollToTop}
            aria-label="Back to top"
            className="flex items-center gap-1.5 text-xs text-text-dim transition-colors hover:text-text-muted"
          >
            Back to top
            <ArrowUp className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </footer>
  );
}
