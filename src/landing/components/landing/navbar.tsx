"use client";

import { useState, useEffect } from "react";
import { motion, AnimatePresence } from "motion/react";
import { Menu, X, Clock } from "lucide-react";
import { cn } from "@/lib/cn";
import { NAV_ITEMS } from "@/lib/landing-data";

export function Navbar() {
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

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
          <a href="#home" className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-blue/20">
              <Clock className="h-4.5 w-4.5 text-accent-blue" />
            </div>
            <span className="font-heading text-lg font-bold text-text-primary">
              Chronos<span className="text-accent-blue">X</span>
            </span>
          </a>

          {/* Desktop nav links */}
          <div className="hidden items-center gap-8 md:flex">
            {NAV_ITEMS.map((item) => (
              <a
                key={item.href}
                href={item.href}
                className="text-sm font-medium text-text-muted transition-colors hover:text-text-primary"
              >
                {item.label}
              </a>
            ))}
          </div>

          {/* Desktop CTA */}
          <div className="hidden md:block">
            <a
              href="#pricing"
              className="pill-button inline-flex items-center border border-accent-blue/40 text-sm font-medium text-accent-blue transition-all hover:border-accent-blue hover:bg-accent-blue/10 hover:shadow-[0_0_20px_rgba(46,99,255,0.15)]"
            >
              Get Started
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
              <div className="flex flex-col items-center gap-5">
                {NAV_ITEMS.map((item) => (
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
              <a
                href="#pricing"
                onClick={() => setMobileOpen(false)}
                className="pill-button mt-4 inline-flex items-center bg-accent-blue px-8 py-3 text-base font-semibold text-white transition-all hover:bg-accent-blue/90"
              >
                Get Started
              </a>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}
