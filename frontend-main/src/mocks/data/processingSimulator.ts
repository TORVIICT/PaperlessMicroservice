import type {
  DocumentProcessingError,
  DocumentStatus,
} from '@/features/documents/models/document';

const QUEUED_DURATION_MS = 2_000;
const PROCESSING_DURATION_MS = 4_000;
const TOTAL_DURATION_MS = QUEUED_DURATION_MS + PROCESSING_DURATION_MS;

const SIMULATED_FAILURE: DocumentProcessingError = Object.freeze({
  code: 'OCR_PROCESSING_FAILED',
  message: 'El OCR no pudo extraer texto del documento.',
});

export type ProcessingOutcome = 'completed' | 'failed';

export interface ProcessingSource {
  fileName: string;
  createdAt: string;
  outcome: ProcessingOutcome;
  alreadyProcessed: boolean;
  completedAt: string | null;
  pageCount: number | null;
  extractedText: string;
}

export interface ProcessingState {
  status: DocumentStatus;
  completedAt: string | null;
  pageCount: number | null;
  error: DocumentProcessingError | null;
  extractedText: string;
}

export function resolveProcessingState(
  source: ProcessingSource,
  now = Date.now(),
): ProcessingState {
  if (source.alreadyProcessed) {
    return buildFinalState(source);
  }

  const elapsedMs = now - new Date(source.createdAt).getTime();

  if (elapsedMs >= TOTAL_DURATION_MS) {
    return buildFinalState(source);
  }

  return {
    status: elapsedMs < QUEUED_DURATION_MS ? 'queued' : 'processing',
    completedAt: null,
    pageCount: null,
    error: null,
    extractedText: '',
  };
}

function buildFinalState(source: ProcessingSource): ProcessingState {
  const completedAt =
    source.completedAt ??
    new Date(new Date(source.createdAt).getTime() + TOTAL_DURATION_MS)
      .toISOString();

  if (source.outcome === 'failed') {
    return {
      status: 'failed',
      completedAt,
      pageCount: null,
      error: SIMULATED_FAILURE,
      extractedText: '',
    };
  }

  return {
    status: 'completed',
    completedAt,
    pageCount: source.pageCount ?? 1,
    error: null,
    extractedText:
      source.extractedText || `Texto simulado extraído de ${source.fileName}.`,
  };
}
