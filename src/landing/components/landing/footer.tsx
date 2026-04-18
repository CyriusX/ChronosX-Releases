"use client";

import Image from "next/image";
import { ArrowUp, Mail } from "lucide-react";
import { useLandingContent } from "./content-provider";

export function Footer() {
  const { copy, lang, audience } = useLandingContent();
  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const homeHref = `/${audience === "teams" ? "teams" : ""}?lang=${lang}#home`;
  const brandLabel = `${copy.brand.name} ${copy.brand.product}`;

  return (
    <footer className="relative border-t border-border-subtle bg-bg-secondary/50">
      <div className="mx-auto max-w-7xl px-5 pt-16 pb-8 md:px-8">
        <div className="flex flex-col items-start justify-between gap-10 md:flex-row md:items-center">
          {/* Brand */}
          <div>
            <a href={homeHref} className="mb-4 flex items-center gap-2">
              <Image
                src="/brand/logo-64.png"
                alt={copy.brand.name}
                width={32}
                height={32}
                className="h-8 w-8 rounded-lg shadow-[0px_6px_10px_0px_rgba(139,92,246,0.25)]"
              />
              <div className="leading-tight">
                <div className="font-heading text-lg font-semibold text-text-primary tracking-[-0.3px]">
                  {copy.brand.name}
                </div>
                <div className="text-xs text-text-dim">{copy.brand.product}</div>
              </div>
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
            &copy; {new Date().getFullYear()} {brandLabel}.{" "}
            {lang === "pt" ? "Todos os direitos reservados." : "All rights reserved."}
          </p>
          <button
            onClick={scrollToTop}
            aria-label="Back to top"
            className="flex items-center gap-1.5 text-xs text-text-dim transition-colors hover:text-text-muted"
          >
            {lang === "pt" ? "Voltar ao topo" : "Back to top"}
            <ArrowUp className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </footer>
  );
}
