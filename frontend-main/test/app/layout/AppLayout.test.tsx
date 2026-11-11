import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { AppLayout } from '@/app/layout/AppLayout';

function renderLayout(initialEntry = '/documents') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/documents" element={<h1>Documentos</h1>} />
          <Route
            path="/documents/:documentId"
            element={<h1>Detalle del documento</h1>}
          />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('AppLayout', () => {
  it('renders the shell with navigation and a disabled search field', () => {
    renderLayout();

    expect(
      screen.getByRole('link', { name: 'Documentos' }),
    ).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('searchbox')).toBeDisabled();
  });

  it('marks the active status filter in the sidebar', () => {
    renderLayout('/documents?status=processing');

    expect(
      screen.getByRole('link', { name: 'Procesando' }),
    ).toHaveAttribute('aria-current', 'page');
    expect(
      screen.getByRole('link', { name: 'Documentos' }),
    ).not.toHaveAttribute('aria-current');
  });
});
