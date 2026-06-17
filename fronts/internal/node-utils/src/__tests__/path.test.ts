// pathUtils.test.ts

import { describe, expect, it } from 'vitest';

import { toPosixPath } from '../path';

describe('toPosixPath', () => {
  // Windows 스타일 경로를 POSIX 스타일 경로로 변환 테스트
  it('converts Windows-style paths to POSIX paths', () => {
    const windowsPath = String.raw`C:\Users\Example\file.txt`;
    const expectedPosixPath = 'C:/Users/Example/file.txt';
    expect(toPosixPath(windowsPath)).toBe(expectedPosixPath);
  });

  // POSIX 스타일 경로가 변경되지 않는지 확인
  it('leaves POSIX-style paths unchanged', () => {
    const posixPath = '/home/user/file.txt';
    expect(toPosixPath(posixPath)).toBe(posixPath);
  });

  // 여러 구분자가 포함된 경로 테스트
  it('converts paths with mixed separators', () => {
    const mixedPath = String.raw`C:/Users\Example\file.txt`;
    const expectedPosixPath = 'C:/Users/Example/file.txt';
    expect(toPosixPath(mixedPath)).toBe(expectedPosixPath);
  });

  // 빈 문자열 테스트
  it('handles empty strings', () => {
    const emptyPath = '';
    expect(toPosixPath(emptyPath)).toBe('');
  });

  // 구분자만 포함된 경로 테스트
  it('handles path with only separators', () => {
    const separatorsPath = '\\\\\\';
    const expectedPosixPath = '///';
    expect(toPosixPath(separatorsPath)).toBe(expectedPosixPath);
  });

  // 구분자가 없는 경로 테스트
  it('handles path without separators', () => {
    const noSeparatorPath = 'file.txt';
    expect(toPosixPath(noSeparatorPath)).toBe('file.txt');
  });

  // 구분자로 끝나는 경로 테스트
  it('handles path ending with a separator', () => {
    const endingSeparatorPath = 'C:\\Users\\Example\\';
    const expectedPosixPath = 'C:/Users/Example/';
    expect(toPosixPath(endingSeparatorPath)).toBe(expectedPosixPath);
  });

  // 구분자로 시작하는 경로 테스트
  it('handles path starting with a separator', () => {
    const startingSeparatorPath = String.raw`\Users\Example`;
    const expectedPosixPath = '/Users/Example';
    expect(toPosixPath(startingSeparatorPath)).toBe(expectedPosixPath);
  });

  // 유효하지 않은 문자가 포함된 경로 테스트
  it('handles path with invalid characters', () => {
    const invalidCharsPath = String.raw`C:\Us*?ers\Ex<ample>|file.txt`;
    const expectedPosixPath = 'C:/Us*?ers/Ex<ample>|file.txt';
    expect(toPosixPath(invalidCharsPath)).toBe(expectedPosixPath);
  });
});
