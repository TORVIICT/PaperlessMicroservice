import {
  isSupportedContentType,
  MAX_FILE_SIZE_BYTES,
} from '../models/documentConstraints';

export type DocumentFileValidationCode =
  | 'INVALID_FILE'
  | 'UNSUPPORTED_FILE_TYPE'
  | 'FILE_TOO_LARGE';

export function validateDocumentFile(
  file: File,
): DocumentFileValidationCode | null {
  if (file.size === 0) {
    return 'INVALID_FILE';
  }

  if (!isSupportedContentType(file.type)) {
    return 'UNSUPPORTED_FILE_TYPE';
  }

  if (file.size > MAX_FILE_SIZE_BYTES) {
    return 'FILE_TOO_LARGE';
  }

  return null;
}
