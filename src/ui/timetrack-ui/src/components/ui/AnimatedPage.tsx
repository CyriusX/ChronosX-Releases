import { motion } from 'motion/react';
import { pageVariants, pageTransition } from '../../lib/animation';
import { isDesktopRuntime } from '../../lib/runtime';

interface AnimatedPageProps {
  children: React.ReactNode;
  className?: string;
}

export function AnimatedPage({ children, className }: AnimatedPageProps) {
  const desktop = isDesktopRuntime();
  return (
    <motion.div
      variants={pageVariants}
      // Desktop webviews sometimes fail to paint/animate until the window gains focus.
      // Avoid rendering the whole app at `opacity: 0` on first mount.
      initial={desktop ? false : 'hidden'}
      animate="visible"
      exit="exit"
      transition={pageTransition}
      className={className}
    >
      {children}
    </motion.div>
  );
}
