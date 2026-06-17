import { describe, expect, it } from 'vitest';

import { defaultPreferences } from '../src/config';

describe('defaultPreferences immutability test', () => {
  // 기본 설정 객체가 수정되지 않았음을 보장하기 위해 스냅샷 생성
  it('should not modify the config object', () => {
    expect(defaultPreferences).toMatchSnapshot();
  });
});
