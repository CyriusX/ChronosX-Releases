import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ProjectCard } from '../ProjectCard';

const baseProject = {
  id: 'p1',
  createdByUserId: 'u1',
  name: 'Proj',
  description: '',
  color: '#4A9FFF',
  status: 'Active',
  createdAt: new Date().toISOString(),
  updatedAt: undefined,
  isBillable: false,
  currency: null,
  hourlyRate: null,
  canArchive: false,
  canDelete: false,
  canReactivate: false,
};

describe('ProjectCard', () => {
  it('hides Archive/Delete actions when not allowed', () => {
    render(
      <ProjectCard
        project={baseProject as any}
        isMenuOpen={true}
        onToggleMenu={vi.fn()}
        onEdit={vi.fn()}
        onArchive={vi.fn()}
        onReactivate={vi.fn()}
        onDelete={vi.fn()}
        isArchived={false}
      />
    );

    expect(screen.queryByText(/Arquivar/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Excluir/i)).not.toBeInTheDocument();
  });

  it('shows Reactivate action only when allowed', () => {
    render(
      <ProjectCard
        project={{ ...baseProject, status: 'Archived', canReactivate: true } as any}
        isMenuOpen={true}
        onToggleMenu={vi.fn()}
        onEdit={vi.fn()}
        onArchive={vi.fn()}
        onReactivate={vi.fn()}
        onDelete={vi.fn()}
        isArchived={true}
      />
    );

    expect(screen.getByText(/Reativar/i)).toBeInTheDocument();
  });
});

