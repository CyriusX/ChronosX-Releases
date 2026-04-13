"use client";

import { Clock, ArrowUp, Globe, MessageCircle, AtSign } from "lucide-react";
import { FOOTER_LINKS } from "@/lib/landing-data";

export function Footer() {
  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  return (
    <footer className="relative border-t border-border-subtle bg-bg-secondary/50">
      <div className="mx-auto max-w-7xl px-5 pt-16 pb-8 md:px-8">
        <div className="grid gap-12 md:grid-cols-6">
          {/* Brand column */}
          <div className="md:col-span-2">
            <a href="#home" className="mb-4 flex items-center gap-2">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-blue/20">
                <Clock className="h-4.5 w-4.5 text-accent-blue" />
              </div>
              <span className="font-heading text-lg font-bold text-text-primary">
                Chronos<span className="text-accent-blue">X</span>
              </span>
            </a>
            <p className="mt-3 max-w-xs text-sm leading-relaxed text-text-dim">
              Time tracking and productivity visibility for individuals and
              teams. See where your time goes. Work with more focus.
            </p>

            {/* Newsletter */}
            <div className="mt-6">
              <p className="mb-2 text-xs font-medium text-text-muted">
                Stay updated
              </p>
              <div className="flex gap-2">
                <input
                  type="email"
                  placeholder="your@email.com"
                  className="w-full max-w-[200px] rounded-lg border border-border-subtle bg-card/40 px-3 py-2 text-xs text-text-primary placeholder:text-text-dim outline-none transition-all focus:border-accent-blue/40"
                />
                <button className="rounded-lg bg-accent-blue/20 px-3 py-2 text-xs font-medium text-accent-blue transition-colors hover:bg-accent-blue/30">
                  Subscribe
                </button>
              </div>
            </div>

            {/* Socials */}
            <div className="mt-5 flex gap-3">
              {[
                { icon: <Globe className="h-4 w-4" />, label: "Website" },
                { icon: <MessageCircle className="h-4 w-4" />, label: "Community" },
                { icon: <AtSign className="h-4 w-4" />, label: "Contact" },
              ].map((social) => (
                <a
                  key={social.label}
                  href="#"
                  aria-label={social.label}
                  className="flex h-9 w-9 items-center justify-center rounded-lg border border-border-subtle bg-card/30 text-text-dim transition-all hover:border-accent-blue/30 hover:text-text-muted"
                >
                  {social.icon}
                </a>
              ))}
            </div>
          </div>

          {/* Link columns */}
          {FOOTER_LINKS.map((group) => (
            <div key={group.title}>
              <h4 className="mb-4 text-sm font-semibold text-text-primary">
                {group.title}
              </h4>
              <ul className="space-y-2.5">
                {group.links.map((link) => (
                  <li key={link.label}>
                    <a
                      href={link.href}
                      className="text-sm text-text-dim transition-colors hover:text-text-muted"
                    >
                      {link.label}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          ))}
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
