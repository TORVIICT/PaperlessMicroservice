const SIZE_UNITS = [
  { threshold: 1024 ** 3, suffix: 'GB' },
  { threshold: 1024 ** 2, suffix: 'MB' },
  { threshold: 1024, suffix: 'kB' },
] as const;

const DECIMAL_FORMATTER = new Intl.NumberFormat('es-AR', {
  maximumFractionDigits: 1,
});

const DATE_FORMATTER = new Intl.DateTimeFormat('es-AR', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const CONTENT_TYPE_LABELS: Readonly<Record<string, string>> = {
  'application/pdf': 'PDF',
  'image/jpeg': 'JPG',
  'image/png': 'PNG',
  'image/tiff': 'TIFF',
  'image/gif': 'GIF',
  'image/bmp': 'BMP',
  'image/webp': 'WebP',
  'image/heic': 'HEIC',
  'image/heif': 'HEIF',
};

const UNKNOWN_VALUE = '—';

export function formatFileSize(sizeBytes: number): string {
  if (!Number.isFinite(sizeBytes) || sizeBytes < 0) {
    return UNKNOWN_VALUE;
  }

  if (sizeBytes < 1024) {
    return `${sizeBytes} B`;
  }

  const unit =
    SIZE_UNITS.find(({ threshold }) => sizeBytes >= threshold) ??
    SIZE_UNITS[SIZE_UNITS.length - 1];

  return `${DECIMAL_FORMATTER.format(sizeBytes / unit.threshold)} ${unit.suffix}`;
}

export function formatDocumentDate(createdAt: string): string {
  const date = new Date(createdAt);

  return Number.isNaN(date.getTime())
    ? UNKNOWN_VALUE
    : DATE_FORMATTER.format(date);
}

export function formatOptionalDocumentDate(value: string | null): string {
  return value ? formatDocumentDate(value) : UNKNOWN_VALUE;
}

export function formatPageCount(pageCount: number | null): string {
  if (pageCount === null || !Number.isInteger(pageCount) || pageCount < 0) {
    return UNKNOWN_VALUE;
  }

  return `${pageCount} ${pageCount === 1 ? 'página' : 'páginas'}`;
}

export function formatContentType(contentType: string): string {
  return CONTENT_TYPE_LABELS[contentType] ?? (contentType || UNKNOWN_VALUE);
}

export function formatContentTypeList(contentTypes: readonly string[]): string {
  return contentTypes.map(formatContentType).join(', ');
}
