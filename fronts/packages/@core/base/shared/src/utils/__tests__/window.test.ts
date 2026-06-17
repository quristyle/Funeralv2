import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { openWindow } from '../window';

describe('openWindow', () => {
  // 원본 window.open 함수 저장
  let originalOpen: typeof window.open;

  beforeEach(() => {
    originalOpen = window.open;
  });

  afterEach(() => {
    window.open = originalOpen;
  });

  it('should call window.open with correct arguments', () => {
    const url = 'https://example.com';
    const options = { noopener: true, noreferrer: true, target: '_blank' };

    window.open = vi.fn();

    // 함수 호출
    openWindow(url, options);

    // window.open이 올바르게 호출되었는지 검증
    expect(window.open).toHaveBeenCalledWith(
      url,
      options.target,
      'noopener=yes,noreferrer=yes',
    );
  });
});
