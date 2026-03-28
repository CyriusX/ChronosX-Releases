import type { Transition, Variants } from 'motion/react';

// ── Timing (in seconds for Motion, ms noted for AnimeJS) ──
export const TIMING = {
  micro: 0.15,    // 150ms — button hovers, icon swaps
  fast: 0.2,      // 200ms — tooltips, small reveals
  normal: 0.3,    // 300ms — card appearances, content swaps
  slow: 0.5,      // 500ms — page transitions, large reveals
  deliberate: 0.8, // 800ms — celebrations, loading sequences
} as const;

// AnimeJS uses milliseconds
export const TIMING_MS = {
  micro: 150,
  fast: 200,
  normal: 300,
  slow: 500,
  deliberate: 800,
  ring: 1200,
  counter: 800,
  celebration: 800,
} as const;

// ── Easing curves ──
export const EASING = {
  easeOut: [0.0, 0.0, 0.2, 1] as [number, number, number, number],
  easeIn: [0.4, 0.0, 1, 1] as [number, number, number, number],
  easeInOut: [0.4, 0.0, 0.2, 1] as [number, number, number, number],
  bounce: [0.34, 1.56, 0.64, 1] as [number, number, number, number],
} as const;

// ── Spring configs (without type field, use via transition={{ type: 'spring', ...SPRING.gentle }}) ──
export const SPRING = {
  snappy: { stiffness: 300, damping: 30, mass: 0.8 },
  gentle: { stiffness: 150, damping: 20, mass: 1 },
  bouncy: { stiffness: 400, damping: 15, mass: 0.5 },
};

// ── Stagger delays (in seconds) ──
export const STAGGER = {
  cards: 0.06,       // 60ms — dashboard/project cards
  listItems: 0.04,   // 40ms — session rows, app list items
  pills: 0.08,       // 80ms — insight pills, legend items
  fast: 0.03,        // 30ms — heatmap cells, timeline entries
} as const;

// ── Reusable Motion Variants ──

export const fadeUp: Variants = {
  hidden: { opacity: 0, y: 12 },
  visible: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -8 },
};

export const fadeIn: Variants = {
  hidden: { opacity: 0 },
  visible: { opacity: 1 },
  exit: { opacity: 0 },
};

export const scaleIn: Variants = {
  hidden: { opacity: 0, scale: 0.95 },
  visible: { opacity: 1, scale: 1 },
  exit: { opacity: 0, scale: 0.95 },
};

export const slideRight: Variants = {
  hidden: { opacity: 0, x: -16 },
  visible: { opacity: 1, x: 0 },
  exit: { opacity: 0, x: -16 },
};

export const slideLeft: Variants = {
  hidden: { opacity: 0, x: 16 },
  visible: { opacity: 1, x: 0 },
  exit: { opacity: 0, x: 16 },
};

// ── Container variants with stagger ──

export const staggerContainer = (staggerDelay: number = STAGGER.cards): Variants => ({
  hidden: { opacity: 1 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: staggerDelay,
      delayChildren: 0.05,
    },
  },
});

// ── Page transition ──
export const pageTransition: Transition = {
  type: 'tween',
  ease: EASING.easeOut,
  duration: TIMING.normal,
};

export const pageVariants: Variants = {
  hidden: { opacity: 0, y: 8 },
  visible: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -8 },
};

// ── Tooltip variants ──
export const tooltipVariants: Variants = {
  hidden: { opacity: 0, y: 4, scale: 0.97 },
  visible: { opacity: 1, y: 0, scale: 1 },
  exit: { opacity: 0, y: 4, scale: 0.97 },
};

// ── Modal variants ──
export const modalOverlayVariants: Variants = {
  hidden: { opacity: 0 },
  visible: { opacity: 1 },
  exit: { opacity: 0 },
};

export const modalContentVariants: Variants = {
  hidden: { opacity: 0, scale: 0.96, y: 8 },
  visible: { opacity: 1, scale: 1, y: 0 },
  exit: { opacity: 0, scale: 0.96, y: 8 },
};

// ── Shake animation (for error toasts) ──
export const shakeX = {
  x: [0, -4, 4, -2, 2, 0],
  transition: { duration: 0.4 },
};

// ── Hover/tap presets ──
export const hoverLift = {
  whileHover: { y: -2, transition: { duration: TIMING.fast } },
};

export const hoverScale = {
  whileHover: { scale: 1.08 },
  whileTap: { scale: 0.95 },
  transition: { duration: TIMING.micro },
};

export const buttonTap = {
  whileHover: { scale: 1.04 },
  whileTap: { scale: 0.97 },
};

export const subtleHover = {
  whileHover: { x: 3 },
  transition: { type: 'spring', ...SPRING.snappy },
};

// ── Glass card hover elevation ──
export const glassCardHover = {
  whileHover: { y: -3, transition: { duration: TIMING.fast, ease: EASING.easeOut } },
};

// ── Glow entrance for premium cards ──
export const glowIn: Variants = {
  hidden: { opacity: 0, scale: 0.97, filter: 'blur(4px)' },
  visible: { opacity: 1, scale: 1, filter: 'blur(0px)' },
  exit: { opacity: 0, scale: 0.97, filter: 'blur(4px)' },
};
