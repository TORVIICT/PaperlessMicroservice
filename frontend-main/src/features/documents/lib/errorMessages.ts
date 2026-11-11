import { ApiError } from '@/shared/api/ApiError';

const FALLBACK_CODE = 'UNEXPECTED_ERROR';

const ERROR_MESSAGES: Record<string, string> = {
  INVALID_FILE: 'El archivo no es válido. Seleccione otro documento.',
  UNSUPPORTED_FILE_TYPE: 'El formato del archivo no está soportado.',
  FILE_TOO_LARGE: 'El archivo supera el tamaño máximo permitido.',
  DOCUMENT_NOT_FOUND: 'El documento no existe o fue eliminado.',
  DOCUMENT_NOT_READY:
    'El documento todavía se está procesando. Intente nuevamente en unos segundos.',
  OCR_PROCESSING_FAILED:
    'El procesamiento OCR del documento falló. Revise el detalle del error.',
  SERVICE_UNAVAILABLE:
    'El servicio OCR no está disponible. Intente nuevamente más tarde.',
  [FALLBACK_CODE]: 'Ocurrió un error inesperado. Intente nuevamente.',
};

export interface UserFacingError {
  code: string;
  message: string;
  status: number | null;
}

export function getUserFacingMessage(code: string): string {
  return ERROR_MESSAGES[code] ?? ERROR_MESSAGES[FALLBACK_CODE];
}

export function toUserFacingError(error: unknown): UserFacingError {
  if (!(error instanceof ApiError)) {
    return {
      code: FALLBACK_CODE,
      message: ERROR_MESSAGES[FALLBACK_CODE],
      status: null,
    };
  }

  const code =
    error.status >= 500 && error.code === FALLBACK_CODE
      ? 'SERVICE_UNAVAILABLE'
      : error.code;

  return {
    code,
    message: ERROR_MESSAGES[code] ?? error.message,
    status: error.status,
  };
}
