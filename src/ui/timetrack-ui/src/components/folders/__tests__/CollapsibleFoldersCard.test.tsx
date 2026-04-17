import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

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

import { CollapsibleFoldersCard } from '../CollapsibleFoldersCard';

describe('CollapsibleFoldersCard', () => {
  it('lazy loads folders only after expanding', async () => {
    const loadFolders = vi.fn(async () => [
      { folderPath: '/Users/junior/Documents', totalSeconds: 123, visitCount: 4 },
    ]);

    render(
      <CollapsibleFoldersCard
        title="Top Folders"
        defaultCollapsed
        loadFolders={loadFolders}
      />
    );

    expect(loadFolders).not.toHaveBeenCalled();
    expect(screen.queryByText('/Users/junior/Documents')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Top Folders/i }));

    await waitFor(() => expect(loadFolders).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('/Users/junior/Documents')).toBeInTheDocument();
  });
});

