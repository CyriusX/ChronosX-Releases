"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { SHOWCASE } from "@/lib/landing-data";
import {
  fadeUp,
  glowIn,
  staggerContainer,
  STAGGER,
  sectionTransition,
  TIMING,
  EASING,
} from "@/lib/animations";
import { MockupActivityChart } from "./mockups/mockup-activity-chart";
import { MockupFocusScore } from "./mockups/mockup-focus-score";
import { MockupProjectCards } from "./mockups/mockup-project-cards";
import { MockupTimer } from "./mockups/mockup-timer";
import { MockupTeamGrid } from "./mockups/mockup-team-grid";
import { MockupHeatmap } from "./mockups/mockup-heatmap";
import { MockupReports } from "./mockups/mockup-reports";

const mockupComponents: Record<string, React.ReactNode> = {
  dashboard: <MockupActivityChart />,
  team: <MockupTeamGrid />,
  projects: <MockupProjectCards />,
  timer: <MockupTimer />,
  analytics: <MockupHeatmap />,
  reports: <MockupReports />,
};

export function Showcase() {
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.1 });

  return (
    <section ref={ref} id="showcase" className="section-padding relative">
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
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-cyan uppercase">
            {SHOWCASE.eyebrow}
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {SHOWCASE.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          {SHOWCASE.description}
        </motion.p>

        {/* Showcase grid */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-3"
        >
          {SHOWCASE.items.map((item) => (
            <motion.div
              key={item.title}
              variants={glowIn}
              transition={{
                type: "tween",
                ease: EASING.easeOut,
                duration: TIMING.slow,
              }}
              className="group relative overflow-hidden rounded-2xl border border-border-subtle bg-bg-secondary/60 p-1 transition-all duration-300 hover:border-border-glow"
            >
              {/* Mockup */}
              <div className="pointer-events-none">
                {mockupComponents[item.mockup]}
              </div>

              {/* Label overlay */}
              <div className="px-4 pt-2 pb-4">
                <h3 className="font-heading text-sm font-semibold text-text-primary">
                  {item.title}
                </h3>
                <p className="mt-0.5 text-xs text-text-dim">{item.description}</p>
              </div>

              {/* Hover glow */}
              <div className="pointer-events-none absolute -bottom-6 -right-6 h-24 w-24 rounded-full bg-accent-blue/5 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
