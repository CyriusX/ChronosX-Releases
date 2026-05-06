import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

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

vi.mock('../../components/dashboard', () => ({
  Sidebar: () => <div data-testid="sidebar" />,
}));

vi.mock('../../hooks/usePermissions', () => ({
  usePermissions: () => ({ canManageTeam: false }),
}));

vi.mock('../../services/projectsApi', () => ({
  listMyTasks: vi.fn(async () => ({ tasks: [] })),
}));

const mockProjects = [
  {
    id: 'p-active',
    createdByUserId: 'u1',
    name: 'Active A',
    color: '#4A9FFF',
    status: 'Active',
    createdAt: new Date().toISOString(),
    isBillable: false,
    currency: null,
    hourlyRate: null,
    canArchive: true,
    canDelete: true,
    canReactivate: false,
  },
  {
    id: 'p-arch',
    createdByUserId: 'u1',
    name: 'Archived B',
    color: '#4A9FFF',
    status: 'archived', // intentionally lower-case to validate normalization
    createdAt: new Date().toISOString(),
    isBillable: false,
    currency: null,
    hourlyRate: null,
    canArchive: false,
    canDelete: true,
    canReactivate: true,
  },
];

import Projects from '../Projects';
import { useProjectStore } from '../../stores/projectStore';

describe('Projects page', () => {
  it('separates Active and Archived projects and normalizes archived status', async () => {
    useProjectStore.setState({
      projects: mockProjects as any,
      isLoading: false,
      error: null,
      fetchProjects: vi.fn(async () => {}),
      createProject: vi.fn(),
      updateProject: vi.fn(),
      archiveProject: vi.fn(async () => true),
      reactivateProject: vi.fn(async () => true),
      deleteProject: vi.fn(async () => true),
      setError: vi.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Projects />
      </MemoryRouter>
    );

    expect(screen.getByText(/Active A/)).toBeInTheDocument();
    expect(screen.queryByText(/Archived B/)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Arquivados/i }));

    expect(screen.getByText(/Archived B/)).toBeInTheDocument();
    expect(screen.queryByText(/Active A/)).not.toBeInTheDocument();
  });
});
