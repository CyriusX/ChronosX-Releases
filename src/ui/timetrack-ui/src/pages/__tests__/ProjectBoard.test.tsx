import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import type { Task, ProjectItem } from '../../services/projectsApi';
import { emitProjectUpdated, emitTaskUpdated } from '../../lib/appEvents';

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

vi.mock('../../components/projects/KanbanBoard', () => ({
  KanbanBoard: ({ tasks }: { tasks: Task[] }) => (
    <div>
      {tasks.map((t) => (
        <div key={t.id}>{t.title}</div>
      ))}
    </div>
  ),
}));

vi.mock('../../components/projects/ProjectMembersModal', () => ({
  ProjectMembersModal: () => null,
}));

vi.mock('../../stores/uiStore', () => ({
  useNotifications: () => ({ notify: { success: vi.fn(), error: vi.fn() } }),
}));

vi.mock('../../hooks/usePermissions', () => ({
  usePermissions: () => ({ canManageTeam: true }),
}));

const mockProject: ProjectItem = {
  id: 'p1',
  createdByUserId: 'u1',
  name: 'Old Project',
  description: null,
  color: '#4A9FFF',
  status: 'Active',
  createdAt: new Date().toISOString(),
  updatedAt: null,
  isBillable: false,
  currency: null,
  hourlyRate: null,
  canArchive: true,
  canDelete: true,
  canReactivate: false,
  syncSource: 'Local',
  linearProjectId: null,
} as any;

const mockTask: Task = {
  id: 't1',
  projectId: 'p1',
  projectName: 'Old Project',
  projectColor: '#4A9FFF',
  title: 'Old Task',
  description: null,
  status: 'Todo',
  createdByUserId: 'u1',
  assignedUserId: null,
  assignedUserDisplayName: null,
  priority: 'Medium',
  dueDate: null,
  position: 1,
  createdAt: new Date().toISOString(),
  updatedAt: null,
  movedToInProgressAt: null,
  completedAt: null,
  totalSecondsWorked: 0,
  rowVersion: 0,
  isRunning: false,
  isPaused: false,
  runningSeconds: null,
  isLinearSourced: false,
  linearIssueIdentifier: null,
  linearUrl: null,
  linearStateName: null,
} as any;

vi.mock('../../services/projectsApi', async () => {
  return {
    getProject: vi.fn(async () => mockProject),
    listProjectTasks: vi.fn(async () => ({ tasks: [mockTask] })),
    listProjectMembers: vi.fn(async () => ({ members: [], totalCount: 0 })),
    syncLinear: vi.fn(async () => ({ tasksCreated: 0, tasksUpdated: 0 })),
  };
});

import ProjectBoard from '../ProjectBoard';

describe('ProjectBoard immediate updates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('updates task title immediately when a taskUpdated event is emitted', async () => {
    render(
      <MemoryRouter initialEntries={['/projects/p1/board']}>
        <Routes>
          <Route path="/projects/:projectId/board" element={<ProjectBoard />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByText('Old Task')).toBeInTheDocument();

    emitTaskUpdated({ ...mockTask, title: 'New Task Title' } as any);

    await waitFor(() => {
      expect(screen.getByText('New Task Title')).toBeInTheDocument();
    });
  });

  it('updates project name immediately when a projectUpdated event is emitted', async () => {
    render(
      <MemoryRouter initialEntries={['/projects/p1/board']}>
        <Routes>
          <Route path="/projects/:projectId/board" element={<ProjectBoard />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByText('Old Project')).toBeInTheDocument();

    emitProjectUpdated({ id: 'p1', name: 'Renamed Project', color: '#111111' } as any);

    await waitFor(() => {
      expect(screen.getByText('Renamed Project')).toBeInTheDocument();
    });
  });
});
