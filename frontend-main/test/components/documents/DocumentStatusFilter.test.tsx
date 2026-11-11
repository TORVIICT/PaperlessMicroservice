import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { DocumentStatusFilter } from '@/features/documents/components/DocumentStatusFilter';

describe('DocumentStatusFilter', () => {
  it('renders every status and marks the selected one', () => {
    render(
      <DocumentStatusFilter value="processing" onChange={vi.fn()} />,
    );

    expect(
      screen.getByRole('group', { name: 'Filtrar por estado' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Procesando' }),
    ).toHaveAttribute('aria-pressed', 'true');
    expect(
      screen.getByRole('button', { name: 'Todos' }),
    ).toHaveAttribute('aria-pressed', 'false');
    expect(
      screen.getByRole('button', { name: 'Fallido' }),
    ).toHaveAttribute('aria-pressed', 'false');
  });

  it('emits the selected status and the clear action', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<DocumentStatusFilter onChange={onChange} />);

    await user.click(screen.getByRole('button', { name: 'Fallido' }));

    expect(onChange).toHaveBeenCalledWith('failed');

    await user.click(screen.getByRole('button', { name: 'Todos' }));

    expect(onChange).toHaveBeenLastCalledWith(undefined);
  });
});
