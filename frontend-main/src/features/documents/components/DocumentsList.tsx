import type { OcrDocument } from '../models/document';
import { DocumentsListItem } from './DocumentsListItem';

interface DocumentsListProps {
  documents: readonly OcrDocument[];
  isUpdating?: boolean;
}

export function DocumentsList({
  documents,
  isUpdating = false,
}: DocumentsListProps) {
  return (
    <section
      aria-labelledby="documents-list-heading"
      aria-busy={isUpdating}
      className="transition-opacity"
    >
      <h2 id="documents-list-heading" className="sr-only">
        Listado de documentos
      </h2>
      <ul className={`space-y-3 ${isUpdating ? 'opacity-60' : ''}`}>
        {documents.map((document) => (
          <DocumentsListItem key={document.id} document={document} />
        ))}
      </ul>
    </section>
  );
}
