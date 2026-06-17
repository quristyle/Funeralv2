import { beforeEach, describe, expect, it, vi } from 'vitest';

import { loadScript } from '../resources';

describe('loadScript', () => {
  beforeEach(() => {
    // 각 테스트 전 head를 비워 깨끗한 환경 보장
    document.head.innerHTML = '';
  });

  it('should resolve when the script loads successfully', async () => {
    // happy-dom v20+ auto-fires 'load' via handleDisabledFileLoadingAsSuccess
    const promise = loadScript('/test-script.js');

    const script = document.querySelector(
      'script[src="/test-script.js"]',
    ) as HTMLScriptElement;
    expect(script).toBeTruthy();

    await expect(promise).resolves.toBeUndefined();
  });

  it('should not insert duplicate script and resolve immediately if already loaded', async () => {
    // 먼저 동일한 src의 스크립트를 수동으로 삽입
    const existing = document.createElement('script');
    existing.src = 'bar.js';
    document.head.append(existing);

    // 다시 호출
    const promise = loadScript('bar.js');

    // 즉시 resolve
    await expect(promise).resolves.toBeUndefined();

    // head에 하나만 유지됨
    const scripts = document.head.querySelectorAll('script[src="bar.js"]');
    expect(scripts).toHaveLength(1);
  });

  it('should reject when the script fails to load', async () => {
    let capturedScript: HTMLScriptElement | null = null;

    // append를 가로채서 script 요소를 캡처하지만 DOM에 삽입하지 않음,
    // happy-dom v20+가 자동으로 load 이벤트를 트리거하는 것을 방지
    const appendSpy = vi
      .spyOn(document.head, 'append')
      .mockImplementation((...nodes) => {
        for (const node of nodes) {
          if (node instanceof HTMLScriptElement) {
            capturedScript = node;
          }
        }
      });

    const promise = loadScript('error.js');

    appendSpy.mockRestore();

    expect(capturedScript).toBeTruthy();
    if (!capturedScript) {
      throw new Error('Expected the captured script element to exist');
    }
    capturedScript.dispatchEvent(new Event('error'));

    await expect(promise).rejects.toThrow('Failed to load script: error.js');
  });

  it('should handle multiple concurrent calls and only insert one script tag', async () => {
    const p1 = loadScript('/test-script.js');
    const p2 = loadScript('/test-script.js');

    // happy-dom v20+ auto-fires 'load'，두 promise 모두 resolve 되어야 함
    await expect(p1).resolves.toBeUndefined();
    await expect(p2).resolves.toBeUndefined();

    // 한 번만 삽입됨
    const scripts = document.head.querySelectorAll(
      'script[src="/test-script.js"]',
    );
    expect(scripts).toHaveLength(1);
  });
});
