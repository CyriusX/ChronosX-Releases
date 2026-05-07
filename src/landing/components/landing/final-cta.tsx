"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";
import { useLandingContent } from "./content-provider";
import { WaitlistForm } from "./waitlist-form";

export function FinalCta() {
  const { copy } = useLandingContent();
  const ref = useRef<HTMLDivElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.3 });

  return (
    <div ref={ref} className="relative py-20 md:py-28">
      {/* Background */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "linear-gradient(135deg, rgba(46, 99, 255, 0.08) 0%, rgba(124, 92, 255, 0.08) 50%, rgba(193, 92, 255, 0.05) 100%)",
        }}
      />
      <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent-blue/25 to-transparent" />
      <div className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-accent-violet/25 to-transparent" />

      {/* Central glow */}
      <div
        className="pointer-events-none absolute top-1/2 left-1/2 h-[400px] w-[600px] -translate-x-1/2 -translate-y-1/2 opacity-20"
        style={{
          background:
            "radial-gradient(ellipse, rgba(46, 99, 255, 0.3) 0%, transparent 70%)",
          filter: "blur(80px)",
        }}
      />

      <motion.div
        className="relative mx-auto max-w-3xl px-5 text-center md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {copy.finalCta.headline.split("\n").map((line, i) => (
            <span key={i}>
              {i > 0 && <br />}
              {line}
            </span>
          ))}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mt-5 text-base leading-relaxed text-text-muted md:text-lg"
        >
          {copy.finalCta.description}
        </motion.p>

        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-8 w-full max-w-2xl"
        >
          <WaitlistForm compact />
        </motion.div>
      </motion.div>
    </div>
  );
}
