import * as React from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../../lib/utils';

const buttonVariants = cva(
  'inline-flex items-center justify-center whitespace-nowrap rounded-[14px] text-sm font-medium transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#8B5CF6] focus-visible:ring-offset-2 focus-visible:ring-offset-[rgb(10,12,18)] disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        default:
          'bg-gradient-to-r from-[#8B5CF6] to-[#6D28D9] text-white shadow-[0_0_15px_rgba(139,92,246,0.25)] hover:shadow-[0_0_25px_rgba(139,92,246,0.4)] hover:brightness-110',
        destructive:
          'bg-gradient-to-r from-[#DC2626] to-[#B91C1C] text-white shadow-[0_0_15px_rgba(220,38,38,0.25)] hover:shadow-[0_0_25px_rgba(220,38,38,0.4)]',
        outline:
          'border border-[rgba(255,255,255,0.10)] bg-[rgba(255,255,255,0.04)] backdrop-blur-sm shadow-sm hover:bg-[rgba(255,255,255,0.08)] hover:border-[rgba(139,92,246,0.25)] text-[#f5f7fb]',
        secondary:
          'bg-[rgba(255,255,255,0.06)] backdrop-blur-sm border border-[rgba(255,255,255,0.08)] text-[#f5f7fb] shadow-sm hover:bg-[rgba(255,255,255,0.10)]',
        ghost:
          'hover:bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.7)] hover:text-[#f5f7fb]',
        link:
          'text-[#8B5CF6] underline-offset-4 hover:underline hover:text-[#A78BFA]',
      },
      size: {
        default: 'h-9 px-4 py-2',
        sm: 'h-8 rounded-[12px] px-3 text-xs',
        lg: 'h-10 rounded-[14px] px-8',
        icon: 'h-9 w-9',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button';
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    );
  }
);
Button.displayName = 'Button';

export { Button, buttonVariants };
