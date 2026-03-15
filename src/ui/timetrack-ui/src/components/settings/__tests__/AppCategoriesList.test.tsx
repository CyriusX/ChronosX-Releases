/**
 * Tests for AppCategoriesList component (CX-144)
 *
 * Tests:
 * - Rendering with apps
 * - Search functionality
 * - Filter functionality
 * - Edit/Delete actions
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AppCategoriesList } from '../AppCategoriesList';
import type { AppCategoryDisplayItem } from '../../../types/appCategories';

const mockApps: AppCategoryDisplayItem[] = [
  {
    identifier: 'code.exe',
    identifierType: 'exe',
    displayName: 'VS Code',
    productivity: 'productive',
    subcategory: 'development',
    source: 'global',
  },
  {
    identifier: 'youtube.com',
    identifierType: 'domain',
    displayName: 'YouTube',
    productivity: 'distraction',
    subcategory: 'entertainment',
    source: 'global',
  },
  {
    identifier: 'whatsapp.exe',
    identifierType: 'exe',
    displayName: 'WhatsApp',
    productivity: 'neutral',
    subcategory: 'communication',
    source: 'org_override',
    note: 'Used for customer support',
  },
  {
    identifier: 'unknown-app.exe',
    identifierType: 'exe',
    displayName: 'unknown-app.exe',
    productivity: 'unknown',
    subcategory: 'unknown',
    source: 'default',
  },
];

const defaultProps = {
  apps: mockApps,
  isLoading: false,
  error: null,
  searchQuery: '',
  activeFilter: 'all' as const,
  uncategorizedCount: 1,
  onSearchChange: vi.fn(),
  onFilterChange: vi.fn(),
  onEdit: vi.fn(),
  onDelete: vi.fn(),
  onAdd: vi.fn(),
};

describe('AppCategoriesList', () => {
  describe('rendering', () => {
    it('should render apps list', () => {
      render(<AppCategoriesList {...defaultProps} />);

      expect(screen.getByText('VS Code')).toBeInTheDocument();
      expect(screen.getByText('YouTube')).toBeInTheDocument();
      expect(screen.getByText('WhatsApp')).toBeInTheDocument();
    });

    it('should show productivity badges', () => {
      render(<AppCategoriesList {...defaultProps} />);

      // Check for productivity labels within badges using getAllByText
      // The badges show text like "🟢 Produtivo/Desenvolvimento"
      const productiveBadges = screen.getAllByText((_content, element) => {
        return element?.textContent?.includes('Produtivo') ?? false;
      });
      expect(productiveBadges.length).toBeGreaterThan(0);
    });

    it('should show override badge for org overrides', () => {
      render(<AppCategoriesList {...defaultProps} />);

      // The override badge shows "✏️ Override" - use getAllByText since there might be multiple
      const overrideElements = screen.getAllByText((_content, element) => {
        // Check if the element's text content contains "Override"
        return Boolean(element?.textContent?.includes('Override'));
      });
      expect(overrideElements.length).toBeGreaterThan(0);
    });

    it('should show "Classificar" button for unknown apps', () => {
      render(<AppCategoriesList {...defaultProps} />);

      expect(screen.getByText('Classificar')).toBeInTheDocument();
    });

    it('should show uncategorized count badge', () => {
      render(<AppCategoriesList {...defaultProps} />);

      expect(screen.getByText(/1 app sem classificação/)).toBeInTheDocument();
    });
  });

  describe('loading state', () => {
    it('should show loading spinner', () => {
      render(<AppCategoriesList {...defaultProps} isLoading={true} />);

      expect(screen.getByText('Carregando...')).toBeInTheDocument();
    });
  });

  describe('error state', () => {
    it('should show error message', () => {
      render(
        <AppCategoriesList {...defaultProps} error="Network error" isLoading={false} />
      );

      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  describe('empty state', () => {
    it('should show empty message when no apps', () => {
      render(<AppCategoriesList {...defaultProps} apps={[]} />);

      expect(screen.getByText('Nenhum aplicativo encontrado')).toBeInTheDocument();
    });
  });

  describe('search', () => {
    it('should call onSearchChange when typing', () => {
      const onSearchChange = vi.fn();
      render(<AppCategoriesList {...defaultProps} onSearchChange={onSearchChange} />);

      const searchInput = screen.getByPlaceholderText('Buscar app ou site...');
      fireEvent.change(searchInput, { target: { value: 'code' } });

      expect(onSearchChange).toHaveBeenCalledWith('code');
    });
  });

  describe('filter', () => {
    it('should call onFilterChange when selecting filter', () => {
      const onFilterChange = vi.fn();
      render(<AppCategoriesList {...defaultProps} onFilterChange={onFilterChange} />);

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'productive' } });

      expect(onFilterChange).toHaveBeenCalledWith('productive');
    });
  });

  describe('actions', () => {
    it('should call onEdit when clicking Edit button', () => {
      const onEdit = vi.fn();
      render(<AppCategoriesList {...defaultProps} onEdit={onEdit} />);

      const editButtons = screen.getAllByText('Editar');
      fireEvent.click(editButtons[0]);

      expect(onEdit).toHaveBeenCalled();
    });

    it('should call onDelete when clicking Delete button', () => {
      const onDelete = vi.fn();
      render(<AppCategoriesList {...defaultProps} onDelete={onDelete} />);

      // Find delete button (only shows for overrides)
      const deleteButton = screen.getByTitle('Remover override');
      fireEvent.click(deleteButton);

      expect(onDelete).toHaveBeenCalledWith('whatsapp.exe');
    });

    it('should call onAdd when clicking Add button', () => {
      const onAdd = vi.fn();
      render(<AppCategoriesList {...defaultProps} onAdd={onAdd} />);

      fireEvent.click(screen.getByText('Adicionar'));

      expect(onAdd).toHaveBeenCalled();
    });
  });

  describe('filter link for uncategorized', () => {
    it('should call onFilterChange when clicking filter link', () => {
      const onFilterChange = vi.fn();
      render(<AppCategoriesList {...defaultProps} onFilterChange={onFilterChange} />);

      fireEvent.click(screen.getByText('Filtrar'));

      expect(onFilterChange).toHaveBeenCalledWith('unknown');
    });
  });
});
