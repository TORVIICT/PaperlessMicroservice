const DOCUMENTS_PATH = '/documents';

export const ROUTES = Object.freeze({
  documents: DOCUMENTS_PATH,
  documentDetail: `${DOCUMENTS_PATH}/:documentId`,
});

export function documentDetailPath(documentId: string): string {
  return `${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}`;
}
