"use client";

import { useState, useEffect } from "react";
import { motion, AnimatePresence } from "motion/react";
import { Menu, X } from "lucide-react";
import { cn } from "@/lib/cn";
import { usePathname } from "next/navigation";
import { useLandingContent } from "./content-provider";
import Image from "next/image";

export function Navbar() {
  const pathname = usePathname();
  const { copy, lang, setLang } = useLandingContent();
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const isTeams = pathname?.startsWith("/teams");

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 20);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    if (mobileOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => {
      document.body.style.overflow = "";
    };
  }, [mobileOpen]);

  return (
    <>
      <header
        className={cn(
          "fixed top-0 right-0 left-0 z-50 transition-all duration-300",
          scrolled
            ? "border-b border-border-subtle bg-bg-primary/80 backdrop-blur-xl"
            : "bg-transparent"
        )}
      >
        <nav className="mx-auto flex max-w-7xl items-center justify-between px-5 py-4 md:px-8">
          {/* Logo */}
          <a href={`/${isTeams ? "teams" : ""}?lang=${lang}#home`} className="flex items-center gap-2">
            <Image
              src="/brand/logo-64.png"
              alt="ChronosX"
              width={32}
              height={32}
              className="h-8 w-8 rounded-lg shadow-[0px_6px_10px_0px_rgba(139,92,246,0.25)]"
              priority
            />
            <span className="text-[15px] font-semibold text-[#f5f7fb] tracking-[-0.3px]">
              ChronosX
            </span>
          </a>

          <div className="hidden items-center gap-4 md:flex">
            {/* Audience switch */}
            <div className="inline-flex items-center rounded-full border border-border-subtle bg-card/30 p-1">
              <a
                href={`/?lang=${lang}`}
                className={cn(
                  "rounded-full px-3 py-1.5 text-sm font-medium transition-colors",
                  !isTeams
                    ? "bg-accent-blue/15 text-text-primary"
                    : "text-text-muted hover:text-text-primary"
                )}
              >
                {copy.nav.individualsLabel}
              </a>
              <a
                href={`/teams?lang=${lang}`}
                className={cn(
                  "rounded-full px-3 py-1.5 text-sm font-medium transition-colors",
                  isTeams
                    ? "bg-accent-blue/15 text-text-primary"
                    : "text-text-muted hover:text-text-primary"
                )}
              >
                {copy.nav.teamsLabel}
              </a>
            </div>

            {/* Desktop nav links */}
            <div className="flex items-center gap-7">
              {copy.nav.items.map((item) => (
                <a
                  key={item.href}
                  href={item.href}
                  className="text-sm font-medium text-text-muted transition-colors hover:text-text-primary"
                >
                  {item.label}
                </a>
              ))}
            </div>
          </div>

          {/* Desktop CTA */}
          <div className="hidden items-center gap-3 md:flex">
            {/* Language toggle */}
            <div className="inline-flex items-center rounded-full border border-border-subtle bg-card/30 p-1">
              <button
                type="button"
                onClick={() => setLang("en")}
                className={cn(
                  "rounded-full px-2.5 py-1 text-xs font-semibold tracking-wide transition-colors",
                  lang === "en"
                    ? "bg-card/70 text-text-primary"
                    : "text-text-dim hover:text-text-muted"
                )}
                aria-label="Switch language to English"
              >
                EN
              </button>
              <button
                type="button"
                onClick={() => setLang("pt")}
                className={cn(
                  "rounded-full px-2.5 py-1 text-xs font-semibold tracking-wide transition-colors",
                  lang === "pt"
                    ? "bg-card/70 text-text-primary"
                    : "text-text-dim hover:text-text-muted"
                )}
                aria-label="Switch language to Portuguese"
              >
                PT-BR
              </button>
            </div>

            <a
              href="#waitlist"
              className="pill-button inline-flex items-center border border-accent-blue/40 text-sm font-medium text-accent-blue transition-all hover:border-accent-blue hover:bg-accent-blue/10 hover:shadow-[0_0_20px_rgba(46,99,255,0.15)]"
            >
              {copy.nav.cta}
            </a>
          </div>

          {/* Mobile hamburger */}
          <button
            className="relative z-50 flex h-10 w-10 items-center justify-center rounded-xl border border-border-subtle md:hidden"
            onClick={() => setMobileOpen(!mobileOpen)}
            aria-label={mobileOpen ? "Close menu" : "Open menu"}
          >
            {mobileOpen ? (
              <X className="h-5 w-5 text-text-primary" />
            ) : (
              <Menu className="h-5 w-5 text-text-primary" />
            )}
          </button>
        </nav>
      </header>

      {/* Mobile menu */}
      <AnimatePresence>
        {mobileOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="fixed inset-0 z-40 bg-bg-primary/95 backdrop-blur-xl md:hidden"
          >
            <div className="flex h-full flex-col items-center justify-center gap-6">
              {/* Audience switch */}
              <div className="inline-flex items-center rounded-full border border-border-subtle bg-card/30 p-1">
                <a
                  href={`/?lang=${lang}`}
                  onClick={() => setMobileOpen(false)}
                  className={cn(
                    "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
                    !isTeams
                      ? "bg-accent-blue/15 text-text-primary"
                      : "text-text-muted hover:text-text-primary"
                  )}
                >
                  {copy.nav.individualsLabel}
                </a>
                <a
                  href={`/teams?lang=${lang}`}
                  onClick={() => setMobileOpen(false)}
                  className={cn(
                    "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
                    isTeams
                      ? "bg-accent-blue/15 text-text-primary"
                      : "text-text-muted hover:text-text-primary"
                  )}
                >
                  {copy.nav.teamsLabel}
                </a>
              </div>

              <div className="flex flex-col items-center gap-5">
                {copy.nav.items.map((item) => (
                  <a
                    key={item.href}
                    href={item.href}
                    onClick={() => setMobileOpen(false)}
                    className="font-heading text-2xl font-semibold text-text-primary transition-colors hover:text-accent-blue"
                  >
                    {item.label}
                  </a>
                ))}
              </div>

              {/* Language toggle */}
              <div className="mt-2 inline-flex items-center rounded-full border border-border-subtle bg-card/30 p-1">
                <button
                  type="button"
                  onClick={() => setLang("en")}
                  className={cn(
                    "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
                    lang === "en"
                      ? "bg-card/70 text-text-primary"
                      : "text-text-muted hover:text-text-primary"
                  )}
                >
                  EN
                </button>
                <button
                  type="button"
                  onClick={() => setLang("pt")}
                  className={cn(
                    "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
                    lang === "pt"
                      ? "bg-card/70 text-text-primary"
                      : "text-text-muted hover:text-text-primary"
                  )}
                >
                  PT-BR
                </button>
              </div>
              <a
                href="#waitlist"
                onClick={() => setMobileOpen(false)}
                className="pill-button mt-4 inline-flex items-center bg-accent-blue px-8 py-3 text-base font-semibold text-white transition-all hover:bg-accent-blue/90"
              >
                {copy.nav.cta}
              </a>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}
