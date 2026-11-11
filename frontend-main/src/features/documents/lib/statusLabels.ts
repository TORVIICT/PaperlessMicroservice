import type { DocumentStatus } from '../models/document';

export type DocumentStatusTone =
  | 'neutral'
  | 'info'
  | 'warning'
  | 'success'
  | 'danger';

export interface DocumentStatusPresentation {
  label: string;
  description: string;
  tone: DocumentStatusTone;
}

export const DOCUMENT_STATUS_PRESENTATION: Record<
  DocumentStatus,
  DocumentStatusPresentation
> = {
  queued: {
    label: 'En cola',
    description: 'El documento fue recibido y espera ser procesado.',
    tone: 'neutral',
  },
  processing: {
    label: 'Procesando',
    description: 'El OCR está extrayendo el texto del documento.',
    tone: 'warning',
  },
  completed: {
    label: 'Completado',
    description: 'El texto extraído está disponible.',
    tone: 'success',
  },
  failed: {
    label: 'Fallido',
    description: 'El procesamiento del documento terminó con un error.',
    tone: 'danger',
  },
};

export function getDocumentStatusPresentation(
  status: DocumentStatus,
): DocumentStatusPresentation {
  return DOCUMENT_STATUS_PRESENTATION[status];
}
