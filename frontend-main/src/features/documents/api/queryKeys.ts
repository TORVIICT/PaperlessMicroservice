import type { DocumentListParams } from '../models/document';

export const documentKeys = {
  all: ['documents'] as const,
  lists: () => [...documentKeys.all, 'list'] as const,
  list: (params: DocumentListParams) =>
    [...documentKeys.lists(), params] as const,
  details: () => [...documentKeys.all, 'detail'] as const,
  detail: (documentId: string) =>
    [...documentKeys.details(), documentId] as const,
  text: (documentId: string) =>
    [...documentKeys.detail(documentId), 'text'] as const,
};
