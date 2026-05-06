"use client";

import { useMemo, useRef, useState } from "react";
import { motion, useInView } from "motion/react";
import { ChevronDown } from "lucide-react";
import { useLandingContent } from "./content-provider";
import { cn } from "@/lib/cn";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";

export function Faq() {
  const { copy } = useLandingContent();
  const ref = useRef<HTMLElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.15 });
  const [open, setOpen] = useState<number>(0);

  const items = useMemo(() => copy.faq.items, [copy.faq.items]);

  return (
    <section ref={ref} id="faq" className="section-padding relative">
      <motion.div
        className="mx-auto max-w-4xl px-5 md:px-8"
        initial="hidden"
        animate={isInView ? "visible" : "hidden"}
        variants={staggerContainer(STAGGER.cards)}
      >
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="mb-4 text-center"
        >
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-cyan uppercase">
            {copy.faq.eyebrow}
          </span>
        </motion.div>

        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          {copy.faq.headline}
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-2xl text-center text-base leading-relaxed text-text-muted"
        >
          {copy.faq.description}
        </motion.p>

        <motion.div
          variants={staggerContainer(STAGGER.cards)}
          className="mt-12 space-y-3"
        >
          {items.map((item, idx) => {
            const isOpen = open === idx;
            return (
              <motion.div
                key={item.q}
                variants={fadeUp}
                transition={sectionTransition}
                className="overflow-hidden rounded-2xl border border-border-subtle bg-card/30"
              >
                <button
                  type="button"
                  onClick={() => setOpen(isOpen ? -1 : idx)}
                  className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left"
                  aria-expanded={isOpen}
                >
                  <span className="font-heading text-base font-semibold text-text-primary">
                    {item.q}
                  </span>
                  <ChevronDown
                    className={cn(
                      "h-5 w-5 shrink-0 text-text-dim transition-transform duration-300",
                      isOpen ? "rotate-180" : "rotate-0"
                    )}
                  />
                </button>
                <div
                  className={cn(
                    "grid transition-[grid-template-rows] duration-300 ease-out",
                    isOpen ? "grid-rows-[1fr]" : "grid-rows-[0fr]"
                  )}
                >
                  <div className="min-h-0 overflow-hidden">
                    <div className="px-5 pb-5 text-sm leading-relaxed text-text-muted">
                      {item.a}
                    </div>
                  </div>
                </div>
              </motion.div>
            );
          })}
        </motion.div>
      </motion.div>
    </section>
  );
}

