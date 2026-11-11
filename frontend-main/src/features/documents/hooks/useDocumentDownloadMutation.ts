import { useMutation } from '@tanstack/react-query';

import { documentsApi } from '../api/documentsApi';
import {
  resolveDownloadFileName,
  type DocumentDownloadVariant,
} from '../lib/downloadFileName';
import { saveBlob } from '../lib/saveBlob';
import type { OcrDocument } from '../models/document';

export interface DownloadDocumentVariables {
  document: Pick<OcrDocument, 'id' | 'fileName'>;
  variant: DocumentDownloadVariant;
}

export function useDocumentDownloadMutation() {
  return useMutation({
    mutationFn: ({ document, variant }: DownloadDocumentVariables) =>
      variant === 'original'
        ? documentsApi.downloadOriginal(document.id)
        : documentsApi.downloadText(document.id),
    onSuccess: (blob, { document, variant }) => {
      saveBlob(blob, resolveDownloadFileName(document.fileName, variant));
    },
  });
}
