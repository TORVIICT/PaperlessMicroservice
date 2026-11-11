import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DocumentStatusBadge } from '@/features/documents/components/DocumentStatusBadge';
import type { DocumentStatus } from '@/features/documents/models/document';
import { getDocumentStatusPresentation } from '@/features/documents/lib/statusLabels';

const CASES: ReadonlyArray<[DocumentStatus, string]> = [
  ['queued', 'En cola'],
  ['processing', 'Procesando'],
  ['completed', 'Completado'],
  ['failed', 'Fallido'],
];

describe('DocumentStatusBadge', () => {
  it.each(CASES)('renders %s as %s', (status, label) => {
    const { description } = getDocumentStatusPresentation(status);

    render(<DocumentStatusBadge status={status} />);

    expect(screen.getByTitle(description)).toHaveTextContent(label);
  });
});
