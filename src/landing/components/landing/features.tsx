"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import {
  Activity,
  Brain,
  Users,
  FileBarChart,
  Shield,
} from "lucide-react";
import {
  fadeUp,
  glowIn,
  staggerContainer,
  STAGGER,
  sectionTransition,
  TIMING,
  EASING,
} from "@/lib/animations";
import { useLandingContent } from "./content-provider";

const iconMap: Record<string, React.ReactNode> = {
  Activity: <Activity className="h-6 w-6" />,
  Brain: <Brain className="h-6 w-6" />,
  Users: <Users className="h-6 w-6" />,
  FileBarChart: <FileBarChart className="h-6 w-6" />,
  Shield: <Shield className="h-6 w-6" />,
};

const cardGradients = [
  "from-accent-blue/10 to-transparent",
  "from-accent-violet/10 to-transparent",
  "from-accent-cyan/10 to-transparent",
];

const iconColors = ["text-accent-blue", "text-accent-violet", "text-accent-cyan"];

export function Features() {
  const { copy } = useLandingContent();
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.2 });

  return (
    <section ref={ref} id="product" className="section-padding relative">
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
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-violet uppercase">
            {copy.features.eyebrow}
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {copy.features.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          {copy.features.description}
        </motion.p>

        {/* Feature cards */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-14 grid gap-6 md:grid-cols-3"
        >
          {copy.features.items.map((feature, i) => (
            <motion.div
              key={feature.title}
              variants={glowIn}
              transition={{
                type: "tween",
                ease: EASING.easeOut,
                duration: TIMING.slow,
              }}
              className="group relative overflow-hidden rounded-2xl border border-border-subtle bg-card/60 p-8 transition-all duration-300 hover:border-border-glow hover:shadow-[0_0_40px_rgba(46,99,255,0.08)]"
            >
              {/* Card gradient bg */}
              <div
                className={`pointer-events-none absolute inset-0 bg-gradient-to-br ${cardGradients[i]} opacity-50`}
              />

              <div className="relative z-10">
                {/* Icon */}
                <div
                  className={`mb-5 inline-flex h-12 w-12 items-center justify-center rounded-xl border border-border-subtle bg-bg-tertiary/60 ${iconColors[i]}`}
                >
                  {iconMap[feature.icon] ?? <Activity className="h-6 w-6" />}
                </div>

                {/* Title */}
                <h3 className="mb-3 font-heading text-xl font-semibold text-text-primary">
                  {feature.title}
                </h3>

                {/* Description */}
                <p className="text-sm leading-relaxed text-text-muted">
                  {feature.description}
                </p>
              </div>

              {/* Hover glow effect */}
              <div className="pointer-events-none absolute -bottom-8 -right-8 h-32 w-32 rounded-full bg-accent-blue/5 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
