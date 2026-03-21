import type { Variants } from 'motion/react';
import { STAGGER } from '../lib/animation';

interface UseStaggeredListOptions {
  staggerDelay?: number;
  direction?: 'up' | 'right';
  distance?: number;
}

interface StaggeredListResult {
  container: Variants;
  item: Variants;
}

export function useStaggeredList(
  options: UseStaggeredListOptions = {},
): StaggeredListResult {
  const {
    staggerDelay = STAGGER.listItems,
    direction = 'up',
    distance = 8,
  } = options;

  const container: Variants = {
    hidden: { opacity: 1 },
    visible: {
      opacity: 1,
      transition: {
        staggerChildren: staggerDelay,
        delayChildren: 0.05,
      },
    },
  };

  const item: Variants =
    direction === 'up'
      ? {
          hidden: { opacity: 0, y: distance },
          visible: { opacity: 1, y: 0 },
        }
      : {
          hidden: { opacity: 0, x: -distance },
          visible: { opacity: 1, x: 0 },
        };

  return { container, item };
}
