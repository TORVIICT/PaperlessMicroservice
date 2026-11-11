export const NETWORK_ERROR_STATUS = 0;

export interface ApiErrorPayload {
  code: string;
  message: string;
  documentId: string | null;
  details: Record<string, unknown>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly documentId: string | null;
  readonly details: Record<string, unknown>;

  constructor(status: number, payload: ApiErrorPayload) {
    super(payload.message);
    this.name = 'ApiError';
    this.status = status;
    this.code = payload.code;
    this.documentId = payload.documentId;
    this.details = payload.details;
  }
}
