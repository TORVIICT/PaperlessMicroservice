export const MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024;
export const MAX_FILE_SIZE_MB = MAX_FILE_SIZE_BYTES / (1024 * 1024);

export const SUPPORTED_CONTENT_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/tiff',
  'image/gif',
  'image/bmp',
  'image/webp',
  'image/heic',
  'image/heif',
] as const;

const SUPPORTED_CONTENT_TYPE_SET: ReadonlySet<string> = new Set(
  SUPPORTED_CONTENT_TYPES,
);

export function isSupportedContentType(contentType: string): boolean {
  return SUPPORTED_CONTENT_TYPE_SET.has(contentType);
}
