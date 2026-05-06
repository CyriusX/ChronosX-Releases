import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

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

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: any) => <div data-testid="recharts-container">{children}</div>,
  LineChart: ({ children }: any) => <div data-testid="recharts-linechart">{children}</div>,
  Line: () => <div data-testid="recharts-line" />,
  XAxis: () => <div data-testid="recharts-xaxis" />,
  Tooltip: () => <div data-testid="recharts-tooltip" />,
}));

vi.mock('../MyTasksWidget', () => ({
  MyTasksWidget: () => <div data-testid="my-tasks" />,
}));

vi.mock('../../../hooks/useTeamStatus', () => ({
  useTeamStatus: () => ({ members: [], isLoading: false, loadTeamStatus: vi.fn() }),
}));

vi.mock('../../../stores/authStore', () => ({
  useAuthStore: (selector: any) => selector({ user: { id: 'u1' } }),
}));

import { RightPanel } from '../RightPanel';

describe('RightPanel', () => {
  it('does not render the folders card on dashboard', () => {
    render(<RightPanel summary={null} weeklyHistory={[]} />);
    expect(screen.queryByText(/Pastas acessadas/i)).not.toBeInTheDocument();
  });
});

