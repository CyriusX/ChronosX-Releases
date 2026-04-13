"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { ArrowRight, Sparkles } from "lucide-react";
import { HERO } from "@/lib/landing-data";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";
import { MockupDashboard } from "./mockups/mockup-dashboard";

export function Hero() {
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.1 });

  return (
    <section
      ref={ref}
      id="home"
      className="relative overflow-hidden pt-28 pb-12 md:pt-36 md:pb-20"
    >
      {/* Hero-specific background glow */}
      <div
        className="pointer-events-none absolute top-0 left-1/2 h-[700px] w-[900px] -translate-x-1/2 opacity-20"
        style={{
          background:
            "radial-gradient(ellipse at center top, rgba(46, 99, 255, 0.3) 0%, rgba(124, 92, 255, 0.15) 40%, transparent 70%)",
          filter: "blur(60px)",
        }}
      />

      <motion.div
        className="relative z-10 mx-auto max-w-7xl px-5 md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        {/* Eyebrow */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mb-6 flex justify-center"
        >
          <div className="inline-flex items-center gap-2 rounded-full border border-accent-violet/30 bg-accent-violet/10 px-4 py-1.5">
            <Sparkles className="h-3.5 w-3.5 text-accent-violet" />
            <span className="text-xs font-medium text-accent-violet">
              {HERO.eyebrow}
            </span>
          </div>
        </motion.div>

        {/* Headline */}
        <motion.h1
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-4xl text-center font-heading text-4xl leading-[1.1] font-bold tracking-tight text-text-primary sm:text-5xl md:text-6xl lg:text-7xl"
        >
          {HERO.headline.split("\n").map((line, i) => (
            <span key={i}>
              {i > 0 && <br />}
              {i === 1 ? (
                <span className="gradient-text-accent">{line}</span>
              ) : (
                line
              )}
            </span>
          ))}
        </motion.h1>

        {/* Subheadline */}
        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-6 max-w-2xl text-center text-base leading-relaxed text-text-muted md:text-lg"
        >
          {HERO.subheadline}
        </motion.p>

        {/* CTA Form */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-8 flex max-w-md flex-col items-center gap-3 sm:flex-row"
        >
          <div className="relative w-full flex-1">
            <input
              type="email"
              placeholder={HERO.inputPlaceholder}
              className="w-full rounded-full border border-border-subtle bg-card/60 px-5 py-3 text-sm text-text-primary placeholder:text-text-dim outline-none transition-all focus:border-accent-blue/50 focus:ring-2 focus:ring-accent-blue/20"
            />
          </div>
          <button className="pill-button inline-flex w-full items-center justify-center gap-2 bg-accent-blue font-semibold text-white shadow-lg shadow-accent-blue/25 transition-all hover:bg-accent-blue/90 hover:shadow-xl hover:shadow-accent-blue/30 sm:w-auto">
            {HERO.ctaText}
            <ArrowRight className="h-4 w-4" />
          </button>
        </motion.div>

        {/* Dashboard mockup */}
        <motion.div
          variants={fadeUp}
          transition={{ ...sectionTransition, duration: 0.7, delay: 0.2 }}
          className="mt-14 md:mt-20"
          style={{
            perspective: "1200px",
          }}
        >
          <div
            style={{
              transform: "rotateX(2deg)",
            }}
          >
            <MockupDashboard />
          </div>
        </motion.div>
      </motion.div>
    </section>
  );
}
