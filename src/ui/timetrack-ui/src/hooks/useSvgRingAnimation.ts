import { useEffect, useRef } from 'react';
import { animate } from 'animejs';
import { TIMING_MS } from '../lib/animation';
import { useReducedMotion } from './useReducedMotion';

interface UseSvgRingAnimationOptions {
  duration?: number;
  delay?: number;
}

export function useSvgRingAnimation(
  progress: number, // 0 to 1
  circumference: number,
  options: UseSvgRingAnimationOptions = {},
) {
  const {
    duration = TIMING_MS.ring,
    delay = 0,
  } = options;

  const ref = useRef<SVGCircleElement>(null);
  const animRef = useRef<ReturnType<typeof animate> | null>(null);
  const reduced = useReducedMotion();

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    const targetOffset = circumference * (1 - progress);

    if (reduced) {
      el.style.strokeDashoffset = String(targetOffset);
      return;
    }

    animRef.current?.pause();
    animRef.current = animate(el, {
      strokeDashoffset: targetOffset,
      duration,
      ease: 'inOutQuart',
      delay,
    });

    return () => {
      animRef.current?.pause();
    };
  }, [progress, circumference, duration, delay, reduced]);

  return ref;
}
