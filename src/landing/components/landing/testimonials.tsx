"use client";

import { useRef, useState, useEffect, useCallback } from "react";
import { motion, useInView } from "motion/react";
import { Star, ChevronLeft, ChevronRight } from "lucide-react";
import { TESTIMONIALS } from "@/lib/landing-data";
import { fadeUp, staggerContainer, STAGGER, sectionTransition } from "@/lib/animations";

export function Testimonials() {
  const ref = useRef<HTMLElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const isInView = useInView(ref, { once: true, amount: 0.15 });
  const [activeIndex, setActiveIndex] = useState(0);
  const [isPaused, setIsPaused] = useState(false);

  const scrollTo = useCallback((index: number) => {
    if (!scrollRef.current) return;
    const container = scrollRef.current;
    const cardWidth = container.scrollWidth / TESTIMONIALS.length;
    container.scrollTo({ left: cardWidth * index, behavior: "smooth" });
    setActiveIndex(index);
  }, []);

  const next = useCallback(() => {
    scrollTo((activeIndex + 1) % TESTIMONIALS.length);
  }, [activeIndex, scrollTo]);

  const prev = useCallback(() => {
    scrollTo((activeIndex - 1 + TESTIMONIALS.length) % TESTIMONIALS.length);
  }, [activeIndex, scrollTo]);

  useEffect(() => {
    if (isPaused || !isInView) return;
    const timer = setInterval(next, 5000);
    return () => clearInterval(timer);
  }, [isPaused, isInView, next]);

  const handleScroll = () => {
    if (!scrollRef.current) return;
    const container = scrollRef.current;
    const cardWidth = container.scrollWidth / TESTIMONIALS.length;
    const newIndex = Math.round(container.scrollLeft / cardWidth);
    setActiveIndex(newIndex);
  };

  return (
    <section ref={ref} className="section-padding relative overflow-hidden">
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
          <span className="text-xs font-semibold tracking-[0.2em] text-accent-blue uppercase">
            Testimonials
          </span>
        </motion.div>

        {/* Heading */}
        <motion.h2
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto max-w-3xl text-center font-heading text-3xl leading-tight font-bold text-text-primary md:text-4xl lg:text-5xl"
        >
          Loved by teams who value their time
        </motion.h2>

        <motion.p
          variants={fadeUp}
          transition={sectionTransition}
          className="mx-auto mt-4 max-w-xl text-center text-base text-text-muted"
        >
          See how professionals and teams use ChronosX to gain clarity and focus.
        </motion.p>

        {/* Carousel */}
        <motion.div
          variants={fadeUp}
          transition={sectionTransition}
          className="relative mt-14"
          onMouseEnter={() => setIsPaused(true)}
          onMouseLeave={() => setIsPaused(false)}
          onFocus={() => setIsPaused(true)}
          onBlur={() => setIsPaused(false)}
        >
          {/* Scroll container */}
          <div
            ref={scrollRef}
            onScroll={handleScroll}
            className="scrollbar-hide flex snap-x snap-mandatory gap-6 overflow-x-auto pb-4"
            style={{ scrollbarWidth: "none", msOverflowStyle: "none" }}
          >
            {TESTIMONIALS.map((t) => (
              <div
                key={t.name}
                className="w-[320px] shrink-0 snap-center rounded-2xl border border-border-subtle bg-card/50 p-6 sm:w-[380px]"
              >
                {/* Stars */}
                <div className="mb-4 flex gap-0.5">
                  {[...Array(t.rating)].map((_, i) => (
                    <Star
                      key={i}
                      className="h-4 w-4 fill-amber-400 text-amber-400"
                    />
                  ))}
                </div>

                {/* Quote */}
                <p className="mb-6 text-sm leading-relaxed text-text-muted">
                  &ldquo;{t.quote}&rdquo;
                </p>

                {/* Author */}
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-accent-blue/15 font-heading text-sm font-semibold text-accent-blue">
                    {t.name
                      .split(" ")
                      .map((n) => n[0])
                      .join("")}
                  </div>
                  <div>
                    <div className="text-sm font-medium text-text-primary">
                      {t.name}
                    </div>
                    <div className="text-xs text-text-dim">
                      {t.role}, {t.company}
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Navigation */}
          <div className="mt-6 flex items-center justify-center gap-4">
            <button
              onClick={prev}
              aria-label="Previous testimonial"
              className="flex h-9 w-9 items-center justify-center rounded-full border border-border-subtle bg-card/50 text-text-muted transition-colors hover:border-accent-blue/30 hover:text-text-primary"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>

            <div className="flex gap-2">
              {TESTIMONIALS.map((_, i) => (
                <button
                  key={i}
                  onClick={() => scrollTo(i)}
                  aria-label={`Go to testimonial ${i + 1}`}
                  className={`h-2 rounded-full transition-all duration-300 ${
                    i === activeIndex
                      ? "w-6 bg-accent-blue"
                      : "w-2 bg-text-dim/30 hover:bg-text-dim/50"
                  }`}
                />
              ))}
            </div>

            <button
              onClick={next}
              aria-label="Next testimonial"
              className="flex h-9 w-9 items-center justify-center rounded-full border border-border-subtle bg-card/50 text-text-muted transition-colors hover:border-accent-blue/30 hover:text-text-primary"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        </motion.div>
      </motion.div>
    </section>
  );
}
