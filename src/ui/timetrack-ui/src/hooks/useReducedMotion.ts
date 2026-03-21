import { useReducedMotion as useMotionReducedMotion } from 'motion/react';

/**
 * Returns true if the user prefers reduced motion.
 * When true, all animations should be instant or disabled.
 */
export function useReducedMotion(): boolean {
  return useMotionReducedMotion() ?? false;
}
