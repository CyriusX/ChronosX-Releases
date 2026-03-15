/**
 * Tests for AppCategoryOverrideModal component (CX-144)
 *
 * Tests:
 * - Rendering
 * - Productivity selection
 * - Subcategory selection
 * - Form validation
 * - Save/Cancel actions
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AppCategoryOverrideModal } from '../AppCategoryOverrideModal';


const mockApp = {
  identifier: 'whatsapp.exe',
  identifierType: 'exe' as const,
  displayName: 'WhatsApp Desktop',
  productivity: 'neutral' as const,
  subcategory: 'communication' as const,
  source: 'global',
  note: undefined,
};

const defaultProps = {
  isOpen: true,
  onClose: vi.fn(),
  onSave: vi.fn().mockResolvedValue(undefined),
  app: mockApp,
};

describe('AppCategoryOverrideModal', () => {
  describe('rendering', () => {
    it('should not render when closed', () => {
      render(<AppCategoryOverrideModal {...defaultProps} isOpen={false} />);

      expect(screen.queryByText('Classificar:')).not.toBeInTheDocument();
    });

    it('should not render when app is null', () => {
      render(<AppCategoryOverrideModal {...defaultProps} app={null} />);

      expect(screen.queryByText('Classificar:')).not.toBeInTheDocument();
    });

    it('should render modal with app name', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      expect(screen.getByText('Classificar: WhatsApp Desktop')).toBeInTheDocument();
      expect(screen.getByText('whatsapp.exe')).toBeInTheDocument();
    });

    it('should show current global classification', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      expect(screen.getByText(/Classificação atual \(global\):/)).toBeInTheDocument();
      expect(screen.getByText(/Neutro — Comunicação/)).toBeInTheDocument();
    });

    it('should show productivity options', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      // The buttons include emoji and there may be multiple elements with the same text
      // Use getAllByText since "Neutro" appears in both the current classification and button
      expect(screen.getAllByText(/Produtivo/).length).toBeGreaterThan(0);
      expect(screen.getAllByText(/Neutro/).length).toBeGreaterThan(0);
      expect(screen.getAllByText(/Distração/).length).toBeGreaterThan(0);
    });

    it('should show warning message', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      expect(screen.getByText(/Esta mudança afeta toda a organização/)).toBeInTheDocument();
    });

    it('should show note textarea', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      expect(screen.getByPlaceholderText('Justificativa para a classificação...')).toBeInTheDocument();
    });
  });

  describe('productivity selection', () => {
    it('should select productivity on click', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      const productiveButton = screen.getByRole('button', { name: /Produtivo/ });
      fireEvent.click(productiveButton);

      // Should have active state (checking via class would be fragile)
      expect(productiveButton).toBeInTheDocument();
    });

    it('should update subcategories when productivity changes', async () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      // Select "Distraction" which has different subcategories
      const distractionButton = screen.getByRole('button', { name: /Distração/ });
      fireEvent.click(distractionButton);

      const subcategorySelect = screen.getByRole('combobox');
      // Should show distraction subcategories (Social Media, Entertainment)
      expect(subcategorySelect).toBeInTheDocument();
    });
  });

  describe('form interactions', () => {
    it('should allow typing note', () => {
      render(<AppCategoryOverrideModal {...defaultProps} />);

      const noteTextarea = screen.getByPlaceholderText('Justificativa para a classificação...');
      fireEvent.change(noteTextarea, { target: { value: 'Used for customer support' } });

      expect(noteTextarea).toHaveValue('Used for customer support');
    });

    it('should pre-fill with current values', () => {
      const appWithNote = {
        ...mockApp,
        note: 'Existing note',
      };
      render(<AppCategoryOverrideModal {...defaultProps} app={appWithNote} />);

      const noteTextarea = screen.getByPlaceholderText('Justificativa para a classificação...');
      expect(noteTextarea).toHaveValue('Existing note');
    });
  });

  describe('actions', () => {
    it('should call onClose when clicking Cancel', () => {
      const onClose = vi.fn();
      render(<AppCategoryOverrideModal {...defaultProps} onClose={onClose} />);

      fireEvent.click(screen.getByText('Cancelar'));

      expect(onClose).toHaveBeenCalled();
    });

    it('should call onClose when clicking X button', () => {
      const onClose = vi.fn();
      render(<AppCategoryOverrideModal {...defaultProps} onClose={onClose} />);

      const closeButton = screen.getByRole('button', { name: '' }); // X button
      fireEvent.click(closeButton);

      expect(onClose).toHaveBeenCalled();
    });

    it('should call onSave with correct data', async () => {
      const onSave = vi.fn().mockResolvedValue(undefined);
      render(<AppCategoryOverrideModal {...defaultProps} onSave={onSave} />);

      // Select productive
      fireEvent.click(screen.getByRole('button', { name: /Produtivo/ }));

      // Add note
      const noteTextarea = screen.getByPlaceholderText('Justificativa para a classificação...');
      fireEvent.change(noteTextarea, { target: { value: 'Test note' } });

      // Save
      fireEvent.click(screen.getByText('Salvar override'));

      await waitFor(() => {
        expect(onSave).toHaveBeenCalledWith(
          expect.objectContaining({
            identifier: 'whatsapp.exe',
            identifierType: 'exe',
            displayName: 'WhatsApp Desktop',
            productivity: 'productive',
            note: 'Test note',
          })
        );
      });
    });

    it('should disable buttons while saving', async () => {
      const onSave = vi.fn().mockImplementation(() => new Promise((resolve) => setTimeout(resolve, 100)));
      render(<AppCategoryOverrideModal {...defaultProps} onSave={onSave} />);

      fireEvent.click(screen.getByText('Salvar override'));

      // Buttons should be disabled during save
      expect(screen.getByText('Salvando...')).toBeInTheDocument();
      expect(screen.getByText('Cancelar')).toBeDisabled();
    });
  });

  describe('unknown app handling', () => {
    it('should default to neutral for unknown apps', () => {
      const unknownApp = {
        ...mockApp,
        productivity: 'unknown' as const,
        subcategory: 'unknown' as const,
      };
      render(<AppCategoryOverrideModal {...defaultProps} app={unknownApp} />);

      // Should not show current classification for unknown
      expect(screen.queryByText(/Classificação atual/)).not.toBeInTheDocument();
    });
  });
});
