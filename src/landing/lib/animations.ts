import type { Transition, Variants } from "motion/react";

export const TIMING = {
  micro: 0.15,
  fast: 0.2,
  normal: 0.3,
  slow: 0.5,
  deliberate: 0.8,
} as const;

export const EASING = {
  easeOut: [0.0, 0.0, 0.2, 1] as [number, number, number, number],
  easeIn: [0.4, 0.0, 1, 1] as [number, number, number, number],
  easeInOut: [0.4, 0.0, 0.2, 1] as [number, number, number, number],
} as const;

export const SPRING = {
  snappy: { stiffness: 300, damping: 30, mass: 0.8 },
  gentle: { stiffness: 150, damping: 20, mass: 1 },
};

export const STAGGER = {
  cards: 0.08,
  pills: 0.06,
  fast: 0.04,
} as const;

export const fadeUp: Variants = {
  hidden: { opacity: 0, y: 24 },
  visible: { opacity: 1, y: 0 },
};

export const fadeIn: Variants = {
  hidden: { opacity: 0 },
  visible: { opacity: 1 },
};

export const scaleIn: Variants = {
  hidden: { opacity: 0, scale: 0.95 },
  visible: { opacity: 1, scale: 1 },
};

export const glowIn: Variants = {
  hidden: { opacity: 0, scale: 0.97, filter: "blur(4px)" },
  visible: { opacity: 1, scale: 1, filter: "blur(0px)" },
};

export const slideUp: Variants = {
  hidden: { opacity: 0, y: 40 },
  visible: { opacity: 1, y: 0 },
};

export const staggerContainer = (
  staggerDelay: number = STAGGER.cards
): Variants => ({
  hidden: { opacity: 1 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: staggerDelay,
      delayChildren: 0.1,
    },
  },
});

export const sectionTransition: Transition = {
  type: "tween",
  ease: EASING.easeOut,
  duration: TIMING.slow,
};

export const hoverLift = {
  whileHover: { y: -4, transition: { duration: TIMING.fast } },
};

export const glassCardHover = {
  whileHover: {
    y: -3,
    transition: { duration: TIMING.fast, ease: EASING.easeOut },
  },
};

export const buttonTap = {
  whileHover: { scale: 1.03 },
  whileTap: { scale: 0.97 },
};
