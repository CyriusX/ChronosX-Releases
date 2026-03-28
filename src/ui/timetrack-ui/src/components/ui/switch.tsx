import * as React from 'react';
import * as SwitchPrimitives from '@radix-ui/react-switch';
import { cn } from '../../lib/utils';

/**
 * Switch Component - Toggle control following shadcn/ui pattern
 *
 * SOLID:
 * - SRP: Apenas toggle UI control
 * - OCP: Extensível via className e props
 *
 * Composition:
 * - Usa Radix UI Switch como base (composition over inheritance)
 */
const Switch = React.forwardRef<
  React.ElementRef<typeof SwitchPrimitives.Root>,
  React.ComponentPropsWithoutRef<typeof SwitchPrimitives.Root>
>(({ className, ...props }, ref) => (
  <SwitchPrimitives.Root
    className={cn(
      // Base styles - peer for label sibling styling
      'peer inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full',
      'border-2 border-transparent shadow-sm transition-colors',
      // Focus visible ring
      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#8B5CF6] focus-visible:ring-offset-2 focus-visible:ring-offset-[rgb(10,12,18)]',
      // Disabled state
      'disabled:cursor-not-allowed disabled:opacity-50',
      // Checked/unchecked colors
      'data-[state=checked]:bg-gradient-to-r data-[state=checked]:from-[#8B5CF6] data-[state=checked]:to-[#6D28D9]',
      'data-[state=unchecked]:bg-[rgba(255,255,255,0.10)]',
      className
    )}
    {...props}
    ref={ref}
  >
    <SwitchPrimitives.Thumb
      className={cn(
        // Thumb base styles
        'pointer-events-none block h-4 w-4 rounded-full bg-white shadow-lg ring-0',
        // Transition animation
        'transition-transform duration-200 ease-in-out',
        // Transform for checked state
        'data-[state=checked]:translate-x-4',
        'data-[state=unchecked]:translate-x-0'
      )}
    />
  </SwitchPrimitives.Root>
));
Switch.displayName = SwitchPrimitives.Root.displayName;

export { Switch };
