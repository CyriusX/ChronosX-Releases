import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { SimpleMarkdown } from '../SimpleMarkdown';

describe('SimpleMarkdown', () => {
  it('should auto-linkify raw https URLs inside text', () => {
    render(<SimpleMarkdown source={'See https://example.com for docs.'} />);

    const link = screen.getByRole('link', { name: 'https://example.com' });
    expect(link).toHaveAttribute('href', 'https://example.com');
  });

  it('should render a rich embed card for a standalone URL', () => {
    const { container } = render(<SimpleMarkdown source={'https://example.com'} />);

    // The generic embed renders an <a> card.
    const link = container.querySelector('a[href="https://example.com"]');
    expect(link).toBeTruthy();
    expect(screen.getByText('example.com')).toBeInTheDocument();
  });

  it('should auto-linkify www.* URLs and normalize href to https', () => {
    const { container } = render(<SimpleMarkdown source={'www.example.com'} />);

    const link = container.querySelector('a[href="https://www.example.com"]');
    expect(link).toBeTruthy();
  });
});
