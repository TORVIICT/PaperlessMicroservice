import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { DocumentsPagination } from '@/features/documents/components/DocumentsPagination';

describe('DocumentsPagination', () => {
  it('shows the range and navigates to the previous page', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();

    render(
      <DocumentsPagination
        page={2}
        pageSize={20}
        total={25}
        onPageChange={onPageChange}
      />,
    );

    expect(screen.getByText('21–25 de 25 documentos')).toBeInTheDocument();
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Siguiente' }),
    ).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Anterior' }));

    expect(onPageChange).toHaveBeenCalledWith(1);
  });

  it('navigates to the next page from the first one', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();

    render(
      <DocumentsPagination
        page={1}
        pageSize={20}
        total={25}
        onPageChange={onPageChange}
      />,
    );

    expect(
      screen.getByRole('button', { name: 'Anterior' }),
    ).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Siguiente' }));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('hides controls when there is a single page', () => {
    render(
      <DocumentsPagination
        page={1}
        pageSize={20}
        total={2}
        onPageChange={vi.fn()}
      />,
    );

    expect(screen.getByText('1–2 de 2 documentos')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Siguiente' }),
    ).not.toBeInTheDocument();
  });
});
