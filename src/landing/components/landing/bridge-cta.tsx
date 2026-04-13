"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { ArrowRight } from "lucide-react";
import { BRIDGE_CTA } from "@/lib/landing-data";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";

export function BridgeCta() {
  const ref = useRef<HTMLDivElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.3 });

  return (
    <div ref={ref} className="relative py-16 md:py-24">
      {/* Gradient band background */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "linear-gradient(135deg, rgba(46, 99, 255, 0.06) 0%, rgba(124, 92, 255, 0.06) 50%, rgba(34, 211, 238, 0.04) 100%)",
        }}
      />
      <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent-blue/20 to-transparent" />
      <div className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-accent-violet/20 to-transparent" />

      <motion.div
        className="relative mx-auto max-w-3xl px-5 text-center md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="font-heading text-2xl leading-tight font-bold text-text-primary md:text-3xl lg:text-4xl"
        >
          {BRIDGE_CTA.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mt-4 text-base leading-relaxed text-text-muted"
        >
          {BRIDGE_CTA.description}
        </motion.p>

        <motion.div variants={fadeUp} transition={sectionTransition} className="mt-8">
          <a
            href="#pricing"
            className="pill-button inline-flex items-center gap-2 bg-accent-blue font-semibold text-white shadow-lg shadow-accent-blue/25 transition-all hover:bg-accent-blue/90 hover:shadow-xl hover:shadow-accent-blue/30"
          >
            {BRIDGE_CTA.ctaText}
            <ArrowRight className="h-4 w-4" />
          </a>
        </motion.div>
      </motion.div>
    </div>
  );
}
