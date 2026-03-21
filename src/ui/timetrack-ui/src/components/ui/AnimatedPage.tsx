import { motion } from 'motion/react';
import { pageVariants, pageTransition } from '../../lib/animation';

interface AnimatedPageProps {
  children: React.ReactNode;
  className?: string;
}

export function AnimatedPage({ children, className }: AnimatedPageProps) {
  return (
    <motion.div
      variants={pageVariants}
      initial="hidden"
      animate="visible"
      exit="exit"
      transition={pageTransition}
      className={className}
    >
      {children}
    </motion.div>
  );
}
