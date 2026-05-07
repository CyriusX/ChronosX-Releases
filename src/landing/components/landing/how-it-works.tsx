"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { Play, Monitor, BarChart3, Download } from "lucide-react";
import {
  fadeUp,
  staggerContainer,
  STAGGER,
  sectionTransition,
  TIMING,
  EASING,
} from "@/lib/animations";
import { useLandingContent } from "./content-provider";

const iconMap: Record<string, React.ReactNode> = {
  Play: <Play className="h-6 w-6" />,
  Monitor: <Monitor className="h-6 w-6" />,
  BarChart3: <BarChart3 className="h-6 w-6" />,
  Download: <Download className="h-6 w-6" />,
};

const stepColors = ["text-accent-blue", "text-accent-violet", "text-accent-cyan"];
const stepBgColors = [
  "bg-accent-blue/10 border-accent-blue/20",
  "bg-accent-violet/10 border-accent-violet/20",
  "bg-accent-cyan/10 border-accent-cyan/20",
];

export function HowItWorks() {
  const { copy } = useLandingContent();
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.2 });

  return (
    <section ref={ref} id="how-it-works" className="section-padding relative">
      {/* Background panel */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "linear-gradient(180deg, rgba(46, 99, 255, 0.04) 0%, rgba(124, 92, 255, 0.04) 50%, transparent 100%)",
        }}
      />
      <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent-blue/15 to-transparent" />

      <motion.div
        className="relative mx-auto max-w-7xl px-5 md:px-8"
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
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-cyan uppercase">
            {copy.howItWorks.eyebrow}
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {copy.howItWorks.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          {copy.howItWorks.description}
        </motion.p>

        {/* Steps */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="relative mt-16 grid gap-8 md:grid-cols-3"
        >
          {/* Connecting line (desktop) */}
          <div className="pointer-events-none absolute top-14 right-[16.6%] left-[16.6%] hidden h-px bg-gradient-to-r from-accent-blue/20 via-accent-violet/20 to-accent-cyan/20 md:block" />

          {copy.howItWorks.steps.map((step, i) => (
            <motion.div
              key={step.number}
              variants={fadeUp}
              transition={{
                type: "tween",
                ease: EASING.easeOut,
                duration: TIMING.slow,
              }}
              className="relative flex flex-col items-center text-center"
            >
              {/* Number circle */}
              <div
                className={`relative z-10 mb-6 flex h-14 w-14 items-center justify-center rounded-2xl border ${stepBgColors[i]} ${stepColors[i]}`}
              >
                {iconMap[step.icon] ?? <Play className="h-6 w-6" />}
              </div>

              {/* Step number */}
              <div
                className={`mb-2 font-heading text-xs font-bold tracking-[0.2em] uppercase ${stepColors[i]}`}
              >
                Step {step.number}
              </div>

              {/* Title */}
              <h3 className="mb-2 font-heading text-xl font-semibold text-text-primary">
                {step.title}
              </h3>

              {/* Description */}
              <p className="max-w-xs text-sm leading-relaxed text-text-muted">
                {step.description}
              </p>
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
