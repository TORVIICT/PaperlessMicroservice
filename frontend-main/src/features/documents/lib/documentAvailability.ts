import type { DocumentStatus } from '../models/document';

export function isDocumentReadyForResults(status: DocumentStatus): boolean {
  return status === 'completed';
}
