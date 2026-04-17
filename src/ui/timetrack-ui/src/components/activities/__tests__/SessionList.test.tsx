import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

vi.mock('motion/react', async () => {
  const React = (await import('react')).default;

  const strip = (props: Record<string, unknown>) => {
    const {
      variants,
      initial,
      animate,
      exit,
      transition,
      layout,
      whileHover,
      whileTap,
      layoutId,
      ...rest
    } = props;
    void variants; void initial; void animate; void exit; void transition; void layout; void whileHover; void whileTap; void layoutId;
    return rest;
  };

  const motion = new Proxy({}, {
    get: (_target, tag: string) => {
      return (props: any) => React.createElement(tag, strip(props), props.children);
    },
  });

  return {
    motion,
    AnimatePresence: ({ children }: { children: React.ReactNode }) => React.createElement(React.Fragment, null, children),
  };
});

import { SessionList } from '../SessionList';

describe('SessionList', () => {
  it('starts collapsed when defaultCollapsed is true and expands on click', () => {
    const activities: any[] = [
      {
        id: 'a1',
        name: 'Code',
        startUtc: new Date('2026-04-17T10:00:00Z').toISOString(),
        endUtc: new Date('2026-04-17T10:05:00Z').toISOString(),
        duration: 300,
        subcategory: null,
        productivity: 'productive',
      },
    ];

    render(<SessionList activities={activities as any} defaultCollapsed />);

    expect(screen.queryByText('Code')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Sessoes detalhadas/i }));

    expect(screen.getByText('Code')).toBeInTheDocument();
  });
});

