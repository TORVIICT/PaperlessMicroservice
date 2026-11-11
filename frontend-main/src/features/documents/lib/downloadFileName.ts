export type DocumentDownloadVariant = 'original' | 'text';

export function resolveDownloadFileName(
  fileName: string,
  variant: DocumentDownloadVariant,
): string {
  if (variant === 'original') {
    return fileName;
  }

  const baseName = fileName.replace(/\.[^.]+$/, '');
  return `${baseName}.txt`;
}
