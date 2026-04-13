"use client";

import { useRef, useState } from "react";
import { motion, useInView } from "motion/react";
import { Check, ArrowRight } from "lucide-react";
import { PRICING_TIERS } from "@/lib/landing-data";
import {
  fadeUp,
  glowIn,
  staggerContainer,
  STAGGER,
  sectionTransition,
  TIMING,
  EASING,
} from "@/lib/animations";

export function Pricing() {
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.15 });
  const [isAnnual, setIsAnnual] = useState(false);

  return (
    <section ref={ref} id="pricing" className="section-padding relative">
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
            Pricing
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          Simple, transparent pricing
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          Start free, upgrade when you need team features. No hidden fees.
        </motion.p>

        {/* Toggle */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mt-10 flex items-center justify-center gap-3"
        >
          <span
            className={`text-sm font-medium transition-colors ${
              !isAnnual ? "text-text-primary" : "text-text-dim"
            }`}
          >
            Monthly
          </span>
          <button
            onClick={() => setIsAnnual(!isAnnual)}
            className={`relative h-7 w-12 rounded-full border transition-all ${
              isAnnual
                ? "border-accent-blue/50 bg-accent-blue/20"
                : "border-border-subtle bg-card/60"
            }`}
            role="switch"
            aria-checked={isAnnual}
            aria-label="Toggle annual pricing"
          >
            <div
              className={`absolute top-0.5 h-5.5 w-5.5 rounded-full bg-white shadow transition-transform ${
                isAnnual ? "translate-x-5.5" : "translate-x-0.5"
              }`}
            />
          </button>
          <span
            className={`text-sm font-medium transition-colors ${
              isAnnual ? "text-text-primary" : "text-text-dim"
            }`}
          >
            Annual
          </span>
          {isAnnual && (
            <span className="rounded-full bg-green-400/15 px-2.5 py-0.5 text-xs font-medium text-green-400">
              Save 20%
            </span>
          )}
        </motion.div>

        {/* Pricing cards */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-12 grid gap-6 md:grid-cols-3"
        >
          {PRICING_TIERS.map((tier) => (
            <motion.div
              key={tier.name}
              variants={glowIn}
              transition={{
                type: "tween",
                ease: EASING.easeOut,
                duration: TIMING.slow,
              }}
              className={`relative overflow-hidden rounded-2xl border p-8 transition-all duration-300 ${
                tier.popular
                  ? "border-accent-blue/40 bg-gradient-to-b from-accent-blue/8 to-card/60 shadow-[0_0_40px_rgba(46,99,255,0.08)]"
                  : "border-border-subtle bg-card/40 hover:border-border-glow"
              }`}
            >
              {/* Popular badge */}
              {tier.popular && (
                <div className="absolute top-4 right-4 rounded-full bg-accent-blue/20 px-3 py-1 text-xs font-semibold text-accent-blue">
                  Most Popular
                </div>
              )}

              {/* Plan name */}
              <div className="mb-1 font-heading text-lg font-semibold text-text-primary">
                {tier.name}
              </div>
              <p className="mb-6 text-sm text-text-dim">{tier.description}</p>

              {/* Price */}
              <div className="mb-6">
                {tier.monthlyPrice !== null ? (
                  <div className="flex items-baseline gap-1">
                    <span className="font-heading text-4xl font-bold text-text-primary">
                      ${isAnnual ? tier.annualPrice : tier.monthlyPrice}
                    </span>
                    <span className="text-sm text-text-dim">/user/mo</span>
                  </div>
                ) : (
                  <div className="font-heading text-4xl font-bold text-text-primary">
                    Custom
                  </div>
                )}
              </div>

              {/* Features */}
              <ul className="mb-8 space-y-3">
                {tier.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2.5">
                    <Check className="mt-0.5 h-4 w-4 shrink-0 text-accent-blue" />
                    <span className="text-sm text-text-muted">{feature}</span>
                  </li>
                ))}
              </ul>

              {/* CTA */}
              <button
                className={`pill-button flex w-full items-center justify-center gap-2 font-semibold transition-all ${
                  tier.popular
                    ? "bg-accent-blue text-white shadow-lg shadow-accent-blue/25 hover:bg-accent-blue/90 hover:shadow-xl hover:shadow-accent-blue/30"
                    : "border border-accent-blue/40 text-accent-blue hover:border-accent-blue hover:bg-accent-blue/10"
                }`}
              >
                {tier.cta}
                <ArrowRight className="h-4 w-4" />
              </button>
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
