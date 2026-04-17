"use client";

import { useRef } from "react";
import { motion, useInView } from "motion/react";
import { Check, ArrowRight } from "lucide-react";
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

export function Pricing() {
  const { copy, lang } = useLandingContent();
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.15 });

  const salesLabel = lang === "pt" ? "Falar com vendas" : "Contact sales";

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
            {copy.pricing.eyebrow}
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {copy.pricing.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          {copy.pricing.description}
        </motion.p>

        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-8 max-w-2xl text-center text-sm text-text-dim"
        >
          {copy.pricing.note}
        </motion.div>

        {/* Pricing cards */}
        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-12 grid gap-6 md:grid-cols-3"
        >
          {copy.pricing.tiers.map((tier) => (
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
              <a
                href={tier.cta === "waitlist" ? "#waitlist" : copy.footer.contactHref}
                className={`pill-button flex w-full items-center justify-center gap-2 font-semibold transition-all ${
                  tier.popular
                    ? "bg-accent-blue text-white shadow-lg shadow-accent-blue/25 hover:bg-accent-blue/90 hover:shadow-xl hover:shadow-accent-blue/30"
                    : "border border-accent-blue/40 text-accent-blue hover:border-accent-blue hover:bg-accent-blue/10"
                }`}
              >
                {tier.cta === "waitlist" ? copy.nav.cta : salesLabel}
                <ArrowRight className="h-4 w-4" />
              </a>
            </motion.div>
          ))}
        </motion.div>
      </motion.div>
    </section>
  );
}
