"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { ArrowRight } from "lucide-react";
import { ABOUT } from "@/lib/landing-data";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";

export function About() {
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.2 });

  return (
    <section
      ref={ref}
      id="about"
      className="section-padding relative"
    >
      <motion.div
        className="mx-auto max-w-7xl px-5 md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        {/* Eyebrow */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mb-4 text-center"
        >
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-blue uppercase">
            {ABOUT.eyebrow}
          </span>
        </motion.div>

        {/* Headline */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {ABOUT.headline}
        </motion.h2>

        {/* Description */}
        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-6 max-w-2xl text-center text-base leading-relaxed text-text-muted"
        >
          {ABOUT.description}
        </motion.p>

        {/* CTA */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mt-8 flex justify-center"
        >
          <a
            href={ABOUT.ctaHref}
            className="pill-button inline-flex items-center gap-2 border border-accent-blue/40 text-sm font-medium text-accent-blue transition-all hover:border-accent-blue hover:bg-accent-blue/10"
          >
            {ABOUT.ctaText}
            <ArrowRight className="h-3.5 w-3.5" />
          </a>
        </motion.div>

        {/* Badges */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mt-12 flex flex-wrap justify-center gap-3"
        >
          {ABOUT.badges.map((badge) => (
            <div
              key={badge}
              className="inline-flex items-center gap-2 rounded-full border border-border-subtle bg-card/50 px-4 py-2 text-sm text-text-muted transition-all hover:border-accent-blue/30 hover:text-text-primary"
            >
              <div className="h-1.5 w-1.5 rounded-full bg-accent-blue" />
              {badge}
            </div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
