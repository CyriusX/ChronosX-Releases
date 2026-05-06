"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { Sparkles, CheckCircle2 } from "lucide-react";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";
import { useLandingContent } from "./content-provider";
import { WaitlistForm } from "./waitlist-form";

export function Hero() {
  const { copy } = useLandingContent();
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
              {copy.hero.eyebrow}
            </span>
          </div>
        </motion.div>

        {/* Headline */}
        <motion.h1
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-4xl text-center font-heading text-4xl leading-[1.1] font-bold tracking-tight text-text-primary sm:text-5xl md:text-6xl lg:text-7xl"
        >
          {copy.hero.headline.split("\n").map((line, i) => (
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
          {copy.hero.subheadline}
        </motion.p>

        {/* Bullets */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-6 grid max-w-2xl gap-2 text-left sm:grid-cols-3 sm:gap-3"
        >
          {copy.hero.bullets.map((b) => (
            <div
              key={b}
              className="flex items-start gap-2 rounded-xl border border-border-subtle bg-card/25 px-4 py-3 text-sm text-text-muted"
            >
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-accent-cyan" />
              <span>{b}</span>
            </div>
          ))}
        </motion.div>

        {/* CTA Form */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-8 w-full max-w-2xl"
        >
          <div id="waitlist" className="scroll-mt-28" />
          <WaitlistForm />
        </motion.div>

      </motion.div>
    </section>
  );
}
