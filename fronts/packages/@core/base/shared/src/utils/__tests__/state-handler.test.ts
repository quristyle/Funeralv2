import { describe, expect, it } from 'vitest';

import { StateHandler } from '../state-handler';

describe('stateHandler', () => {
  it('should resolve when condition is set to true', async () => {
    const handler = new StateHandler();

    // 비동기로 condition을 true로 설정하는 시뮬레이션
    setTimeout(() => {
      handler.setConditionTrue(); // condition을 true로 명시적으로 트리거
    }, 10);

    // 조건이 true로 설정될 때까지 대기
    await handler.waitForCondition();
    expect(handler.isConditionTrue()).toBe(true);
  });

  it('should resolve immediately if condition is already true', async () => {
    const handler = new StateHandler();
    handler.setConditionTrue(); // 미리 true로 설정

    // condition이 이미 true이므로 즉시 resolve
    await handler.waitForCondition();
    expect(handler.isConditionTrue()).toBe(true);
  });

  it('should reject when condition is set to false after waiting', async () => {
    const handler = new StateHandler();

    // 비동기로 condition을 false로 설정하는 시뮬레이션
    setTimeout(() => {
      handler.setConditionFalse(); // condition을 false로 명시적으로 트리거
    }, 10);

    // 대기 중 Promise가 reject되기를 기대함
    await expect(handler.waitForCondition()).rejects.toThrow();
    expect(handler.isConditionTrue()).toBe(false);
  });

  it('should reset condition to false', () => {
    const handler = new StateHandler();
    handler.setConditionTrue(); // true로 설정
    handler.reset(); // false로 재설정

    expect(handler.isConditionTrue()).toBe(false);
  });

  it('should resolve when condition is set to true after reset', async () => {
    const handler = new StateHandler();
    handler.reset(); // 초기값이 false인지 확인

    setTimeout(() => {
      handler.setConditionTrue(); // 재설정 후 true로 설정
    }, 10);

    await handler.waitForCondition();
    expect(handler.isConditionTrue()).toBe(true);
  });
});
