"use client";

import { useRef, useEffect, useState } from "react";
import { motion, useInView } from "motion/react";
import { Star } from "lucide-react";
import { STATS, TRUST_LOGOS } from "@/lib/landing-data";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";

function AnimatedCounter({
  value,
  suffix,
  isInView,
}: {
  value: number;
  suffix: string;
  isInView: boolean;
}) {
  const [current, setCurrent] = useState(0);

  useEffect(() => {
    if (!isInView) return;
    const duration = 1500;
    const steps = 40;
    const increment = value / steps;
    let step = 0;

    const timer = setInterval(() => {
      step++;
      if (step >= steps) {
        setCurrent(value);
        clearInterval(timer);
      } else {
        setCurrent(Number((increment * step).toFixed(1)));
      }
    }, duration / steps);

    return () => clearInterval(timer);
  }, [isInView, value]);

  const display =
    value >= 100 ? Math.round(current).toLocaleString() : current.toFixed(1);

  return (
    <span>
      {display}
      {suffix}
    </span>
  );
}

export function Stats() {
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.2 });

  return (
    <section ref={ref} className="section-padding relative">
      {/* Background glow */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "linear-gradient(180deg, rgba(46, 99, 255, 0.03) 0%, transparent 40%, transparent 60%, rgba(124, 92, 255, 0.03) 100%)",
        }}
      />

      <motion.div
        className="relative mx-auto max-w-7xl px-5 md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        {/* Star rating */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mb-6 flex flex-col items-center gap-2"
        >
          <div className="flex gap-1">
            {[...Array(5)].map((_, i) => (
              <Star
                key={i}
                className="h-5 w-5 fill-amber-400 text-amber-400"
              />
            ))}
          </div>
          {/* TODO: Replace with real review data */}
          <p className="text-sm text-text-muted">
            4.9/5 based on 120+ reviews
          </p>
        </motion.div>

        {/* Stats grid */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="grid grid-cols-2 gap-6 md:grid-cols-4"
        >
          {STATS.map((stat) => (
            <motion.div
              key={stat.label}
              variants={fadeUp}
              transition={sectionTransition}
              className="rounded-2xl border border-border-subtle bg-card/40 p-6 text-center"
            >
              <div className="font-heading text-3xl font-bold text-text-primary md:text-4xl">
                <AnimatedCounter
                  value={stat.value}
                  suffix={stat.suffix}
                  isInView={isInView}
                />
              </div>
              <div className="mt-1 text-sm text-text-muted">{stat.label}</div>
            </motion.div>
          ))}
        </motion.div>

        {/* Trust logos */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mt-14"
        >
          {/* TODO: Replace with real company logos */}
          <p className="mb-6 text-center text-xs font-medium tracking-[0.15em] text-text-dim uppercase">
            Trusted by teams at
          </p>
          <div className="flex flex-wrap items-center justify-center gap-8">
            {TRUST_LOGOS.map((name) => (
              <div
                key={name}
                className="rounded-lg border border-border-subtle bg-card/30 px-5 py-2.5 text-sm font-medium text-text-dim transition-colors hover:text-text-muted"
              >
                {name}
              </div>
            ))}
          </div>
        </motion.div>
      </motion.div>
    </section>
  );
}
