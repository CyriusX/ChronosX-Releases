import { useEffect, useRef, useState } from 'react';
import { animate } from 'animejs';
import { TIMING_MS } from '../lib/animation';
import { useReducedMotion } from './useReducedMotion';

interface UseAnimatedCounterOptions {
  duration?: number;
  decimals?: number;
}

export function useAnimatedCounter(
  target: number,
  options: UseAnimatedCounterOptions = {},
) {
  const {
    duration = TIMING_MS.counter,
    decimals = 0,
  } = options;

  const reduced = useReducedMotion();
  const [display, setDisplay] = useState(target);
  const objRef = useRef({ value: target });
  const animRef = useRef<ReturnType<typeof animate> | null>(null);

  useEffect(() => {
    if (reduced) {
      setDisplay(target);
      objRef.current.value = target;
      return;
    }

    animRef.current?.pause();
    animRef.current = animate(objRef.current, {
      value: target,
      duration,
      ease: 'outExpo',
      onUpdate: () => {
        const val = decimals === 0
          ? Math.round(objRef.current.value)
          : Number(objRef.current.value.toFixed(decimals));
        setDisplay(val);
      },
    });

    return () => {
      animRef.current?.pause();
    };
  }, [target, duration, decimals, reduced]);

  return display;
}
