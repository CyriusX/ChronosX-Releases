"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import {
  Rocket,
  Settings,
  Code,
  Shield,
  Palette,
  Building2,
  Target,
} from "lucide-react";
import { useLandingContent } from "./content-provider";
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
  Rocket: <Rocket className="h-5 w-5" />,
  Settings: <Settings className="h-5 w-5" />,
  Code: <Code className="h-5 w-5" />,
  Shield: <Shield className="h-5 w-5" />,
  Palette: <Palette className="h-5 w-5" />,
  Building2: <Building2 className="h-5 w-5" />,
  Target: <Target className="h-5 w-5" />,
};

export function DesignedFor() {
  const { copy } = useLandingContent();
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.15 });

  return (
    <section ref={ref} className="section-padding relative overflow-hidden">
      <motion.div
        className="mx-auto max-w-7xl px-5 md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mb-4 text-center"
        >
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-blue uppercase">
            {copy.designedFor.eyebrow}
          </span>
        </motion.div>

        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {copy.designedFor.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-2xl text-center text-base leading-relaxed text-text-muted"
        >
          {copy.designedFor.description}
        </motion.p>

        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-14 grid gap-6 sm:grid-cols-2 lg:grid-cols-4"
        >
          {copy.designedFor.items.map((item) => (
            <motion.div
              key={item.title}
              variants={glowIn}
              transition={{
                type: "tween",
                ease: EASING.easeOut,
                duration: TIMING.slow,
              }}
              className="group relative overflow-hidden rounded-2xl border border-border-subtle bg-card/40 p-6 transition-all duration-300 hover:border-border-glow"
            >
              <div className="relative z-10">
                <div className="inline-flex h-11 w-11 items-center justify-center rounded-2xl border border-border-subtle bg-bg-tertiary/50 text-accent-violet">
                  {iconMap[item.icon] ?? <Rocket className="h-5 w-5" />}
                </div>
                <h3 className="mt-4 font-heading text-base font-semibold text-text-primary">
                  {item.title}
                </h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">
                  {item.description}
                </p>
              </div>
              <div className="pointer-events-none absolute -bottom-10 -right-10 h-36 w-36 rounded-full bg-accent-violet/5 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}

