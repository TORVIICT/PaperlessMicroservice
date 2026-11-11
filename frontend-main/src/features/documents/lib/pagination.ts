const FIRST_PAGE = 1;

export interface DocumentPageRange {
  from: number;
  to: number;
}

export function getPageCount(total: number, pageSize: number): number {
  if (!Number.isFinite(total) || total <= 0) {
    return FIRST_PAGE;
  }

  if (!Number.isFinite(pageSize) || pageSize <= 0) {
    return FIRST_PAGE;
  }

  return Math.max(FIRST_PAGE, Math.ceil(total / pageSize));
}

export function getPageRange(
  page: number,
  pageSize: number,
  total: number,
): DocumentPageRange | null {
  if (!Number.isFinite(total) || total <= 0) {
    return null;
  }

  const pageCount = getPageCount(total, pageSize);
  const requestedPage = Number.isFinite(page) ? Math.trunc(page) : FIRST_PAGE;
  const requestedPageSize = Number.isFinite(pageSize)
    ? Math.trunc(pageSize)
    : FIRST_PAGE;

  const safePage = Math.min(Math.max(requestedPage, FIRST_PAGE), pageCount);
  const safePageSize = Math.max(requestedPageSize, FIRST_PAGE);

  return {
    from: (safePage - 1) * safePageSize + 1,
    to: Math.min(safePage * safePageSize, total),
  };
}
