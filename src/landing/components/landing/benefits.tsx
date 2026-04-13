"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import {
  FileBarChart,
  Shield,
  Calendar,
  Target,
  Clock,
} from "lucide-react";
import { BENEFITS } from "@/lib/landing-data";
import {
  fadeUp,
  glowIn,
  staggerContainer,
  STAGGER,
  sectionTransition,
  TIMING,
  EASING,
} from "@/lib/animations";

const iconMap: Record<string, React.ReactNode> = {
  FileBarChart: <FileBarChart className="h-5 w-5" />,
  Shield: <Shield className="h-5 w-5" />,
  Calendar: <Calendar className="h-5 w-5" />,
  Target: <Target className="h-5 w-5" />,
  Clock: <Clock className="h-5 w-5" />,
};

export function Benefits() {
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.15 });

  const leadBenefit = BENEFITS.find((b) => b.highlight);
  const otherBenefits = BENEFITS.filter((b) => !b.highlight);

  return (
    <section ref={ref} className="section-padding relative">
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
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-magenta uppercase">
            Why ChronosX
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          Outcomes that matter for your team
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          ChronosX doesn&apos;t just track time — it transforms how teams understand
          and improve their work.
        </motion.p>

        {/* Lead benefit */}
        {leadBenefit && (
          <motion.div
            variants={glowIn}
            transition={{ type: "tween", ease: EASING.easeOut, duration: TIMING.slow }}
            className="mt-14 overflow-hidden rounded-2xl border border-accent-blue/20 bg-gradient-to-br from-accent-blue/8 to-accent-violet/5 p-8 md:p-10"
          >
            <div className="flex flex-col items-start gap-4 md:flex-row md:items-center md:gap-8">
              <div className="inline-flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl border border-accent-blue/20 bg-accent-blue/10 text-accent-blue">
                {iconMap[leadBenefit.icon]}
              </div>
              <div>
                <h3 className="mb-2 font-heading text-xl font-semibold text-text-primary md:text-2xl">
                  {leadBenefit.title}
                </h3>
                <p className="max-w-2xl text-base leading-relaxed text-text-muted">
                  {leadBenefit.description}
                </p>
              </div>
            </div>
          </motion.div>
        )}

        {/* Supporting benefits */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-6 grid gap-5 sm:grid-cols-2"
        >
          {otherBenefits.map((benefit) => (
            <motion.div
              key={benefit.title}
              variants={glowIn}
              transition={{
                type: "tween",
                ease: EASING.easeOut,
                duration: TIMING.slow,
              }}
              className="group rounded-2xl border border-border-subtle bg-card/50 p-6 transition-all duration-300 hover:border-border-glow hover:shadow-[0_0_30px_rgba(46,99,255,0.06)]"
            >
              <div className="mb-4 inline-flex h-10 w-10 items-center justify-center rounded-xl border border-border-subtle bg-bg-tertiary/60 text-accent-violet">
                {iconMap[benefit.icon]}
              </div>
              <h3 className="mb-2 font-heading text-lg font-semibold text-text-primary">
                {benefit.title}
              </h3>
              <p className="text-sm leading-relaxed text-text-muted">
                {benefit.description}
              </p>
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
