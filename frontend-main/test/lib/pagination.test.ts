import { describe, expect, it } from 'vitest';

import {
  getPageCount,
  getPageRange,
} from '@/features/documents/lib/pagination';

describe('getPageCount', () => {
  it('always exposes at least one page', () => {
    expect(getPageCount(0, 20)).toBe(1);
    expect(getPageCount(20, 20)).toBe(1);
    expect(getPageCount(1, 20)).toBe(1);
  });

  it('rounds up partial pages', () => {
    expect(getPageCount(21, 20)).toBe(2);
    expect(getPageCount(40, 20)).toBe(2);
    expect(getPageCount(41, 20)).toBe(3);
  });

  it('handles invalid values defensively', () => {
    expect(getPageCount(10, 0)).toBe(1);
    expect(getPageCount(Number.NaN, 20)).toBe(1);
  });
});

describe('getPageRange', () => {
  it('returns the visible range for a page', () => {
    expect(getPageRange(1, 20, 25)).toEqual({ from: 1, to: 20 });
    expect(getPageRange(2, 20, 25)).toEqual({ from: 21, to: 25 });
  });

  it('returns null when there are no documents', () => {
    expect(getPageRange(1, 20, 0)).toBeNull();
  });

  it('clamps out-of-range pages', () => {
    expect(getPageRange(0, 20, 25)).toEqual({ from: 1, to: 20 });
    expect(getPageRange(99, 20, 25)).toEqual({ from: 21, to: 25 });
  });
});
